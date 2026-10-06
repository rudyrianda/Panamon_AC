using System.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Data.SqlClient;

namespace MonitoringSystem.Pages.ACOEE.Performance
{
    // Performance AC OEE = tampilan Performance Assembly, data Expander Kyoshin 6.35 (MCH1-01) dari PLC:
    //   dbo.PlcKyoshinTrend      : layar utama GOT per menit (R10 model, R20 prod. plan, R23 plan by SUT, R22 actual, R24 defect)
    //   dbo.PlcKyoshinLossEvent  : loss time (timer R103), sudah dipotong per shift
    //   dbo.MasterProduct        : SUT per model (Machine = Expander Kyoshin 6.35)
    //   BreakTimeService         : jadwal istirahat, untuk working time
    // Hari produksi 07:00 - 07:00 besoknya; shift 1 = 07:00-15:45, 2 = 15:45-23:15, 3 = 23:15-07:00.
    public class IndexModel : PageModel
    {
        public const string MachineCode = "MCH1-01";
        public const string MachineName = "Expander Kyoshin 6.35";
        private const int CycleWindowMinutes = 5;   // actual cycle = rata-rata 5 menit terakhir (data PLC per menit)
        private const int MaxSampleGapMinutes = 3;  // jeda sampel lebih lama = logger/PLC mati, cycle tidak dihitung melewatinya

        private static readonly (int No, TimeSpan Start, TimeSpan End)[] Shifts =
        {
            (1, new TimeSpan(7, 0, 0), new TimeSpan(15, 45, 0)),
            (2, new TimeSpan(15, 45, 0), new TimeSpan(23, 15, 0)),
            (3, new TimeSpan(23, 15, 0), new TimeSpan(31, 0, 0)) // 07:00 besoknya
        };

        private readonly string _connectionString;
        private readonly MonitoringSystem.Services.BreakTimeService _breakTimeService;
        private readonly ILogger<IndexModel> _logger;

        public IndexModel(IConfiguration configuration, MonitoringSystem.Services.BreakTimeService breakTimeService, ILogger<IndexModel> logger)
        {
            _connectionString = configuration.GetConnectionString("DefaultConnection") ?? "";
            _breakTimeService = breakTimeService;
            _logger = logger;
        }

        // Tanpa filter shift: satu hari produksi penuh (07:00 - 07:00 besoknya)
        [BindProperty(SupportsGet = true)]
        public DateTime? SelectedDate { get; set; }

        public PerformanceData Data { get; private set; } = new();

        public sealed class CyclePoint
        {
            public string Label { get; set; } = "";
            public string ModelProduk { get; set; } = "";
            public double PlanCycleTime { get; set; }
            public double ActualCycleTime { get; set; }
        }

        public sealed class HourlyRow
        {
            public string Time { get; set; } = "";
            public string Model { get; set; } = "";
            public int Plan { get; set; }
            public int Actual { get; set; }
        }

        public sealed class PerformanceData
        {
            public int PlanProd { get; set; }
            public int TargetProd { get; set; }
            public int ActualProd { get; set; }
            public int DifferenceProd { get; set; }
            public double EfficiencyValue { get; set; }
            public double PlanTaktTime { get; set; }
            public double ActTaktTime { get; set; }
            public double DefectRatio { get; set; } = 100;
            public int Defect { get; set; }
            public int WorkingTime { get; set; }
            public int LossTime { get; set; }
            public string CurrentModel { get; set; } = "";
            public string LastSampleAt { get; set; } = "";
            public List<HourlyRow> HourlyList { get; set; } = new();
            public List<CyclePoint> CycleTimeData { get; set; } = new();
            public double? DailyAverageCycleTime { get; set; }
            public double? DailyMinimumCycleTime { get; set; }
            public double? DailyMaximumCycleTime { get; set; }
        }

        public async Task OnGetAsync()
        {
            NormalizeFilter(DateTime.Now);
            Data = await LoadAsync(SelectedDate!.Value, 0, DateTime.Now);
        }

        public async Task<IActionResult> OnGetUpdatedDataAsync()
        {
            NormalizeFilter(DateTime.Now);
            return new JsonResult(await LoadAsync(SelectedDate!.Value, 0, DateTime.Now));
        }

