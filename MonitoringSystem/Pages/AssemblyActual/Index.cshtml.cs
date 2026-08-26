using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Data.SqlClient;
using MonitoringSystem.Services;
using System.Data;

namespace MonitoringSystem.Pages.AssemblyActual;

public class IndexModel : PageModel
{
    public const int MinimumCycleTimeSeconds = 14;

    private static readonly HashSet<string> AllowedMachineCodes =
        new(StringComparer.OrdinalIgnoreCase) { "MCH1-01", "MCH1-02" };

    private readonly IConfiguration _configuration;
    private readonly ILogger<IndexModel> _logger;
    private readonly BreakTimeService _breakTimeService;
    private List<BreakTimeInfo> _breakTimes = [];

    public IndexModel(
        IConfiguration configuration,
        ILogger<IndexModel> logger,
        BreakTimeService breakTimeService)
    {
        _configuration = configuration;
        _logger = logger;
        _breakTimeService = breakTimeService;
    }

    [BindProperty(SupportsGet = true)]
    public string MachineCode { get; set; } = "MCH1-02";

    [BindProperty(SupportsGet = true)]
    public DateTime SelectedDate { get; set; } = DateTime.Today;

    public List<ScanRow> ScanRows { get; private set; } = [];
    public List<HourlyRow> HourlyRows { get; private set; } = [];
    public List<CycleChartPoint> CycleChartPoints { get; private set; } = [];
    public List<EfficiencyChartPoint> EfficiencyChartPoints { get; private set; } = [];
    public string ErrorMessage { get; private set; } = string.Empty;

    public string LineName => MachineCode switch
    {
        "MCH1-01" => "CU",
        "MCH1-02" => "CS",
        _ => MachineCode
    };

