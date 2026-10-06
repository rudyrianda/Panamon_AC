using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;

// Samakan namespace ini dengan nama project Anda kalau berbeda.
namespace PWK_ACTUAL.Pages
{
    public class PwkModel : PageModel
    {
        private readonly IConfiguration _cfg;
        public PwkModel(IConfiguration cfg) => _cfg = cfg;

        // =============================================================================
        // KONFIGURASI - ubah di sini kalau standar pabrik berubah
        // =============================================================================
        private const string DefaultConn =
            "Server=.;Database=PROMOSYS;Trusted_Connection=True;TrustServerCertificate=True";

        private string ConnStr => _cfg.GetConnectionString("PROMOSYS") ?? DefaultConn;

        public const string LineName = "Assembly";

        private static readonly Dictionary<string, string> GroupMap = new()
        {
            ["Line CS"] = "MCH1-02",
            ["Line CU"] = "MCH1-01"
        };

        private static readonly string[] ShiftList = { "Shift 1", "Shift 2", "Shift 3", "Non-Shift" };
        private static readonly string[] SabtuList = { "Sabtu Kerja", "Sabtu Libur" };

        // (A) Working Time per orang, dalam menit
        private const int WT_NonShift = 473;   // Senin-Kamis, 540 gross - 67 rest
        private const int WT_NonShiftJumat = 433;   // 540 - 107
        private const int WT_Shift1 = 458;   // 525 - 67
        private const int WT_Shift1Jumat = 418;   // 525 - 107
        private const int WT_Shift2 = 393;   // 450 - 57
        private const int WT_Shift3 = 398;   // 465 - 67
        private const int WT_SabtuKerja = 473;
        private const int WT_SabtuLibur = 420;
        private const int WT_Minggu = 420;

        public static int HitungWorkingTime(DateTime tgl, string shift, string sabtuMode)
        {
            if (tgl.DayOfWeek == DayOfWeek.Sunday) return WT_Minggu;
            if (tgl.DayOfWeek == DayOfWeek.Saturday)
                return string.Equals(sabtuMode, "Sabtu Kerja", StringComparison.OrdinalIgnoreCase)
                    ? WT_SabtuKerja : WT_SabtuLibur;

            bool jumat = tgl.DayOfWeek == DayOfWeek.Friday;
            return shift switch
            {
                "Shift 1" => jumat ? WT_Shift1Jumat : WT_Shift1,
                "Shift 2" => WT_Shift2,
                "Shift 3" => WT_Shift3,
                _ => jumat ? WT_NonShiftJumat : WT_NonShift
            };
        }

        /// <summary>Jam operasional tiap shift. Shift 3 lewat tengah malam.</summary>
        public static (TimeSpan Start, TimeSpan End) ShiftWindow(string shift) => shift switch
        {
            "Shift 1" => (new TimeSpan(7, 0, 0), new TimeSpan(15, 45, 0)),
            "Shift 2" => (new TimeSpan(15, 45, 0), new TimeSpan(23, 15, 0)),
            "Shift 3" => (new TimeSpan(23, 15, 0), new TimeSpan(7, 0, 0)),
            _ => (new TimeSpan(7, 0, 0), new TimeSpan(16, 0, 0))
        };

        // Reason_ID 1-11 Fixed, 12-17 Work, 18-19 ORG, sisanya Defect
        private static string KategoriOf(int reasonId) =>
            reasonId <= 11 ? "FIXED LOSS" :
            reasonId <= 17 ? "WORK LOSS" :
            reasonId <= 19 ? "ORG LOSS" : "DEFFECT LOSS";

        /// <summary>Kolom LossTime di AssemblyLossTime berisi DETIK. Set false kalau suatu saat diganti menit.</summary>
        private const bool LossTimeDalamDetik = true;

        public static int KeMenit(int nilai) => LossTimeDalamDetik
            ? (int)Math.Round(nilai / 60.0, MidpointRounding.AwayFromZero)
            : nilai;

        public static readonly string[] KategoriList =
            { "FIXED LOSS", "WORK LOSS", "ORG LOSS", "DEFFECT LOSS" };