        public static (DateTime Date, int Shift) ProductionShiftOf(DateTime at)
        {
            var date = at.TimeOfDay < Shifts[0].Start ? at.Date.AddDays(-1) : at.Date;
            var offset = at - date;
            var shift = Shifts.First(s => offset >= s.Start && offset < s.End).No;
            return (date, shift);
        }

        // Tanpa pilihan: hari produksi yang sedang berjalan (sebelum 07:00 = hari sebelumnya)
        private void NormalizeFilter(DateTime now)
        {
            if (SelectedDate == null || SelectedDate.Value.Year is < 2000 or > 2100)
                SelectedDate = ProductionShiftOf(now).Date;
            SelectedDate = SelectedDate.Value.Date;
        }

        public sealed record Sample(DateTime At, string Model, int R20, int R23, int R22, int R24);

        private async Task<PerformanceData> LoadAsync(DateTime productionDate, int shift, DateTime now)
        {
            var result = new PerformanceData();
            var dayStart = productionDate.Date.AddHours(7);
            var windowStart = shift == 0 ? dayStart : productionDate.Date + Shifts[shift - 1].Start;
            var windowEnd = shift == 0 ? dayStart.AddDays(1) : productionDate.Date + Shifts[shift - 1].End;
            var effectiveEnd = now < windowEnd ? now : windowEnd;
            if (effectiveEnd <= windowStart) return result; // shift belum mulai

            var samples = new List<Sample>();
            var seeds = new List<Sample>(); // nilai counter terakhir tiap (model, PROD. PLAN) sebelum periode, terbaru dulu
            var sut = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
            try
            {
                await using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();

                await using (var cmd = new SqlCommand(@"
                    SELECT SampleAt, Model, ProdPlan, PlanBySut, Actual, Defect FROM (
                        SELECT SampleAt, LTRIM(RTRIM(Model)) AS Model, ProdPlan, PlanBySut, Actual, Defect,
                               ROW_NUMBER() OVER (PARTITION BY LTRIM(RTRIM(Model)), ProdPlan ORDER BY SampleAt DESC) AS rn
                        FROM dbo.PlcKyoshinTrend
                        WHERE MachineCode = @mc AND SampleAt < @from AND SampleAt >= DATEADD(DAY, -7, @from) AND LTRIM(RTRIM(Model)) <> ''
                    ) x WHERE rn = 1 ORDER BY SampleAt DESC;
                    SELECT SampleAt, Model, ProdPlan, PlanBySut, Actual, Defect FROM dbo.PlcKyoshinTrend
                    WHERE MachineCode = @mc AND SampleAt >= @from AND SampleAt < @to
                    ORDER BY SampleAt;
                    SELECT ISNULL(SUM(DurationMin), 0) FROM dbo.PlcKyoshinLossEvent
                    WHERE MachineCode = @mc AND ProductionDate = @pd AND (@shift = 0 OR ShiftNo = @shift);
                    SELECT LTRIM(RTRIM(Model)), Sut FROM dbo.MasterProduct WHERE Machine = @machineName AND Sut > 0;", conn))
                {
                    cmd.Parameters.Add("@mc", SqlDbType.NVarChar, 20).Value = MachineCode;
                    cmd.Parameters.Add("@from", SqlDbType.DateTime2).Value = windowStart;
                    cmd.Parameters.Add("@to", SqlDbType.DateTime2).Value = windowEnd;
                    cmd.Parameters.Add("@pd", SqlDbType.Date).Value = productionDate.Date;
                    cmd.Parameters.Add("@shift", SqlDbType.Int).Value = shift;
                    cmd.Parameters.Add("@machineName", SqlDbType.VarChar, 100).Value = MachineName;
                    await using var r = await cmd.ExecuteReaderAsync();
                    Sample Read() => new(r.GetDateTime(0), (r.IsDBNull(1) ? "" : r.GetString(1)).Trim(),
                        r.GetInt32(2), r.GetInt32(3), r.GetInt32(4), r.GetInt32(5));
                    while (await r.ReadAsync()) seeds.Add(Read());
                    await r.NextResultAsync();
                    while (await r.ReadAsync()) samples.Add(Read());
                    await r.NextResultAsync();
                    if (await r.ReadAsync()) result.LossTime = Convert.ToInt32(r.GetValue(0));
                    await r.NextResultAsync();
                    while (await r.ReadAsync())
                        if (!r.IsDBNull(0)) sut.TryAdd(r.GetString(0), Convert.ToDouble(r.GetDecimal(1)));
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "AC OEE Performance: gagal membaca data PLC Expander {Date:yyyy-MM-dd} shift {Shift}", productionDate, shift);
                return result;
            }

            var breaks = new List<(DateTime Start, DateTime End)>();
            try
            {
                foreach (var b in await _breakTimeService.GetBreakTimesForDateAsync(productionDate.Date))
                {
                    var start = productionDate.Date + b.StartTime;
                    if (b.StartTime < Shifts[0].Start) start = start.AddDays(1); // istirahat 00:00-07:00 = malam hari produksi ini
                    var duration = b.EndTime - b.StartTime;
                    if (duration < TimeSpan.Zero) duration += TimeSpan.FromDays(1); // istirahat melewati 00:00
                    breaks.Add((start, start + duration));
                }
            }
            catch (Exception ex) { _logger.LogWarning(ex, "AC OEE Performance: jadwal istirahat tidak terbaca"); }

            return Compute(result, samples, seeds, sut, breaks, windowStart, windowEnd, effectiveEnd);
        }

        // Hitung semua angka halaman dari sampel PLC periode [windowStart, windowEnd)
        // (seeds = nilai counter terakhir tiap model + PROD. PLAN sebelum periode, terbaru dulu)
        public static PerformanceData Compute(PerformanceData result, List<Sample> samples, List<Sample> seeds,
            Dictionary<string, double> sut, List<(DateTime Start, DateTime End)> breaks,
            DateTime windowStart, DateTime windowEnd, DateTime effectiveEnd)
        {
            double SutOf(string model) => sut.TryGetValue(model, out var v) ? v : 0;

            // Kenaikan counter per sampel, aturan sama dengan Plclogger (BuildKyoshinSteps): counter R22/R23/R24 milik baris plan,
            // kembali ke baris lama = counter melanjutkan nilainya. Patokan = nilai terakhir model + PROD. PLAN yang sama,
            // lalu nilai terakhir model itu, lalu 0. Nilai turun = counter di-reset (dihitung dari 0).
            var byRow = new Dictionary<(string, int), (int R23, int R22, int R24)>();
            var byModel = new Dictionary<string, (int R23, int R22, int R24)>();
            foreach (var s in seeds)
            {
                var key = s.Model.ToUpperInvariant();
                if (key.Length == 0) continue;
                var v = (Math.Max(0, s.R23), Math.Max(0, s.R22), Math.Max(0, s.R24));
                byRow.TryAdd((key, s.R20), v);
                byModel.TryAdd(key, v);
            }
            var steps = new List<(Sample S, int D23, int D22, int D24)>();
            foreach (var s in samples)
            {
                if (s.Model.Length == 0) continue;
                var key = s.Model.ToUpperInvariant();
                int r23 = Math.Max(0, s.R23), r22 = Math.Max(0, s.R22), r24 = Math.Max(0, s.R24);
                (int R23, int R22, int R24) prev = byRow.TryGetValue((key, s.R20), out var rv) ? rv : byModel.TryGetValue(key, out var mv) ? mv : (0, 0, 0);
                static int Up(int cur, int baseline) => cur >= baseline ? cur - baseline : cur;
                steps.Add((s, Up(r23, prev.R23), Up(r22, prev.R22), Up(r24, prev.R24)));
                byRow[(key, s.R20)] = byModel[key] = (r23, r22, r24);
            }

            result.TargetProd = steps.Sum(x => x.D23);
            result.ActualProd = steps.Sum(x => x.D22);
            result.Defect = steps.Sum(x => x.D24);
            result.DifferenceProd = result.ActualProd - result.TargetProd;

            // PLAN = PROD. PLAN (R20) terakhir tiap kali model jalan di periode ini
            string? runModel = null;
            int runPlan = 0;
            foreach (var (s, _, _, _) in steps)
            {
                if (!s.Model.Equals(runModel, StringComparison.OrdinalIgnoreCase))
                {
                    result.PlanProd += runPlan;
                    runModel = s.Model;
                }
                runPlan = Math.Max(0, s.R20);
            }
            result.PlanProd += runPlan;

            var last = steps.Count > 0 ? steps[^1].S : null;
            result.CurrentModel = last?.Model ?? "";
            result.LastSampleAt = last?.At.ToString("HH:mm") ?? "";
            result.PlanTaktTime = last == null ? 0 : SutOf(last.Model);
            result.EfficiencyValue = result.TargetProd > 0 ? Math.Round(result.ActualProd * 100.0 / result.TargetProd, 1) : 0;
            result.DefectRatio = result.ActualProd > 0
                ? Math.Round(Math.Max(0, result.ActualProd - result.Defect) * 100.0 / result.ActualProd, 1) : 100;

            // WORKING TIME = menit periode (sampai sekarang) dikurangi istirahat; ACT TAKT = (working - loss) / actual
            double breakMinutes = breaks.Sum(b =>
            {
                var from = b.Start > windowStart ? b.Start : windowStart;
                var to = b.End < effectiveEnd ? b.End : effectiveEnd;
                return to > from ? (to - from).TotalMinutes : 0;
            });
            result.WorkingTime = Math.Max(0, (int)Math.Round((effectiveEnd - windowStart).TotalMinutes - breakMinutes));
            var netMinutes = result.WorkingTime - result.LossTime;
            result.ActTaktTime = netMinutes > 0 && result.ActualProd > 0 ? Math.Round(netMinutes * 60.0 / result.ActualProd, 1) : 0;

            // HOURLY ACHIEVEMENT: per jam per model; sampel jam hh:mm:00 = produksi menit sebelumnya
            result.HourlyList = steps
                .Where(x => x.D22 > 0 || x.D23 > 0)
                .GroupBy(x =>
                {
                    var at = x.S.At.AddSeconds(-1);
                    var hour = new DateTime(at.Year, at.Month, at.Day, at.Hour, 0, 0);
                    return (Hour: hour < windowStart ? windowStart : hour, Model: x.S.Model);
                })
                .OrderByDescending(g => g.Key.Hour).ThenByDescending(g => g.Max(x => x.S.At))
                .Select(g =>
                {
                    var hourEnd = g.Key.Hour.Minute == 0 ? g.Key.Hour.AddHours(1) : g.Key.Hour.AddMinutes(60 - g.Key.Hour.Minute);
                    if (hourEnd > windowEnd) hourEnd = windowEnd;
                    return new HourlyRow
                    {
                        Time = $"{g.Key.Hour:HH:mm} - {hourEnd:HH:mm}",
                        Model = g.Key.Model,
                        Plan = g.Sum(x => x.D23),
                        Actual = g.Sum(x => x.D22)
                    };
                })
                .ToList();

            // CYCLE TIME: detik per unit dari kenaikan ACTUAL (R22) selama ±5 menit terakhir, model sama & tanpa jeda data
            int segStart = 0;
            for (int i = 0; i < samples.Count; i++)
            {
                var s = samples[i];
                if (s.Model.Length == 0) { segStart = i + 1; continue; }
                if (i > segStart)
                {
                    var p = samples[i - 1];
                    if (!p.Model.Equals(s.Model, StringComparison.OrdinalIgnoreCase) || s.R22 < p.R22 || (s.At - p.At).TotalMinutes > MaxSampleGapMinutes)
                        segStart = i;
                }
                int j = i;
                while (j > segStart && (s.At - samples[j - 1].At).TotalMinutes <= CycleWindowMinutes) j--;
                var minutes = (s.At - samples[j].At).TotalMinutes;
                var produced = s.R22 - samples[j].R22;
                if (minutes < CycleWindowMinutes - 1 || produced <= 0) continue;
                result.CycleTimeData.Add(new CyclePoint
                {
                    Label = s.At.ToString("HH:mm:ss"),
                    ModelProduk = s.Model,
                    PlanCycleTime = SutOf(s.Model),
                    ActualCycleTime = Math.Round(minutes * 60 / produced, 1)
                });
            }
            if (result.CycleTimeData.Count > 0)
            {
                result.DailyAverageCycleTime = Math.Round(result.CycleTimeData.Average(c => c.ActualCycleTime), 1);
                result.DailyMinimumCycleTime = result.CycleTimeData.Min(c => c.ActualCycleTime);
                result.DailyMaximumCycleTime = result.CycleTimeData.Max(c => c.ActualCycleTime);
            }
            return result;
        }
    }
}
