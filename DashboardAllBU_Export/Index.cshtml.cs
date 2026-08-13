using Dapper;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using System.Collections.Generic;
using System.Threading.Tasks;
using MonitoringSystem.Models;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Logging;
using System.Linq;
using System;

namespace MonitoringSystem.Pages.DashboardAllBu
{
public class IndexModel : PageModel
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<IndexModel> _logger;

    public IndexModel(IConfiguration configuration, ILogger<IndexModel> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    public void OnGet()
    {
    }

    // ── Break time AC (Regular) ──
    private static readonly List<BreakTime> RegularDayBreakTimes = new List<BreakTime>
    {
        new BreakTime { Start = new TimeSpan(09, 30, 0), End = new TimeSpan(09, 35, 0) },
        new BreakTime { Start = new TimeSpan(12, 15, 0), End = new TimeSpan(13, 0,  0) },
        new BreakTime { Start = new TimeSpan(14, 30, 0), End = new TimeSpan(14, 35, 0) }
    };

    // ── Break time AC (Jumat) ──
    private static readonly List<BreakTime> FridayBreakTimesAC = new List<BreakTime>
    {
        new BreakTime { Start = new TimeSpan(09, 30, 0), End = new TimeSpan(09, 35, 0) },
        new BreakTime { Start = new TimeSpan(11, 50, 0), End = new TimeSpan(13, 15, 0) },
        new BreakTime { Start = new TimeSpan(14, 30, 0), End = new TimeSpan(14, 35, 0) }
    };

    // ── Break time LS/WP (Regular) ──
    private static readonly List<(TimeSpan Start, TimeSpan End)> LSRegularBreakTimes = new()
    {
        (new TimeSpan(9,  30, 0), new TimeSpan(9,  35, 0)),
        (new TimeSpan(11, 40, 0), new TimeSpan(12, 25, 0)),
        (new TimeSpan(14, 30, 0), new TimeSpan(14, 35, 0))
    };

    // ── Break time LS/WP (Jumat) ──
    private static readonly List<(TimeSpan Start, TimeSpan End)> LSFridayBreakTimes = new()
    {
        (new TimeSpan(9,  30, 0), new TimeSpan(9,  35, 0)),
        (new TimeSpan(11, 50, 0), new TimeSpan(13, 15, 0)),
        (new TimeSpan(14, 30, 0), new TimeSpan(14, 35, 0))
    };

    // ════════════════════════════════════════════════════════════
    // UNIFIED DATA
    // ════════════════════════════════════════════════════════════
    [HttpGet]
    public async Task<IActionResult> OnGetUnifiedData()
    {
        var viewModel = new UnifiedDashboardViewModel
        {
            DataAC = new ACDataModel { LineCU = new LineData(), LineCS = new LineData() },
            DataAudio = new Dictionary<string, LineData>(),
            DataLS = new LSDataModel { Line1 = new LineData(), Line2 = new LineData() },
            DataFan = new Dictionary<string, LineData>(),
            DataWP = new Dictionary<string, LineData>(),
            DataRef = new LineData()
        };

        // ── 1. DATA BU AC (CU & CS) ──
        try
        {
            using var conn = new SqlConnection(_configuration.GetConnectionString("DefaultConnection"));
            viewModel.DataAC.LineCU = await GetACLineData(conn, "MCH1-01");
            viewModel.DataAC.LineCS = await GetACLineData(conn, "MCH1-02");
        }
        catch (Exception ex) { _logger.LogWarning(ex, "Data AC gagal"); }

        // ── 2. DATA BU AUDIO (Total semua line dijumlah jadi 1) ──
        try
        {
            using var conn = new SqlConnection(_configuration.GetConnectionString("AUDConnection"));
            await conn.OpenAsync();

            int totalActualAudio = 0, totalPlanAudio = 0, dailyPlanAudio = 0;
            for (int i = 1; i <= 11; i++)
            {
                try
                {
                    var sql = $@"
                        SELECT TOP 1 Actual, Target, DailyPlan
                        FROM dbo.FINAL{i}
                        WHERE CONVERT(date, DateTime) = CONVERT(date, GETDATE())
                        ORDER BY ID DESC";
                    var row = await conn.QuerySingleOrDefaultAsync<dynamic>(sql);
                    if (row != null)
                    {
                        totalActualAudio += row.Actual != null ? (int)row.Actual : 0;
                        totalPlanAudio += row.Target != null ? (int)row.Target : 0;
                        dailyPlanAudio += row.DailyPlan != null ? (int)row.DailyPlan : 0;
                    }
                }
                catch (Exception ex) { _logger.LogWarning(ex, "Query FINAL{Index} Audio gagal", i); }
            }

            var defectsAudio = new List<object>();
            try
            {
                var defectQuery = @"
                    SELECT Station AS Category, SUM(Quantity) AS Count
                    FROM dbo.DEFECTT
                    WHERE CONVERT(date, DateTime) = CONVERT(date, GETDATE())
                    GROUP BY Station
                    ORDER BY SUM(Quantity) DESC";
                var defects = await conn.QueryAsync<dynamic>(defectQuery);
                defectsAudio = defects.Select(d => (object)new
                {
                    category = (string)d.Category,
                    count = (int)d.Count
                }).ToList();
            }
            catch (Exception ex) { _logger.LogWarning(ex, "Query Defect Audio gagal"); }

            var lossTimeAudio = await GetAudioLossTimeAsync(conn);

            viewModel.DataAudio.Add("total", new LineData
            {
                TotalActual = totalActualAudio,
                TotalPlan = totalPlanAudio,
                DailyPlan = dailyPlanAudio,
                DefectsByCategory = defectsAudio,
                LossData = lossTimeAudio
            });
        }
        catch (Exception ex) { _logger.LogWarning(ex, "Data Audio gagal"); }

        // ── 3. DATA BU LS (Line1 = 2T, Line2 = SKD) ──
        try
        {
            using var conn = new SqlConnection(_configuration.GetConnectionString("LSConnection"));
            conn.Open();

            var lsBreakTimes = DateTime.Now.DayOfWeek == DayOfWeek.Friday
                ? LSFridayBreakTimes
                : LSRegularBreakTimes;

            var lsWorkStart = new TimeSpan(7, 7, 0);
            var lsWorkEnd = new TimeSpan(15, 55, 0);
            var lsNow = DateTime.Now.TimeOfDay;
            int lsWorkingTime = 0;

            if (lsNow > lsWorkStart)
            {
                var effectiveEnd = lsNow > lsWorkEnd ? lsWorkEnd : lsNow;
                int totalMin = (int)(effectiveEnd - lsWorkStart).TotalMinutes;
                int totalRest = lsBreakTimes.Sum(b =>
                {
                    if (effectiveEnd <= b.Start || lsWorkStart >= b.End) return 0;
                    var rs = b.Start > lsWorkStart ? b.Start : lsWorkStart;
                    var re = b.End < effectiveEnd ? b.End : effectiveEnd;
                    return (int)Math.Max(0, (re - rs).TotalMinutes);
                });
                lsWorkingTime = Math.Max(0, totalMin - totalRest);
            }

            var defects2T = new List<object>();
            var defectsSKD = new List<object>();
            try
            {
                using var connDefect = new SqlConnection(_configuration.GetConnectionString("LSConnection"));
                connDefect.Open();

                using (var cmd2T = new SqlCommand(@"
                    SELECT dn.DefectName, COUNT(*) AS Count
                    FROM dbo.Defect_Results dr
                    JOIN dbo.Defect_Names dn ON dr.DefectId = dn.Id
                    WHERE CONVERT(date, dr.DateTime) = CONVERT(date, GETDATE())
                      AND dr.LocationId != 10
                    GROUP BY dn.DefectName
                    ORDER BY Count DESC", connDefect))
                {
                    cmd2T.CommandTimeout = 30;
                    using var r = cmd2T.ExecuteReader();
                    while (r.Read())
                        defects2T.Add(new { category = r["DefectName"].ToString(), count = Convert.ToInt32(r["Count"]) });
                }

                using (var cmdSKD = new SqlCommand(@"
                    SELECT dn.DefectName, COUNT(*) AS Count
                    FROM dbo.Defect_Results dr
                    JOIN dbo.Defect_Names dn ON dr.DefectId = dn.Id
                    WHERE CONVERT(date, dr.DateTime) = CONVERT(date, GETDATE())
                      AND dr.LocationId = 10
                    GROUP BY dn.DefectName
                    ORDER BY Count DESC", connDefect))
                {
                    cmdSKD.CommandTimeout = 30;
                    using var r = cmdSKD.ExecuteReader();
                    while (r.Read())
                        defectsSKD.Add(new { category = r["DefectName"].ToString(), count = Convert.ToInt32(r["Count"]) });
                }
            }
            catch (Exception ex) { _logger.LogWarning(ex, "Query Defect LS gagal"); }

            var (lossTime2T, loadTime2T, hourlyEvents2T) = LS_GetAssemblyLossTime(conn, lsBreakTimes, lsWorkingTime, "2T");
            var (lossTimeSKD, loadTimeSKD, hourlyEventsSKD) = LS_GetAssemblyLossTime(conn, lsBreakTimes, lsWorkingTime, "SKD");

            var line1 = new LineData();
            try
            {
                using var cmd = new SqlCommand(@"
                    SELECT TOP 1 DailyPlan, Target, Actual
                    FROM dbo.FINAL1
                    WHERE CONVERT(date, Date) = CONVERT(date, GETDATE())
                    ORDER BY Date DESC", conn);
                using var reader = cmd.ExecuteReader();
                if (reader.Read())
                {
                    line1.TotalActual = reader["Actual"] != DBNull.Value ? Convert.ToInt32(reader["Actual"]) : 0;
                    line1.TotalPlan = reader["Target"] != DBNull.Value ? Convert.ToInt32(reader["Target"]) : 0;
                    line1.DailyPlan = reader["DailyPlan"] != DBNull.Value ? Convert.ToInt32(reader["DailyPlan"]) : 0;
                }
            }
            catch (Exception ex) { _logger.LogWarning(ex, "Query FINAL1 (2T) gagal"); }

            var line2 = new LineData();
            try
            {
                using var connSKD = new SqlConnection(_configuration.GetConnectionString("LSConnection"));
                connSKD.Open();

                using (var cmdActual = new SqlCommand(@"
                    SELECT COUNT(*) AS Actual
                    FROM [dbo].[Production_Results]
                    WHERE CONVERT(date, ScanningDate) = CONVERT(date, GETDATE())", connSKD))
                {
                    cmdActual.CommandTimeout = 30;
                    var result = cmdActual.ExecuteScalar();
                    line2.TotalActual = (result != null && result != DBNull.Value) ? Convert.ToInt32(result) : 0;
                }

                if (line2.TotalActual > 0)
                    line2.TotalPlan = LS_CalculateSKDTargetByModelSegment(connSKD, lsBreakTimes);
            }
            catch (Exception ex) { _logger.LogWarning(ex, "Query SKD (Actual/Target) gagal"); }

            line2.DailyPlan = LS_CalculateSKDDailyPlan(conn);

            line1.DefectsByCategory = defects2T;
            line2.DefectsByCategory = defectsSKD;

            line1.LossData = new LossTimeData
            {
                WorkingTime = lsWorkingTime,
                LossTime = lossTime2T,
                LoadTime = loadTime2T,
                HourlyEvents = hourlyEvents2T.ToDictionary(kv => kv.Key, kv => kv.Value.OfType<LossEvent>().ToList()),
                BreakTimes = lsBreakTimes.Select(b => new BreakTime { Start = b.Start, End = b.End }).ToList()
            };

            line2.LossData = new LossTimeData
            {
                WorkingTime = lsWorkingTime,
                LossTime = lossTimeSKD,
                LoadTime = loadTimeSKD,
                HourlyEvents = hourlyEventsSKD.ToDictionary(kv => kv.Key, kv => kv.Value.OfType<LossEvent>().ToList()),
                BreakTimes = lsBreakTimes.Select(b => new BreakTime { Start = b.Start, End = b.End }).ToList()
            };

            viewModel.DataLS.Line1 = line1;
            viewModel.DataLS.Line2 = line2;
        }
        catch (Exception ex) { _logger.LogWarning(ex, "Data LS gagal"); }

        // ── 4. DATA BU FAN (Line 1–7) ──
        try
        {
            using var conn = new SqlConnection(_configuration.GetConnectionString("FanConnection"));
            int totalActualFan = 0, totalPlanFan = 0, dailyPlanFan = 0;

            for (int i = 1; i <= 7; i++)
            {
                try
                {
                    string query = $"SELECT TOP 1 DailyPlan, Target AS TotalPlan, Actual AS TotalActual FROM dbo.FINAL{i} ORDER BY ID DESC";
                    var data = await conn.QueryFirstOrDefaultAsync<LineData>(query);
                    if (data != null)
                    {
                        totalActualFan += data.TotalActual;
                        totalPlanFan += data.TotalPlan;
                        dailyPlanFan += data.DailyPlan;
                    }
                }
                catch (Exception ex) { _logger.LogWarning(ex, "Query FINAL{Index} Fan gagal", i); }
            }

            viewModel.DataFan.Add("total", new LineData
            {
                TotalActual = totalActualFan,
                TotalPlan = totalPlanFan,
                DailyPlan = dailyPlanFan
            });
        }
        catch (Exception ex) { _logger.LogWarning(ex, "Data Fan gagal"); }

        // ── 5. DATA BU WP (Total semua Line1-Line8 dijumlah jadi 1) ──
        try
        {
            using var conn = new SqlConnection(_configuration.GetConnectionString("WPConnection"));
            await conn.OpenAsync();

            int totalActualWP = 0, totalPlanWP = 0, dailyPlanWP = 0;

            var sqlWP = @"
                WITH LatestPerLine AS (
                    SELECT
                        MachineCode, TargetUnit, TotalUnit,
                        ROW_NUMBER() OVER (PARTITION BY MachineCode ORDER BY SDate DESC) AS rn
                    FROM [dbo].[OEESN]
                    WHERE CONVERT(date, SDate) = CONVERT(date, GETDATE())
                      AND MachineCode IN ('Line1','Line2','Line3','Line4',
                                          'Line5','Line6','Line7','Line8')
                )
                SELECT
                    ISNULL(SUM(TotalUnit),  0) AS TotalActual,
                    ISNULL(SUM(TargetUnit), 0) AS TotalPlan
                FROM LatestPerLine WHERE rn = 1";

            var rowWP = await conn.QuerySingleOrDefaultAsync<dynamic>(sqlWP);
            if (rowWP != null)
            {
                totalActualWP = (int)(rowWP.TotalActual ?? 0);
                totalPlanWP = (int)(rowWP.TotalPlan ?? 0);
            }

            var dailyPlanSql = @"
    SELECT ISNULL(SUM(pr.Quantity), 0)
    FROM [dbo].[ProductionRecords] pr
    INNER JOIN [dbo].[ProductionPlan] pp ON pr.PlanId = pp.Id
    WHERE pr.MachineCode IN (
        'Line1','Line2','Line3','Line4',
        'Line5','Line6','Line7','Line8'
    )
    AND CONVERT(date, pp.CurrentDate) = CONVERT(date, GETDATE())";
            dailyPlanWP = await conn.ExecuteScalarAsync<int>(dailyPlanSql);

            var wpLossData = await GetWPLossTimeAsync(conn);

            viewModel.DataWP.Add("total", new LineData
            {
                TotalActual = totalActualWP,
                TotalPlan = totalPlanWP,
                DailyPlan = dailyPlanWP,
                LossData = wpLossData
            });
        }
        catch (Exception ex) { _logger.LogWarning(ex, "Data WP gagal"); }

        // ── DUMMY FALLBACK — sementara sampai data asli LS/Ref/Fan/WP siap ──
        ApplyDummyIfEmpty(viewModel.DataLS.Line1, 500, 420, 500, "Laundry 2T");
        ApplyDummyIfEmpty(viewModel.DataLS.Line2, 480, 410, 480, "Laundry SKD");
        ApplyDummyIfEmpty(viewModel.DataRef, 600, 550, 600, "Refrigerator");

        if (!viewModel.DataFan.ContainsKey("total")) viewModel.DataFan["total"] = new LineData();
        ApplyDummyIfEmpty(viewModel.DataFan["total"], 700, 640, 700, "Fan");

        if (!viewModel.DataWP.ContainsKey("total")) viewModel.DataWP["total"] = new LineData();
        ApplyDummyIfEmpty(viewModel.DataWP["total"], 550, 500, 550, "Water Pump");

        return new JsonResult(viewModel);
    }

    //cek dummy
    private void ApplyDummyIfEmpty(LineData line, int dummyPlan, int dummyActual, int dummyDailyPlan, string tag)
    {
        if (line == null) return;
        if (line.TotalPlan == 0 && line.TotalActual == 0 && line.DailyPlan == 0)
        {
            line.TotalPlan = dummyPlan;
            line.TotalActual = dummyActual;
            line.DailyPlan = dummyDailyPlan;
            line.IsDummy = true;
            _logger.LogInformation("Data {Tag} masih kosong, menggunakan dummy data sementara", tag);
        }
    }

    // ════════════════════════════════════════════════════════════
    // MACHINE NAMES
    // ════════════════════════════════════════════════════════════
    [HttpGet]
    public IActionResult OnGetMachineNamesFromDB([FromQuery] string bu = "ls")
    {
        try
        {
            if (bu.Equals("wp", StringComparison.OrdinalIgnoreCase))
            {
                var connStr = _configuration.GetConnectionString("WPConnection");
                if (string.IsNullOrEmpty(connStr))
                    return StatusCode(500, new { error = "Connection string 'ConnectionWP' tidak ditemukan" });

                using var conn = new SqlConnection(connStr);
                try { conn.Open(); }
                catch (SqlException sqlEx)
                {
                    _logger.LogError(sqlEx, "Gagal koneksi ke DB WP Machine");
                    return StatusCode(500, new { error = "Gagal koneksi ke database" });
                }

                using var cmd = new SqlCommand(@"
                SELECT LOWER(TRIM([MachineName])) AS MachineName
                FROM [dbo].[MachineList]
                ORDER BY [IdMachine]", conn);
                cmd.CommandTimeout = 30;
                using var reader = cmd.ExecuteReader();

                var names = new List<string>();
                while (reader.Read())
                {
                    var name = reader["MachineName"]?.ToString();
                    if (!string.IsNullOrEmpty(name)) names.Add(name);
                }
                return new JsonResult(new { data = names });
            }
            else
            {
                var connStr = _configuration.GetConnectionString("LSConnection");
                if (string.IsNullOrEmpty(connStr))
                    return StatusCode(500, new { error = "Connection string 'ConnectionLS' tidak ditemukan" });

                using var conn = new SqlConnection(connStr);
                try { conn.Open(); }
                catch (SqlException sqlEx)
                {
                    _logger.LogError(sqlEx, "Gagal koneksi ke DB Machine");
                    return StatusCode(500, new { error = "Gagal koneksi ke database" });
                }

                using var cmd = new SqlCommand(@"
                SELECT DISTINCT LOWER(TRIM([MachineName])) AS MachineName
                FROM [dbo].[MachineEfficiency]
                ORDER BY MachineName", conn);
                cmd.CommandTimeout = 30;
                using var reader = cmd.ExecuteReader();

                var names = new List<string>();
                while (reader.Read())
                {
                    var name = reader["MachineName"]?.ToString();
                    if (!string.IsNullOrEmpty(name)) names.Add(name);
                }
                return new JsonResult(new { data = names });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GetMachineNamesFromDB gagal (bu={BU})", bu);
            return StatusCode(500, new { error = "Gagal memuat data mesin" });
        }
    }

    // ════════════════════════════════════════════════════════════
    // MACHINE EFFICIENCY
    // ════════════════════════════════════════════════════════════
    [HttpGet]
    public IActionResult OnGetMachineEfficiencyFromDB([FromQuery] string bu = "ls", [FromQuery] int? month = null, [FromQuery] int? year = null)
    {
        int m = month ?? DateTime.Now.Month;
        int y = year ?? DateTime.Now.Year;

        try
        {
            if (bu.Equals("wp", StringComparison.OrdinalIgnoreCase))
            {
                var connStr = _configuration.GetConnectionString("WPConnection");
                if (string.IsNullOrEmpty(connStr))
                    return StatusCode(500, new { error = "Connection string 'ConnectionWP' tidak ditemukan" });

                using var conn = new SqlConnection(connStr);
                try { conn.Open(); }
                catch (SqlException sqlEx)
                {
                    _logger.LogError(sqlEx, "Gagal koneksi ke DB WP Machine");
                    return StatusCode(500, new { error = "Gagal koneksi ke database", message = sqlEx.Message });
                }

                using var cmd = new SqlCommand(@"
                SELECT
                    ml.MachineName,
                    ROUND(AVG(CAST(e.OEE            AS FLOAT)), 2) AS OEE,
                    ROUND(AVG(CAST(e.OperatingRatio AS FLOAT)), 2) AS OperatingRatio,
                    ROUND(AVG(CAST(e.Ability        AS FLOAT)), 2) AS Ability,
                    ROUND(AVG(CAST(e.Quality        AS FLOAT)), 2) AS Quality,
                    ROUND(AVG(CAST(e.Achievement    AS FLOAT)), 2) AS Achievement
                FROM [dbo].[Efficiency] e
                JOIN [dbo].[MachineList] ml ON ml.IdMachine = e.IdMachine
                WHERE MONTH(e.[Date]) = @Month
                  AND YEAR(e.[Date])  = @Year
                GROUP BY ml.MachineName
                ORDER BY ml.MachineName", conn);
                cmd.Parameters.AddWithValue("@Month", m);
                cmd.Parameters.AddWithValue("@Year", y);
                cmd.CommandTimeout = 30;

                using var reader = cmd.ExecuteReader();
                var result = new List<object>();
                while (reader.Read())
                    result.Add(new
                    {
                        machineName = reader["MachineName"]?.ToString() ?? "-",
                        oEE = reader.IsDBNull(reader.GetOrdinal("OEE")) ? (double?)null : reader.GetDouble(reader.GetOrdinal("OEE")),
                        operatingRatio = reader.IsDBNull(reader.GetOrdinal("OperatingRatio")) ? (double?)null : reader.GetDouble(reader.GetOrdinal("OperatingRatio")),
                        ability = reader.IsDBNull(reader.GetOrdinal("Ability")) ? (double?)null : reader.GetDouble(reader.GetOrdinal("Ability")),
                        quality = reader.IsDBNull(reader.GetOrdinal("Quality")) ? (double?)null : reader.GetDouble(reader.GetOrdinal("Quality")),
                        achievement = reader.IsDBNull(reader.GetOrdinal("Achievement")) ? (double?)null : reader.GetDouble(reader.GetOrdinal("Achievement"))
                    });

                if (result.Count == 0)
                    return new JsonResult(new { warning = $"Tidak ada data untuk {m}/{y}", data = result, queriedMonth = m, queriedYear = y, rowCount = 0 });

                return new JsonResult(new { success = true, data = result, rowCount = result.Count, queriedMonth = m, queriedYear = y });
            }
            else
            {
                var connStr = _configuration.GetConnectionString("LSConnection");
                if (string.IsNullOrEmpty(connStr))
                    return StatusCode(500, new { error = "Connection string 'ConnectionLS' tidak ditemukan" });

                using var conn = new SqlConnection(connStr);
                try { conn.Open(); }
                catch (SqlException sqlEx)
                {
                    _logger.LogError(sqlEx, "Gagal koneksi ke DB Machine");
                    return StatusCode(500, new { error = "Gagal koneksi ke database" });
                }

                using var cmd = new SqlCommand(@"
                SELECT
                    LOWER(TRIM([MachineName]))                                    AS MachineName,
                    CAST(ROUND(AVG(CAST([OEE]            AS FLOAT)), 1) AS FLOAT) AS OEE,
                    CAST(ROUND(AVG(CAST([OperatingRatio] AS FLOAT)), 1) AS FLOAT) AS OperatingRatio,
                    CAST(ROUND(AVG(CAST([Ability]        AS FLOAT)), 1) AS FLOAT) AS Ability,
                    CAST(ROUND(AVG(CAST([Quality]        AS FLOAT)), 1) AS FLOAT) AS Quality
                FROM [dbo].[MachineEfficiency]
                WHERE MONTH([Date]) = @Month AND YEAR([Date]) = @Year
                GROUP BY LOWER(TRIM([MachineName]))
                ORDER BY MachineName", conn);
                cmd.Parameters.AddWithValue("@Month", m);
                cmd.Parameters.AddWithValue("@Year", y);
                cmd.CommandTimeout = 30;

                using var reader = cmd.ExecuteReader();
                var result = new List<object>();
                while (reader.Read())
                    result.Add(new
                    {
                        machineName = reader["MachineName"]?.ToString() ?? "-",
                        oEE = reader.IsDBNull(reader.GetOrdinal("OEE")) ? (double?)null : reader.GetDouble(reader.GetOrdinal("OEE")),
                        operatingRatio = reader.IsDBNull(reader.GetOrdinal("OperatingRatio")) ? (double?)null : reader.GetDouble(reader.GetOrdinal("OperatingRatio")),
                        ability = reader.IsDBNull(reader.GetOrdinal("Ability")) ? (double?)null : reader.GetDouble(reader.GetOrdinal("Ability")),
                        quality = reader.IsDBNull(reader.GetOrdinal("Quality")) ? (double?)null : reader.GetDouble(reader.GetOrdinal("Quality"))
                    });

                if (result.Count == 0)
                    return new JsonResult(new { warning = $"Tidak ada data untuk {m}/{y}", data = result });

                return new JsonResult(new { data = result, rowCount = result.Count });
            }
        }
        catch (SqlException sqlEx)
        {
            _logger.LogError(sqlEx, "SQL error di GetMachineEfficiencyFromDB (bu={BU})", bu);
            return StatusCode(500, new { error = "SQL error.", message = sqlEx.Message, number = sqlEx.Number });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GetMachineEfficiencyFromDB gagal (bu={BU})", bu);
            return StatusCode(500, new { error = "Gagal memuat data efisiensi mesin" });
        }
    }

    // ════════════════════════════════════════════════════════════
    // HELPER: AC LINE DATA
    // ════════════════════════════════════════════════════════════
    private async Task<LineData> GetACLineData(SqlConnection conn, string machineCode)
    {
        var result = new LineData();

        var oeeSql = @"
            DECLARE @ProdDate DATE = CAST(DATEADD(hour, -7, GETDATE()) AS DATE);
            DECLARE @ShiftStart DATETIME = DATEADD(hour, 7, CAST(@ProdDate AS DATETIME));
            DECLARE @ShiftEnd DATETIME = DATEADD(hour, 31, CAST(@ProdDate AS DATETIME));

            SELECT 
                ISNULL((SELECT TOP 1 TargetUnit FROM OEESN WHERE SDate >= @ShiftStart AND SDate < @ShiftEnd AND MachineCode = @Code ORDER BY ID DESC), 0) AS TotalPlan,
                (SELECT COUNT(*) FROM OEESN WHERE SDate >= @ShiftStart AND SDate < @ShiftEnd AND MachineCode = @Code) AS TotalActual";
        var oeeData = await conn.QueryFirstOrDefaultAsync<dynamic>(oeeSql, new { Code = machineCode });
        if (oeeData != null)
        {
            result.TotalPlan = (int)oeeData.TotalPlan;
            result.TotalActual = (int)oeeData.TotalActual;
        }

        var planSql = @"
            DECLARE @ProdDate DATE = CAST(DATEADD(hour, -7, GETDATE()) AS DATE);
            SELECT ISNULL(SUM(pr.Quantity), 0)
            FROM PROMOSYS.dbo.ProductionRecords pr
            INNER JOIN PROMOSYS.dbo.ProductionPlan pp ON pr.PlanId = pp.Id
            WHERE pr.MachineCode = @Code
              AND CONVERT(date, pp.CurrentDate) = @ProdDate";
        result.DailyPlan = await conn.ExecuteScalarAsync<int?>(planSql, new { Code = machineCode }) ?? 0;

        var defectSql = @"
            SELECT COUNT(*) FROM NG_RPTS
            WHERE CONVERT(date, Date) = CONVERT(date, GETDATE())
              AND MachineCode = @Code";
        int totalDefects = await conn.ExecuteScalarAsync<int?>(defectSql, new { Code = machineCode }) ?? 0;
        result.TotalDefects = totalDefects;

        result.QualityRate = result.TotalActual > 0
            ? Math.Max(0, 100.0 - ((double)totalDefects / result.TotalActual * 100.0))
            : 100.0;

        result.LossData = await GetACLossTimeDataAsync(conn, machineCode);
        return result;
    }

    // ════════════════════════════════════════════════════════════
    // HELPER: AC LOSS TIME
    // ════════════════════════════════════════════════════════════
    private async Task<LossTimeData> GetACLossTimeDataAsync(SqlConnection connection, string machineCode)
    {
        var data = new LossTimeData();
        var today = DateTime.Now;
        var breakTimes = today.DayOfWeek == DayOfWeek.Friday ? FridayBreakTimesAC : RegularDayBreakTimes;

        data.BreakTimes = breakTimes.Select(b => new BreakTime { Start = b.Start, End = b.End }).ToList();

        TimeSpan workDayStart = new TimeSpan(7, 7, 0);
        TimeSpan workDayEnd = new TimeSpan(15, 55, 0);
        var currentTime = today.TimeOfDay;

        if (currentTime > workDayStart)
        {
            var effectiveEnd = currentTime > workDayEnd ? workDayEnd : currentTime;
            int totalDuration = (int)(effectiveEnd - workDayStart).TotalMinutes;
            int totalRest = breakTimes.Sum(b =>
            {
                if (effectiveEnd <= b.Start || workDayStart >= b.End) return 0;
                var rs = b.Start > workDayStart ? b.Start : workDayStart;
                var re = b.End < effectiveEnd ? b.End : effectiveEnd;
                return (int)Math.Max(0, (re - rs).TotalMinutes);
            });
            data.WorkingTime = Math.Max(0, totalDuration - totalRest);
        }

        for (int h = 7; h < 16; h++)
            data.HourlyEvents[h] = new List<LossEvent>();

        var lossEventsQuery = @"
            SELECT
                DATEPART(hour,   Time) AS Hour,
                DATEPART(minute, Time) AS StartMinute,
                LossTime               AS DurationSeconds
            FROM AssemblyLossTime
            WHERE CONVERT(date, Date) = CONVERT(date, GETDATE())
              AND MachineCode = @MachineCode";

        var allLossEvents = await connection.QueryAsync<dynamic>(lossEventsQuery, new { MachineCode = machineCode });
        int totalLossSeconds = 0;

        foreach (var ev in allLossEvents)
        {
            var eventTime = new TimeSpan((int)ev.Hour, (int)ev.StartMinute, 0);
            if (breakTimes.Any(b => eventTime >= b.Start && eventTime < b.End)) continue;

            int hour = (int)ev.Hour;
            if (data.HourlyEvents.ContainsKey(hour))
                data.HourlyEvents[hour].Add(new LossEvent
                {
                    StartMinute = (int)ev.StartMinute,
                    DurationMinutes = (int)Math.Ceiling((decimal)ev.DurationSeconds / 60)
                });

            totalLossSeconds += (int)ev.DurationSeconds;
        }

        data.LossTime = totalLossSeconds / 60;
        data.LoadTime = Math.Max(0, data.WorkingTime - data.LossTime);
        return data;
    }

    // ════════════════════════════════════════════════════════════
    // HELPER LS: Assembly Loss Time per MachineCode
    // ════════════════════════════════════════════════════════════
    private (int lossTime, int loadTime, Dictionary<int, List<object>> hourlyEvents)
        LS_GetAssemblyLossTime(
            SqlConnection conn,
            List<(TimeSpan Start, TimeSpan End)> breakTimes,
            int workingTime,
            string? machineCode = null)
    {
        var hourlyEvents = new Dictionary<int, List<object>>();
        for (int h = 7; h < 16; h++) hourlyEvents[h] = new List<object>();
        int totalLossSeconds = 0;

        try
        {
            string lossQuery = machineCode == null
                ? @"SELECT
                        DATEPART(hour,   Time) AS Hour,
                        DATEPART(minute, Time) AS StartMinute,
                        LossTime               AS DurationSeconds
                    FROM dbo.AssemblyLossTime
                    WHERE CONVERT(date, Date) = CONVERT(date, GETDATE())"
                : @"SELECT
                        DATEPART(hour,   Time) AS Hour,
                        DATEPART(minute, Time) AS StartMinute,
                        LossTime               AS DurationSeconds
                    FROM dbo.AssemblyLossTime
                    WHERE CONVERT(date, Date) = CONVERT(date, GETDATE())
                      AND MachineCode = @MachineCode";

            using var cmd = new SqlCommand(lossQuery, conn);
            cmd.CommandTimeout = 30;
            if (machineCode != null)
                cmd.Parameters.AddWithValue("@MachineCode", machineCode);

            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                int hour = Convert.ToInt32(reader["Hour"]);
                int startMin = Convert.ToInt32(reader["StartMinute"]);
                int durSec = Convert.ToInt32(reader["DurationSeconds"]);
                var evTime = new TimeSpan(hour, startMin, 0);

                if (breakTimes.Any(b => evTime >= b.Start && evTime < b.End)) continue;

                if (hourlyEvents.ContainsKey(hour))
                    hourlyEvents[hour].Add(new
                    {
                        startMinute = startMin,
                        durationMinutes = (int)Math.Ceiling((decimal)durSec / 60)
                    });

                totalLossSeconds += durSec;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Query AssemblyLossTime gagal (machineCode={MC})", machineCode ?? "ALL");
        }

        int lossTime = totalLossSeconds / 60;
        int loadTime = Math.Max(0, workingTime - lossTime);
        return (lossTime, loadTime, hourlyEvents);
    }

    // ════════════════════════════════════════════════════════════
    // HELPER LS: Hitung Target SKD per segmen model
    // ════════════════════════════════════════════════════════════
    private int LS_CalculateSKDTargetByModelSegment(
        SqlConnection connLSBU,
        List<(TimeSpan Start, TimeSpan End)> breakTimes)
    {
        var segments = new List<(double CycleTimeSec, DateTime SegStart)>();

        try
        {
            using var cmd = new SqlCommand(@"
                SELECT
                    g.CycleTime,
                    MIN(p.ScanningDate) AS SegStart
                FROM dbo.Production_Results p
                JOIN dbo.GlobalModelCodes g ON p.ModelCodeId = g.ModelCodeId
                WHERE CONVERT(date, p.ScanningDate) = CONVERT(date, GETDATE())
                  AND g.CycleTime IS NOT NULL
                  AND g.CycleTime > 0
                GROUP BY p.ModelCodeId, g.CycleTime
                ORDER BY SegStart ASC", connLSBU);
            cmd.CommandTimeout = 30;

            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                double ct = Convert.ToDouble(reader["CycleTime"]);
                DateTime segSt = Convert.ToDateTime(reader["SegStart"]);
                if (ct > 0) segments.Add((ct, segSt));
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Query model segments SKD gagal");
            return 0;
        }

        if (segments.Count == 0) return 0;

        int totalTarget = 0;
        DateTime now = DateTime.Now;
        TimeSpan workStart = new TimeSpan(7, 7, 0);
        TimeSpan workEnd = new TimeSpan(15, 55, 0);

        for (int i = 0; i < segments.Count; i++)
        {
            var (cycleTimeSec, segStart) = segments[i];
            DateTime segEnd = (i + 1 < segments.Count) ? segments[i + 1].SegStart : now;

            var effStart = segStart.TimeOfDay < workStart ? workStart : segStart.TimeOfDay;
            var effEnd = segEnd.TimeOfDay > workEnd ? workEnd : segEnd.TimeOfDay;

            if (effEnd <= effStart) continue;

            double workSec = (effEnd - effStart).TotalSeconds;
            workSec -= breakTimes.Sum(b =>
            {
                var bs = b.Start < effStart ? effStart : b.Start;
                var be = b.End > effEnd ? effEnd : b.End;
                return be > bs ? (be - bs).TotalSeconds : 0;
            });

            totalTarget += (int)(Math.Max(0, workSec) / cycleTimeSec);
        }

        return totalTarget;
    }

    // ════════════════════════════════════════════════════════════
    // HELPER LS: Hitung DailyPlan SKD
    // ════════════════════════════════════════════════════════════
    private int LS_CalculateSKDDailyPlan(SqlConnection conn)
    {
        int dailyPlan = 0;
        try
        {
            using (var cmd = new SqlCommand(@"
                SELECT ISNULL(SUM(pr.Quantity), 0)
                FROM dbo.ProductionRecords pr
                JOIN dbo.ProductionPlan pp ON pr.PlanId = pp.Id
                WHERE CONVERT(date, pp.CurrentDate) = CONVERT(date, GETDATE())
                  AND pr.MachineCode = 'SKD'", conn))
            {
                cmd.CommandTimeout = 30;
                var result = cmd.ExecuteScalar();
                dailyPlan = (result != null && result != DBNull.Value) ? Convert.ToInt32(result) : 0;
            }

            if (dailyPlan == 0)
            {
                using var cmd = new SqlCommand(@"
                    SELECT ISNULL(SUM(sp.SapPlanNormal), 0)
                    FROM dbo.SapPlan sp
                    JOIN dbo.ProductionPlan pp ON sp.PlanId = pp.Id
                    WHERE sp.MachineCode = 'SKD'
                      AND CONVERT(date, pp.CurrentDate) = CONVERT(date, GETDATE())", conn);
                cmd.CommandTimeout = 30;
                var result = cmd.ExecuteScalar();
                dailyPlan = (result != null && result != DBNull.Value) ? Convert.ToInt32(result) : 0;
                _logger.LogInformation("SKD DailyPlan fallback SapPlan: {Val}", dailyPlan);
            }
            else
            {
                _logger.LogInformation("SKD DailyPlan ProductionRecords: {Val}", dailyPlan);
            }
        }
        catch (Exception ex) { _logger.LogWarning(ex, "Query DailyPlan SKD gagal"); }

        return dailyPlan;
    }

    // ════════════════════════════════════════════════════════════
    // HELPER: Audio Loss Time
    // ════════════════════════════════════════════════════════════
    private async Task<LossTimeData> GetAudioLossTimeAsync(SqlConnection connection)
    {
        var data = new LossTimeData();
        var today = DateTime.Now;
        var breakTimes = today.DayOfWeek == DayOfWeek.Friday ? LSFridayBreakTimes : LSRegularBreakTimes;

        data.BreakTimes = breakTimes.Select(b => new BreakTime { Start = b.Start, End = b.End }).ToList();

        TimeSpan workStart = new TimeSpan(7, 7, 0);
        TimeSpan workEnd = new TimeSpan(15, 55, 0);
        var currentTime = today.TimeOfDay;

        if (currentTime > workStart)
        {
            var effectiveEnd = currentTime > workEnd ? workEnd : currentTime;
            int totalMinutes = (int)(effectiveEnd - workStart).TotalMinutes;
            int totalRest = breakTimes.Sum(b =>
            {
                if (effectiveEnd <= b.Start || workStart >= b.End) return 0;
                var rs = b.Start > workStart ? b.Start : workStart;
                var re = b.End < effectiveEnd ? b.End : effectiveEnd;
                return (int)Math.Max(0, (re - rs).TotalMinutes);
            });
            data.WorkingTime = Math.Max(0, totalMinutes - totalRest);
        }

        for (int h = 7; h < 16; h++)
            data.HourlyEvents[h] = new List<LossEvent>();

        int totalLossSeconds = 0;
        try
        {
            var lossQuery = @"
                SELECT
                    DATEPART(hour,   Time) AS Hour,
                    DATEPART(minute, Time) AS StartMinute,
                    LossTime               AS DurationSeconds
                FROM dbo.AssemblyLossTime
                WHERE CONVERT(date, Date) = CONVERT(date, GETDATE())
                  AND MachineCode = @MachineCode";

            var allLossEvents = await connection.QueryAsync<dynamic>(lossQuery, new { MachineCode = "MCH1-01" });

            foreach (var ev in allLossEvents)
            {
                var evTime = new TimeSpan((int)ev.Hour, (int)ev.StartMinute, 0);
                if (breakTimes.Any(b => evTime >= b.Start && evTime < b.End)) continue;

                int hour = (int)ev.Hour;
                if (data.HourlyEvents.ContainsKey(hour))
                    data.HourlyEvents[hour].Add(new LossEvent
                    {
                        StartMinute = (int)ev.StartMinute,
                        DurationMinutes = (int)Math.Ceiling((decimal)ev.DurationSeconds / 60)
                    });

                totalLossSeconds += (int)ev.DurationSeconds;
            }
        }
        catch (Exception ex) { _logger.LogWarning(ex, "Query LossTime Audio gagal"); }

        data.LossTime = totalLossSeconds / 60;
        data.LoadTime = Math.Max(0, data.WorkingTime - data.LossTime);
        return data;
    }

    // ════════════════════════════════════════════════════════════
    // HELPER: WP Loss Time
    // ════════════════════════════════════════════════════════════
    private async Task<LossTimeData> GetWPLossTimeAsync(SqlConnection connection)
    {
        var data = new LossTimeData();
        var today = DateTime.Now;
        var breakTimes = today.DayOfWeek == DayOfWeek.Friday ? LSFridayBreakTimes : LSRegularBreakTimes;

        data.BreakTimes = breakTimes.Select(b => new BreakTime { Start = b.Start, End = b.End }).ToList();

        TimeSpan workStart = new TimeSpan(7, 7, 0);
        TimeSpan workEnd = new TimeSpan(15, 55, 0);
        var currentTime = today.TimeOfDay;

        if (currentTime > workStart)
        {
            var effectiveEnd = currentTime > workEnd ? workEnd : currentTime;
            int totalMinutes = (int)(effectiveEnd - workStart).TotalMinutes;
            int totalRest = breakTimes.Sum(b =>
            {
                if (effectiveEnd <= b.Start || workStart >= b.End) return 0;
                var rs = b.Start > workStart ? b.Start : workStart;
                var re = b.End < effectiveEnd ? b.End : effectiveEnd;
                return (int)Math.Max(0, (re - rs).TotalMinutes);
            });
            data.WorkingTime = Math.Max(0, totalMinutes - totalRest);
        }

        for (int h = 7; h < 16; h++)
            data.HourlyEvents[h] = new List<LossEvent>();

        int totalLossSeconds = 0;
        try
        {
            var lossQuery = @"
                SELECT
                    DATEPART(hour,   Time) AS Hour,
                    DATEPART(minute, Time) AS StartMinute,
                    LossTime               AS DurationSeconds
                FROM dbo.AssemblyLossTime
                WHERE CONVERT(date, Date) = CONVERT(date, GETDATE())
                  AND MachineCode IN ('Line1','Line2','Line3','Line4',
                                      'Line5','Line6','Line7','Line8')";

            var allLossEvents = await connection.QueryAsync<dynamic>(lossQuery);
            foreach (var ev in allLossEvents)
            {
                var evTime = new TimeSpan((int)ev.Hour, (int)ev.StartMinute, 0);
                if (breakTimes.Any(b => evTime >= b.Start && evTime < b.End)) continue;

                int hour = (int)ev.Hour;
                if (data.HourlyEvents.ContainsKey(hour))
                    data.HourlyEvents[hour].Add(new LossEvent
                    {
                        StartMinute = (int)ev.StartMinute,
                        DurationMinutes = (int)Math.Ceiling((decimal)ev.DurationSeconds / 60)
                    });

                totalLossSeconds += (int)ev.DurationSeconds;
            }
        }
        catch (Exception ex) { _logger.LogWarning(ex, "Query LossTime WP gagal"); }

        data.LossTime = totalLossSeconds / 60;
        data.LoadTime = Math.Max(0, data.WorkingTime - data.LossTime);
        return data;
    }
}
}