        // AssemblyLossTime.Reason berisi teks bebas yang kadang beda dengan master Reason.
        private static readonly Dictionary<string, string> ReasonAlias = new(StringComparer.OrdinalIgnoreCase)
        {
            ["model changing loss"] = "Model Change",
            ["mold changing loss"] = "Mold/Dies Change",
            ["mold/dies changing loss"] = "Mold/Dies Change",
            ["set repairing loss"] = "Set Repairing Lose",
            ["gawse external bodies"] = "GAWSE-External Bodies"
        };

        // =============================================================================
        // DTO
        // =============================================================================
        public class ReasonItem
        {
            public int Id { get; set; }
            public string Name { get; set; }
            public string Kategori { get; set; }
        }

        public class GivenRow
        {
            public string Item { get; set; }
            public int Terdaftar { get; set; }
            public int Absensi { get; set; }
            public int WaktuKerja { get; set; }
            public int Lembur { get; set; }
            public int Total => WaktuKerja + Lembur;
        }

        public class LossRow
        {
            public long Id { get; set; }
            public DateTime Start { get; set; }
            public DateTime? End { get; set; }
            public int Seconds { get; set; }              // nilai mentah kolom LossTime
            public int Minutes => KeMenit(Seconds);
            public int? ReasonId { get; set; }
            public string ReasonName { get; set; }
            public string RawReason { get; set; }
            public string Detail { get; set; }
            public string Jam => End.HasValue
                ? $"{Start:HH:mm} s/d {End.Value:HH:mm}"
                : $"{Start:HH:mm} s/d ";
        }

        public class ProdRow
        {
            public DateTime Jam { get; set; }
            public string Model { get; set; }
            public string SeriAwal { get; set; }
            public string SeriAkhir { get; set; }
            public int DailyPlan { get; set; }
            public int DailyActual { get; set; }
            public int AccmPlan { get; set; }
            public int AccmActual { get; set; }
            public int Defect { get; set; }
            public string Keterangan { get; set; }
            public string Time => $"{Jam:HH:mm} s/d {Jam.AddHours(1):HH:mm}";
        }

        public class CatatanRow
        {
            public string Hambatan { get; set; }
            public string Analisa { get; set; }
            public string Tindakan { get; set; }
            public string Pic { get; set; }
        }

        // =============================================================================
        // STATE HALAMAN
        // =============================================================================
        [BindProperty(SupportsGet = true)] public DateTime Tanggal { get; set; } = DateTime.Today;
        [BindProperty(SupportsGet = true)] public string GroupName { get; set; } = "Line CS";
        [BindProperty(SupportsGet = true)] public string Shift { get; set; } = "Shift 1";
        [BindProperty(SupportsGet = true)] public string SabtuMode { get; set; } = "Sabtu Libur";
        [BindProperty(SupportsGet = true)] public int JumlahOpr { get; set; }

        /// <summary>Kunci konteks (tanggal|mesin|shift) dari halaman sebelumnya.
        /// Dipakai untuk mendeteksi pergantian Line/Shift/Tanggal supaya isian tidak terbawa.</summary>
        [BindProperty] public string Ctx { get; set; }

        [BindProperty] public List<GivenRow> Given { get; set; } = new();
        [BindProperty] public List<CatatanRow> Catatan { get; set; } = new() { new CatatanRow() };

        public List<ReasonItem> Reasons { get; private set; } = new();
        public List<LossRow> Losses { get; private set; } = new();
        public List<ProdRow> Prods { get; private set; } = new();
        public string Pesan { get; private set; }
        public string Error { get; private set; }

        // ---- dipakai langsung oleh view ----
        public IEnumerable<string> GroupNames => GroupMap.Keys;
        public IEnumerable<string> Shifts => ShiftList;
        public IEnumerable<string> SabtuModes => SabtuList;
        public string MachineCode => GroupMap.TryGetValue(GroupName ?? "", out var m) ? m : "MCH1-02";
        public bool IsSabtu => Tanggal.DayOfWeek == DayOfWeek.Saturday;
        public int WorkingTime => HitungWorkingTime(Tanggal, Shift, SabtuMode);