    public int ModelCount => ScanRows
        .Select(row => row.ModelProduk)
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .Count();

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        NormalizeFilters();
        await LoadScanDataAsync(cancellationToken);
        await LoadBreakTimesAsync();
        BuildHourlyRows();
        BuildChartData();
    }

    private void NormalizeFilters()
    {
        if (!AllowedMachineCodes.Contains(MachineCode))
        {
            MachineCode = "MCH1-02";
        }

        if (SelectedDate == default)
        {
            SelectedDate = DateTime.Today;
        }

        SelectedDate = SelectedDate.Date;
    }

    private async Task LoadScanDataAsync(CancellationToken cancellationToken)
    {
        var connectionString = _configuration.GetConnectionString("DefaultConnection");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            ErrorMessage = "Koneksi database PROMOSYS belum dikonfigurasi.";
            return;
        }

        const string query = """
            ;WITH ScanData AS
            (
                SELECT
                    o.MachineCode,
                    o.SN_GOOD AS SerialNumber,
                    o.Product_Id,
                    ISNULL(m.ProductName, o.Product_Id) AS ModelProduk,
                    ISNULL(m.SUT, 0) AS PlanCycleTime,
                    o.[Date] AS WaktuScan,
                    LAG(o.[Date]) OVER (
                        PARTITION BY o.MachineCode
                        ORDER BY o.[Date]
                    ) AS WaktuScanSebelumnya,
                    LAG(o.Product_Id) OVER (
                        PARTITION BY o.MachineCode
                        ORDER BY o.[Date]
                    ) AS ModelSebelumnya
                FROM PROMOSYS.dbo.OEESN o
                LEFT JOIN PROMOSYS.dbo.MasterData m
                    ON o.Product_Id = m.Product_Id
                WHERE
                    o.MachineCode = @MachineCode
                    AND o.SN_GOOD IS NOT NULL
                    AND LTRIM(RTRIM(o.SN_GOOD)) <> ''
                    AND o.[Date] >= @StartDate
                    AND o.[Date] < @EndDate
            )
            SELECT
                CASE
                    WHEN MachineCode = 'MCH1-01' THEN 'CU'
                    WHEN MachineCode = 'MCH1-02' THEN 'CS'
                    ELSE MachineCode
                END AS MachineLine,
                SerialNumber,
                Product_Id,
                ModelProduk,
                PlanCycleTime,
                WaktuScan,
                WaktuScanSebelumnya,
                CASE
                    WHEN Product_Id = ModelSebelumnya
                    THEN DATEDIFF(SECOND, WaktuScanSebelumnya, WaktuScan)
                    ELSE NULL
                END AS CycleTime
            FROM ScanData
            ORDER BY WaktuScan DESC;
            """;

        try
        {
            await using var connection = new SqlConnection(connectionString);
            await connection.OpenAsync(cancellationToken);

            await using var command = new SqlCommand(query, connection)
            {
                CommandTimeout = 60
            };
            command.Parameters.Add("@MachineCode", SqlDbType.VarChar, 50).Value = MachineCode;
            command.Parameters.Add("@StartDate", SqlDbType.DateTime).Value = SelectedDate;
            command.Parameters.Add("@EndDate", SqlDbType.DateTime).Value = SelectedDate.AddDays(1);

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                ScanRows.Add(new ScanRow
                {
                    MachineLine = reader.GetString(reader.GetOrdinal("MachineLine")),
                    SerialNumber = reader.GetString(reader.GetOrdinal("SerialNumber")),
                    ProductId = reader.IsDBNull(reader.GetOrdinal("Product_Id"))
                        ? string.Empty
                        : reader.GetString(reader.GetOrdinal("Product_Id")),
                    ModelProduk = reader.IsDBNull(reader.GetOrdinal("ModelProduk"))
                        ? "-"
                        : reader.GetString(reader.GetOrdinal("ModelProduk")),
                    PlanCycleTime = reader.IsDBNull(reader.GetOrdinal("PlanCycleTime"))
                        ? 0
                        : reader.GetInt32(reader.GetOrdinal("PlanCycleTime")),
                    WaktuScan = reader.GetDateTime(reader.GetOrdinal("WaktuScan")),
                    WaktuScanSebelumnya = reader.IsDBNull(reader.GetOrdinal("WaktuScanSebelumnya"))
                        ? null
                        : reader.GetDateTime(reader.GetOrdinal("WaktuScanSebelumnya")),
                    CycleTime = reader.IsDBNull(reader.GetOrdinal("CycleTime"))
                        ? null
                        : reader.GetInt32(reader.GetOrdinal("CycleTime"))
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Gagal mengambil Assembly Actual untuk {MachineCode} pada {SelectedDate}",
                MachineCode,
                SelectedDate);
            ErrorMessage = "Data Assembly Actual belum dapat dimuat. Silakan coba kembali.";
        }
    }

    private async Task LoadBreakTimesAsync()
    {
        var breakTimes = new List<BreakTimeInfo>();

        try
        {
            breakTimes.AddRange(await _breakTimeService.GetBreakTimesForDateAsync(SelectedDate));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "Gagal mengambil additional break time untuk Assembly Actual pada {SelectedDate}",
                SelectedDate);
        }

        var connectionString = _configuration.GetConnectionString("DefaultConnection");
        if (!string.IsNullOrWhiteSpace(connectionString))
        {
            try
            {
                await using var connection = new SqlConnection(connectionString);
                await connection.OpenAsync();

                const string query = """
                    SELECT StartTime, EndTime
                    FROM RestTime
                    WHERE DayType = @DayType;
                    """;
                await using var command = new SqlCommand(query, connection);
                command.Parameters.Add("@DayType", SqlDbType.VarChar, 20).Value =
                    DetermineDayType(SelectedDate.DayOfWeek);

                await using var reader = await command.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    breakTimes.Add(new BreakTimeInfo
                    {
                        StartTime = reader.GetTimeSpan(0),
                        EndTime = reader.GetTimeSpan(1),
                        Reason = "Rest Time"
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    "Gagal mengambil RestTime Performance untuk {SelectedDate}",
                    SelectedDate);
            }
        }

        _breakTimes = breakTimes
            .GroupBy(item => new { item.StartTime, item.EndTime })
            .Select(group => group.First())
            .OrderBy(item => item.StartTime)
            .ToList();
    }

    private static string DetermineDayType(DayOfWeek dayOfWeek) => dayOfWeek switch
    {
        DayOfWeek.Monday or DayOfWeek.Tuesday or DayOfWeek.Wednesday or DayOfWeek.Thursday => "REGULAR",
        DayOfWeek.Friday => "FRIDAY",
        DayOfWeek.Saturday or DayOfWeek.Sunday => "WEEKEND",
        _ => "REGULAR"
    };

    private void BuildHourlyRows()
    {
        HourlyRows = ScanRows
            .GroupBy(row => new
            {
                HourStart = new DateTime(
                    row.WaktuScan.Year,
                    row.WaktuScan.Month,
                    row.WaktuScan.Day,
                    row.WaktuScan.Hour,
                    0,
                    0),
                row.ProductId,
                row.ModelProduk
            })
            .Select(group =>
            {
                var validCycleTimes = group
                    .Select(GetCycleTimeWithoutBreak)
                    .Where(cycleTime => cycleTime.HasValue && cycleTime.Value > 0)
                    .Select(cycleTime => cycleTime!.Value)
                    .ToList();

                return new HourlyRow
                {
                    HourStart = group.Key.HourStart,
                    ProductId = group.Key.ProductId,
                    ModelProduk = group.Key.ModelProduk,
                    Actual = group.Count(),
                    FastestCycleTime = validCycleTimes.Count > 0 ? validCycleTimes.Min() : null,
                    AverageCycleTime = validCycleTimes.Count > 0
                        ? Math.Round(validCycleTimes.Average(), 1)
                        : null,
                    SlowestCycleTime = validCycleTimes.Count > 0 ? validCycleTimes.Max() : null
                };
            })
            .OrderByDescending(row => row.HourStart)
            .ThenBy(row => row.ModelProduk)
            .ToList();
    }

    private int? GetCycleTimeWithoutBreak(ScanRow row)
    {
        if (!row.CycleTime.HasValue || !row.WaktuScanSebelumnya.HasValue)
        {
            return null;
        }

        var intervalStart = row.WaktuScanSebelumnya.Value;
        var intervalEnd = row.WaktuScan;
        var breakOverlapSeconds = 0;

        foreach (var breakTime in _breakTimes)
        {
            var breakStart = SelectedDate.Add(breakTime.StartTime);
            var breakEnd = SelectedDate.Add(breakTime.EndTime);

            if (breakEnd <= breakStart)
            {
                breakEnd = breakEnd.AddDays(1);
            }

            // Jika dua scan sama-sama terjadi di dalam jadwal break, line ternyata
            // tetap berproduksi; pertahankan interval scan aktual tersebut.
            if (intervalStart >= breakStart && intervalEnd <= breakEnd)
            {
                continue;
            }

            if (intervalStart < breakEnd && intervalEnd > breakStart)
            {
                var overlapStart = intervalStart > breakStart ? intervalStart : breakStart;
                var overlapEnd = intervalEnd < breakEnd ? intervalEnd : breakEnd;
                breakOverlapSeconds += (int)(overlapEnd - overlapStart).TotalSeconds;
            }
        }

        var cycleTimeWithoutBreak = row.CycleTime.Value - breakOverlapSeconds;
        if (cycleTimeWithoutBreak <= 0)
        {
            return null;
        }

        return Math.Max(MinimumCycleTimeSeconds, cycleTimeWithoutBreak);
    }

    private void BuildChartData()
    {
        var orderedScans = ScanRows
            .OrderBy(row => row.WaktuScan)
            .ToList();

        CycleChartPoints = orderedScans
            .Select(row => new
            {
                Row = row,
                ActualCycleTime = GetCycleTimeWithoutBreak(row)
            })
            .Where(item => item.ActualCycleTime.HasValue && item.Row.PlanCycleTime > 0)
            .Select(item => new CycleChartPoint
            {
                Label = item.Row.WaktuScan.ToString("HH:mm:ss"),
                ModelProduk = item.Row.ModelProduk,
                PlanCycleTime = item.Row.PlanCycleTime,
                ActualCycleTime = item.ActualCycleTime!.Value
            })
            .ToList();

        BuildEfficiencyChart(orderedScans);
    }

    private void BuildEfficiencyChart(List<ScanRow> orderedScans)
    {
        var shiftStart = SelectedDate.AddHours(7);
        var shiftEnd = SelectedDate.AddHours(23).AddMinutes(15);
        var effectiveEnd = SelectedDate == DateTime.Today
            ? (DateTime.Now < shiftEnd ? DateTime.Now : shiftEnd)
            : shiftEnd;

        EfficiencyChartPoints.Add(new EfficiencyChartPoint
        {
            Label = "07:00",
            Efficiency = 0
        });

        if (effectiveEnd <= shiftStart)
        {
            return;
        }

        var netWorkingSeconds = GetNetWorkingSeconds(shiftStart, effectiveEnd);
        if (netWorkingSeconds <= 0)
        {
            return;
        }

        var latestPlanCycleTime = orderedScans
            .LastOrDefault(row => row.PlanCycleTime > 0)?.PlanCycleTime ?? 0;
        if (latestPlanCycleTime <= 0)
        {
            return;
        }

        var lastHour = SelectedDate == DateTime.Today
            ? Math.Min(24, DateTime.Now.Hour + 1)
            : Math.Min(24, Math.Max(8, orderedScans.LastOrDefault()?.WaktuScan.Hour + 1 ?? 8));

        for (var hour = 8; hour <= lastHour; hour++)
        {
            var hourEnd = SelectedDate.AddHours(hour);
            var cumulativeActual = orderedScans.Count(row =>
                row.WaktuScan >= shiftStart && row.WaktuScan < hourEnd);
            var efficiency = cumulativeActual > 0
                ? Math.Round(Math.Min(
                    cumulativeActual * latestPlanCycleTime / netWorkingSeconds * 100.0,
                    120), 2)
                : 0;

            EfficiencyChartPoints.Add(new EfficiencyChartPoint
            {
                Label = $"{hour % 24:D2}:00",
                Efficiency = efficiency
            });
        }
    }

    private double GetNetWorkingSeconds(DateTime intervalStart, DateTime intervalEnd)
    {
        var breakOverlapSeconds = 0.0;
        foreach (var breakTime in _breakTimes)
        {
            var breakStart = SelectedDate.Add(breakTime.StartTime);
            var breakEnd = SelectedDate.Add(breakTime.EndTime);
            if (breakEnd <= breakStart)
            {
                breakEnd = breakEnd.AddDays(1);
            }

            if (intervalStart < breakEnd && intervalEnd > breakStart)
            {
                var overlapStart = intervalStart > breakStart ? intervalStart : breakStart;
                var overlapEnd = intervalEnd < breakEnd ? intervalEnd : breakEnd;
                breakOverlapSeconds += (overlapEnd - overlapStart).TotalSeconds;
            }
        }

        return Math.Max(0, (intervalEnd - intervalStart).TotalSeconds - breakOverlapSeconds);
    }

    public sealed class ScanRow
    {
        public string MachineLine { get; init; } = string.Empty;
        public string SerialNumber { get; init; } = string.Empty;
        public string ProductId { get; init; } = string.Empty;
        public string ModelProduk { get; init; } = string.Empty;
        public int PlanCycleTime { get; init; }
        public DateTime WaktuScan { get; init; }
        public DateTime? WaktuScanSebelumnya { get; init; }
        public int? CycleTime { get; init; }
    }

    public sealed class HourlyRow
    {
        public DateTime HourStart { get; init; }
        public string ProductId { get; init; } = string.Empty;
        public string ModelProduk { get; init; } = string.Empty;
        public int Actual { get; init; }
        public int? FastestCycleTime { get; init; }
        public double? AverageCycleTime { get; init; }
        public int? SlowestCycleTime { get; init; }

        public string TimeRange => $"{HourStart:HH}:00 - {HourStart.AddHours(1):HH}:00";
    }

    public sealed class CycleChartPoint
    {
        public string Label { get; init; } = string.Empty;
        public string ModelProduk { get; init; } = string.Empty;
        public int PlanCycleTime { get; init; }
        public int ActualCycleTime { get; init; }
    }

    public sealed class EfficiencyChartPoint
    {
        public string Label { get; init; } = string.Empty;
        public double Efficiency { get; init; }
    }
}