        public int HdrNonShift => WT_NonShift;
        public int HdrNonShiftJumat => WT_NonShiftJumat;
        public int HdrShift1 => WT_Shift1;
        public int HdrShift2 => WT_Shift2;
        public int HdrShift3 => WT_Shift3;

        public int TotalGiven => Given.Sum(g => g.Total);
        public int TotalLoss => KeMenit(Losses.Sum(l => l.Seconds));
        public int TotalDefect => Prods.Sum(p => p.Defect);
        public int TotalPlan => Prods.Sum(p => p.DailyPlan);
        public int HasilProd => Prods.Sum(p => p.DailyActual);        // (J)
        public int AvailableWT => WorkingTime * JumlahOpr;              // (K)
        public decimal ProdHeadHour =>
            AvailableWT == 0 ? 0m : Math.Round(HasilProd * 60m / AvailableWT, 2);

        private static List<GivenRow> DefaultGiven(int wt) => new()
        {
            new GivenRow { Item = "Karyawan Permanent",   WaktuKerja = wt },
            new GivenRow { Item = "KWT",                  WaktuKerja = wt },
            new GivenRow { Item = "Magang",               WaktuKerja = wt },
            new GivenRow { Item = "Bantuan Masuk ( - )",  WaktuKerja = wt },
            new GivenRow { Item = "Bantuan Keluar ( + )", WaktuKerja = wt },
            new GivenRow { Item = "DT, PC, KP, CT ( - )", WaktuKerja = wt }
        };

        // =============================================================================
        // HANDLER
        // =============================================================================
        public async Task OnGetAsync() => await MuatAsync();

        public async Task OnPostAsync() => await MuatAsync();

        public async Task OnPostSetReasonAsync(long lossId, int reasonId)
        {
            try
            {
                await ExecAsync(@"
UPDATE a SET a.[Reason] = r.[ReasonName]
FROM [dbo].[AssemblyLossTime] a
CROSS JOIN [dbo].[Reason] r
WHERE a.[Id] = @lossId AND r.[Reason_ID] = @reasonId;", c =>
                {
                    c.Parameters.AddWithValue("@lossId", lossId);
                    c.Parameters.AddWithValue("@reasonId", reasonId);
                });
                Pesan = "Reason tersimpan.";
            }
            catch (Exception ex) { Error = ex.Message; }

            await MuatAsync();
        }

        public async Task OnPostSaveAsync()
        {
            await MuatAsync();
            if (Error != null) return;

            try
            {
                GivenRow G(int i) => Given.ElementAtOrDefault(i) ?? new GivenRow();
                var json = new JsonSerializerOptions { WriteIndented = false };

                await ExecAsync("dbo.usp_PWK_ACTUAL_Upsert", c =>
                {
                    c.CommandType = CommandType.StoredProcedure;
                    c.Parameters.AddWithValue("@Tanggal", Tanggal.Date);
                    c.Parameters.AddWithValue("@Line", LineName);
                    c.Parameters.AddWithValue("@GroupName", GroupName);
                    c.Parameters.AddWithValue("@MachineCode", MachineCode);
                    c.Parameters.AddWithValue("@Shift", Shift);
                    c.Parameters.AddWithValue("@SabtuMode", (object)(IsSabtu ? SabtuMode : null) ?? DBNull.Value);
                    c.Parameters.AddWithValue("@JumlahOpr", JumlahOpr);
                    c.Parameters.AddWithValue("@WorkingTime", WorkingTime);
                    c.Parameters.AddWithValue("@PermanentTerdaftar", G(0).Terdaftar);
                    c.Parameters.AddWithValue("@PermanentAbsensi", G(0).Absensi);
                    c.Parameters.AddWithValue("@PermanentLembur", G(0).Lembur);
                    c.Parameters.AddWithValue("@KwtTerdaftar", G(1).Terdaftar);
                    c.Parameters.AddWithValue("@KwtAbsensi", G(1).Absensi);
                    c.Parameters.AddWithValue("@KwtLembur", G(1).Lembur);
                    c.Parameters.AddWithValue("@MagangTerdaftar", G(2).Terdaftar);
                    c.Parameters.AddWithValue("@MagangAbsensi", G(2).Absensi);
                    c.Parameters.AddWithValue("@MagangLembur", G(2).Lembur);
                    c.Parameters.AddWithValue("@BantuanMasuk", G(3).Terdaftar);
                    c.Parameters.AddWithValue("@BantuanKeluar", G(4).Terdaftar);
                    c.Parameters.AddWithValue("@DtPcKpCt", G(5).Terdaftar);
                    c.Parameters.AddWithValue("@TotalGivenTime", TotalGiven);
                    c.Parameters.AddWithValue("@HasilProd", HasilProd);
                    c.Parameters.AddWithValue("@AvailableWorkTime", AvailableWT);
                    c.Parameters.AddWithValue("@ProdHeadHour", ProdHeadHour);
                    c.Parameters.AddWithValue("@TotalLossTime", TotalLoss);
                    c.Parameters.AddWithValue("@TotalDefectQty", TotalDefect);
                    c.Parameters.AddWithValue("@LossTimeJson", JsonSerializer.Serialize(
                        Losses.Select(l => new { l.Id, l.Jam, l.Minutes, l.ReasonId, l.ReasonName, l.Detail }), json));
                    c.Parameters.AddWithValue("@ProduksiJson", JsonSerializer.Serialize(
                        Prods.Select(p => new
                        {
                            p.Time,
                            p.Model,
                            p.SeriAwal,
                            p.SeriAkhir,
                            p.DailyPlan,
                            p.DailyActual,
                            p.AccmPlan,
                            p.AccmActual,
                            p.Defect,
                            p.Keterangan
                        }), json));
                    c.Parameters.AddWithValue("@CatatanJson", JsonSerializer.Serialize(Catatan, json));
                    c.Parameters.AddWithValue("@User", (object)User?.Identity?.Name ?? "system");
                });

                Pesan = $"PWK {Tanggal:dd-MM-yyyy} / {GroupName} / {Shift} tersimpan.";
            }
            catch (Exception ex) { Error = "Gagal menyimpan: " + ex.Message; }
        }

        // =============================================================================
        // MUAT DATA
        // =============================================================================
        private async Task MuatAsync()
        {
            if (Tanggal == default) Tanggal = DateTime.Today;

            // Kalau Tanggal / Line / Shift berganti, isian sebelumnya tidak boleh ikut terbawa:
            // CS dan CU punya jumlah orang sendiri-sendiri.
            var ctxSekarang = $"{Tanggal:yyyy-MM-dd}|{MachineCode}|{Shift}";
            if (!string.Equals(Ctx, ctxSekarang, StringComparison.Ordinal))
            {
                Given = null;
                Catatan = null;
                JumlahOpr = 0;
                try { await LoadSimpananAsync(); } catch { /* tabel PWK_ACTUAL belum ada */ }
            }
            Ctx = ctxSekarang;

            // Given Time: label dan Waktu Kerja selalu mengikuti kalkulasi (A)
            var labels = DefaultGiven(WorkingTime);
            if (Given == null || Given.Count != 6) Given = labels;
            for (int i = 0; i < Given.Count; i++)
            {
                Given[i].Item = labels[i].Item;
                Given[i].WaktuKerja = WorkingTime;
            }
            if (Catatan == null || Catatan.Count == 0)
                Catatan = new List<CatatanRow> { new CatatanRow() };

            var (start, end) = ShiftWindow(Shift);
            var dari = Tanggal.Date.Add(start);
            var sampai = end > start ? Tanggal.Date.Add(end) : Tanggal.Date.AddDays(1).Add(end);

            try
            {
                await LoadReasonsAsync();
                await LoadLossAsync(dari, sampai);
                await LoadProdAsync(dari, sampai);
            }
            catch (Exception ex)
            {
                Error = "Gagal membaca database: " + ex.Message;
            }
        }

        /// <summary>Ambil isian yang pernah disimpan untuk Tanggal + MachineCode + Shift ini.</summary>
        private async Task LoadSimpananAsync()
        {
            var rows = await QueryAsync(@"
SELECT TOP 1 [JumlahOpr],
       [PermanentTerdaftar],[PermanentAbsensi],[PermanentLembur],
       [KwtTerdaftar],[KwtAbsensi],[KwtLembur],
       [MagangTerdaftar],[MagangAbsensi],[MagangLembur],
       [BantuanMasuk],[BantuanKeluar],[DtPcKpCt],[CatatanJson]
FROM   [dbo].[PWK_ACTUAL]
WHERE  [Tanggal] = @tgl AND [MachineCode] = @mc AND [Shift] = @shift;",
                c =>
                {
                    c.Parameters.AddWithValue("@tgl", Tanggal.Date);
                    c.Parameters.AddWithValue("@mc", MachineCode);
                    c.Parameters.AddWithValue("@shift", Shift);
                },
                r =>
                {
                    int I(string k) => r[k] == DBNull.Value ? 0 : Convert.ToInt32(r[k]);
                    var g = DefaultGiven(WorkingTime);
                    g[0].Terdaftar = I("PermanentTerdaftar"); g[0].Absensi = I("PermanentAbsensi"); g[0].Lembur = I("PermanentLembur");
                    g[1].Terdaftar = I("KwtTerdaftar"); g[1].Absensi = I("KwtAbsensi"); g[1].Lembur = I("KwtLembur");
                    g[2].Terdaftar = I("MagangTerdaftar"); g[2].Absensi = I("MagangAbsensi"); g[2].Lembur = I("MagangLembur");
                    g[3].Terdaftar = I("BantuanMasuk");
                    g[4].Terdaftar = I("BantuanKeluar");
                    g[5].Terdaftar = I("DtPcKpCt");

                    List<CatatanRow> ctt = null;
                    var js = r["CatatanJson"] as string;
                    if (!string.IsNullOrWhiteSpace(js))
                    {
                        try { ctt = JsonSerializer.Deserialize<List<CatatanRow>>(js); } catch { }
                    }
                    return (Opr: I("JumlahOpr"), Given: g, Catatan: ctt);
                });

            if (rows.Count == 0) return;
            JumlahOpr = rows[0].Opr;
            Given = rows[0].Given;
            if (rows[0].Catatan != null && rows[0].Catatan.Count > 0) Catatan = rows[0].Catatan;
        }

        private async Task LoadReasonsAsync()
        {
            Reasons = await QueryAsync(
                "SELECT [Reason_ID],[ReasonName] FROM [dbo].[Reason] ORDER BY [Reason_ID];",
                null,
                r => new ReasonItem
                {
                    Id = Convert.ToInt32(r["Reason_ID"]),
                    Name = r["ReasonName"] as string,
                    Kategori = KategoriOf(Convert.ToInt32(r["Reason_ID"]))
                });
        }

        private async Task LoadLossAsync(DateTime dari, DateTime sampai)
        {
            // [Date] hanya menyimpan tanggal dan [Time]/[EndDateTime] hanya menyimpan jam,
            // jadi keduanya digabung dulu jadi datetime utuh sebelum difilter.
            // CAST(... AS time) aman dipakai baik kolomnya bertipe time maupun datetime.
            var raw = await QueryAsync(@"
;WITH src AS (
    SELECT  a.[Id], a.[LossTime], a.[Reason], a.[DetailedReason],
            DATEADD(SECOND, DATEDIFF(SECOND, 0, CAST(a.[Time] AS time)),
                    CAST(a.[Date] AS datetime)) AS Mulai,
            CASE WHEN a.[EndDateTime] IS NULL THEN NULL
                 ELSE DATEADD(SECOND, DATEDIFF(SECOND, 0, CAST(a.[EndDateTime] AS time)),
                              CAST(a.[Date] AS datetime)) END AS Selesai
    FROM    [dbo].[AssemblyLossTime] a
    WHERE   a.[MachineCode] = @mc
      AND   a.[Date] >= CAST(@dari AS date)
      AND   a.[Date] <= CAST(@sampai AS date)
)
SELECT  [Id],
        CONVERT(NVARCHAR(30), Mulai, 120) AS MulaiText,
        CONVERT(NVARCHAR(30), CASE WHEN Selesai < Mulai THEN DATEADD(DAY, 1, Selesai)
                                   ELSE Selesai END, 120) AS SelesaiText,
        [LossTime], [Reason], [DetailedReason]
FROM    src
WHERE   Mulai >= @dari AND Mulai < @sampai
ORDER BY Mulai, [Id];",
                c =>
                {
                    c.Parameters.AddWithValue("@mc", MachineCode);
                    c.Parameters.AddWithValue("@dari", dari);
                    c.Parameters.AddWithValue("@sampai", sampai);
                },
                r => new LossRow
                {
                    Id = Convert.ToInt64(r["Id"]),
                    Start = Convert.ToDateTime(r["MulaiText"], CultureInfo.InvariantCulture),
                    End = r["SelesaiText"] == DBNull.Value
                        ? (DateTime?)null
                        : Convert.ToDateTime(r["SelesaiText"], CultureInfo.InvariantCulture),
                    Seconds = r["LossTime"] == DBNull.Value ? 0 : Convert.ToInt32(r["LossTime"]),
                    RawReason = r["Reason"] as string,
                    Detail = r["DetailedReason"] as string
                });

            foreach (var l in raw)
            {
                var m = MatchReason(l.RawReason);
                l.ReasonId = m?.Id;
                l.ReasonName = m?.Name;
            }
            Losses = raw;
        }

        private ReasonItem MatchReason(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return null;
            var key = raw.Trim();
            if (ReasonAlias.TryGetValue(key, out var alias)) key = alias;
            return Reasons.FirstOrDefault(r => string.Equals(r.Name, key, StringComparison.OrdinalIgnoreCase))
                ?? Reasons.FirstOrDefault(r => Squash(r.Name) == Squash(key));
        }

        private static string Squash(string s) =>
            new string((s ?? "").Where(char.IsLetterOrDigit).ToArray()).ToLowerInvariant();

        private async Task LoadProdAsync(DateTime dari, DateTime sampai)
        {
            // Satu baris OEESN = satu serial number, jadi ACTUAL = COUNT(*).
            // Slot memakai jam dinding supaya shift yang mulai 15:45 tetap masuk kotak 15:00-16:00.
            var raw = await QueryAsync(@"
;WITH src AS (
    SELECT  o.[Date], o.[SN_GOOD], o.[EjectUnit], o.[Product_Id],
            DATEADD(HOUR, DATEDIFF(HOUR, 0, o.[Date]), 0) AS JamMulai,
            ROW_NUMBER() OVER (PARTITION BY DATEDIFF(HOUR, 0, o.[Date]), o.[Product_Id]
                               ORDER BY o.[Date] ASC,  o.[ID] ASC)  AS RnAsc,
            ROW_NUMBER() OVER (PARTITION BY DATEDIFF(HOUR, 0, o.[Date]), o.[Product_Id]
                               ORDER BY o.[Date] DESC, o.[ID] DESC) AS RnDesc
    FROM    [dbo].[OEESN] o
    WHERE   o.[MachineCode] = @mc AND o.[Date] >= @dari AND o.[Date] < @sampai
)
SELECT  s.JamMulai,
        ISNULL(m.[ProductName], CONVERT(NVARCHAR(50), s.[Product_Id])) AS Model,
        MAX(CASE WHEN s.RnAsc  = 1 THEN s.[SN_GOOD] END) AS SeriAwal,
        MAX(CASE WHEN s.RnDesc = 1 THEN s.[SN_GOOD] END) AS SeriAkhir,
        COUNT(*) AS DailyActual,
        SUM(CASE WHEN ISNULL(s.[EjectUnit],0) > 0 THEN 1 ELSE 0 END) AS Defect,
        MAX(ISNULL(m.[QtyHour], 0)) AS DailyPlan
FROM    src s
LEFT JOIN [dbo].[MasterData] m ON m.[Product_Id] = s.[Product_Id] AND m.[MachineCode] = @mc
GROUP BY s.JamMulai, m.[ProductName], s.[Product_Id]
ORDER BY s.JamMulai, Model;",
                c =>
                {
                    c.Parameters.AddWithValue("@mc", MachineCode);
                    c.Parameters.AddWithValue("@dari", dari);
                    c.Parameters.AddWithValue("@sampai", sampai);
                },
                r => new ProdRow
                {
                    Jam = Convert.ToDateTime(r["JamMulai"]),
                    Model = Convert.ToString(r["Model"]),
                    SeriAwal = Convert.ToString(r["SeriAwal"]),
                    SeriAkhir = Convert.ToString(r["SeriAkhir"]),
                    DailyActual = Convert.ToInt32(r["DailyActual"]),
                    Defect = Convert.ToInt32(r["Defect"]),
                    DailyPlan = Convert.ToInt32(r["DailyPlan"])
                });

            // ACCM = akumulasi DAILY per model, urut jam
            var accA = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            var accP = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (var p in raw.OrderBy(x => x.Jam).ThenBy(x => x.Model))
            {
                var k = p.Model ?? "-";
                accA[k] = (accA.TryGetValue(k, out var a) ? a : 0) + p.DailyActual;
                accP[k] = (accP.TryGetValue(k, out var b) ? b : 0) + p.DailyPlan;
                p.AccmActual = accA[k];
                p.AccmPlan = accP[k];
            }
            Prods = raw;
        }

        // =============================================================================
        // HELPER ADO.NET
        // =============================================================================
        private async Task<List<T>> QueryAsync<T>(string sql, Action<SqlCommand> bind, Func<SqlDataReader, T> map)
        {
            var list = new List<T>();
            using var cn = new SqlConnection(ConnStr);
            using var cmd = new SqlCommand(sql, cn) { CommandTimeout = 60 };
            bind?.Invoke(cmd);
            await cn.OpenAsync();
            using var rd = await cmd.ExecuteReaderAsync();
            while (await rd.ReadAsync()) list.Add(map(rd));
            return list;
        }

        private async Task ExecAsync(string sql, Action<SqlCommand> bind)
        {
            using var cn = new SqlConnection(ConnStr);
            using var cmd = new SqlCommand(sql, cn) { CommandTimeout = 60 };
            bind?.Invoke(cmd);
            await cn.OpenAsync();
            await cmd.ExecuteNonQueryAsync();
        }

        // =============================================================================
        // HELPER TAMPILAN (dipanggil dari .cshtml)
        // =============================================================================
        public IEnumerable<ReasonItem> ReasonsOf(string kategori) =>
            Reasons.Where(r => r.Kategori == kategori);

        public List<string> JamRows()
        {
            var jam = Losses.Select(l => l.Jam).Distinct().ToList();
            while (jam.Count < 13) jam.Add("");
            return jam;
        }

        public LossRow BelumAdaReason(string jam) =>
            string.IsNullOrEmpty(jam) ? null : Losses.FirstOrDefault(l => l.Jam == jam && !l.ReasonId.HasValue);

        public int Cell(string jam, int reasonId) => string.IsNullOrEmpty(jam) ? 0 :
            KeMenit(Losses.Where(l => l.Jam == jam && l.ReasonId == reasonId).Sum(l => l.Seconds));

        public int RowTotal(string jam) => string.IsNullOrEmpty(jam) ? 0 :
            KeMenit(Losses.Where(l => l.Jam == jam).Sum(l => l.Seconds));

        public int ColTotal(int reasonId) =>
            KeMenit(Losses.Where(l => l.ReasonId == reasonId).Sum(l => l.Seconds));

        /// <summary>Nol ditampilkan sebagai sel kosong, seperti form aslinya.</summary>
        public string N(int v) => v == 0 ? "" : v.ToString();
    }
}
