using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Data.SqlClient;
using System.Data;

namespace MonitoringSystem.Pages.CAC.ProdReason;

public class IndexModel : PageModel
{
    private static readonly DateTime MinimumStartDate = new(2026, 8, 1);
    private readonly IConfiguration _configuration;

    public IndexModel(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    [BindProperty(SupportsGet = true)]
    public DateTime StartDate { get; set; } = MinimumStartDate;

    [BindProperty(SupportsGet = true)]
    public DateTime EndDate { get; set; } = DateTime.Today;

    [BindProperty(SupportsGet = true)]
    public string MachineLine { get; set; } = "CAC-ALL";

    public List<ModelPerformance> Models { get; private set; } = new();
    public List<DailyModelPerformance> DailyRows { get; private set; } = new();
    public List<ModelPerformance> TopSapModels { get; private set; } = new();
    public List<ModelPerformance> TopChangeModels { get; private set; } = new();
    public List<ModelPerformance> TopChangeVsSapModels { get; private set; } = new();
    public List<ModelPerformance> PriorityModels { get; private set; } = new();
    public List<ModelPerformance> ChartModels { get; private set; } = new();

    public long TotalSapPlan => Models.Sum(x => x.SapPlan);
    public long TotalChangePlan => Models.Sum(x => x.ChangePlan);
    public long TotalActual => Models.Sum(x => x.Actual);
    public long TotalActualForSap => Models.Where(x => x.SapPlan > 0).Sum(x => x.Actual);
    public int SapPlannedModels => Models.Count(x => x.SapPlan > 0);
    public int StrongActualModels => Models.Count(x => x.SapPlan > 0 && x.SapHitRate >= 80m);
    public int ReliableChangeModels => Models.Count(x => x.ChangePlan > 0 && x.ChangeHitRate >= 80m);
    public decimal TotalActualVsSapPct => TotalSapPlan > 0 ? TotalActualForSap * 100m / TotalSapPlan : 0m;

    public string? ErrorMessage { get; private set; }

    public async Task OnGetAsync()
    {
        NormalizeFilters();

        try
        {
            await LoadDataAsync();
            BuildRankings();
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Data belum dapat dimuat: {ex.Message}";
        }
    }

    private void NormalizeFilters()
    {
        if (StartDate == default || StartDate < MinimumStartDate)
            StartDate = MinimumStartDate;

        if (StartDate > DateTime.Today)
            StartDate = DateTime.Today;

        if (EndDate == default || EndDate > DateTime.Today)
            EndDate = DateTime.Today;

        if (EndDate < StartDate)
            EndDate = StartDate;

        if (MachineLine is not ("CAC" or "SKD" or "STANDING FLOOR"))
            MachineLine = "CAC-ALL";
    }

    private async Task LoadDataAsync()
    {
        var connectionString = _configuration.GetConnectionString("DefaultConnection");
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new InvalidOperationException("DefaultConnection belum dikonfigurasi.");

        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();

        await using var command = new SqlCommand(PerformanceSql, connection)
        {
            CommandTimeout = 180
        };
        command.Parameters.Add("@StartDate", SqlDbType.Date).Value = StartDate.Date;
        command.Parameters.Add("@EndDate", SqlDbType.Date).Value = EndDate.Date;
        command.Parameters.Add("@MachineLine", SqlDbType.VarChar, 20).Value = MachineLine;

        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            Models.Add(new ModelPerformance
            {
                ModelName = reader.GetString(reader.GetOrdinal("ModelName")),
                MachineCode = reader.GetString(reader.GetOrdinal("MachineCode")),
                SapPlan = Convert.ToInt64(reader["SapPlan"]),
                ChangePlan = Convert.ToInt64(reader["ChangePlan"]),
                Actual = Convert.ToInt64(reader["Actual"]),
                SapDays = Convert.ToInt32(reader["SapDays"]),
                ChangeDays = Convert.ToInt32(reader["ChangeDays"]),
                ProductionDays = Convert.ToInt32(reader["ProductionDays"]),
                ActualMeetsSapDays = Convert.ToInt32(reader["ActualMeetsSapDays"]),
                ActualMeetsChangeDays = Convert.ToInt32(reader["ActualMeetsChangeDays"]),
                ChangeMeetsSapDays = Convert.ToInt32(reader["ChangeMeetsSapDays"]),
                FirstProducedDays = Convert.ToInt32(reader["FirstProducedDays"]),
                FirstChangePlanDays = Convert.ToInt32(reader["FirstChangePlanDays"]),
                AverageActualOrder = reader["AverageActualOrder"] == DBNull.Value
                    ? null
                    : Convert.ToDecimal(reader["AverageActualOrder"]),
                AverageChangeOrder = reader["AverageChangeOrder"] == DBNull.Value
                    ? null
                    : Convert.ToDecimal(reader["AverageChangeOrder"])
            });
        }

        if (await reader.NextResultAsync())
        {
            while (await reader.ReadAsync())
            {
                DailyRows.Add(new DailyModelPerformance
                {
                    ProductionDate = reader.GetDateTime(reader.GetOrdinal("ProductionDate")),
                    MachineCode = reader.GetString(reader.GetOrdinal("MachineCode")),
                    ModelName = reader.GetString(reader.GetOrdinal("ModelName")),
                    SapPlan = Convert.ToInt64(reader["SapPlan"]),
                    ChangePlan = Convert.ToInt64(reader["ChangePlan"]),
                    Actual = Convert.ToInt64(reader["Actual"]),
                    ActualOrder = reader["ActualOrder"] == DBNull.Value
                        ? null
                        : Convert.ToInt32(reader["ActualOrder"]),
                    ChangeOrder = reader["ChangeOrder"] == DBNull.Value
                        ? null
                        : Convert.ToInt32(reader["ChangeOrder"]),
                    FirstScan = reader["FirstScan"] == DBNull.Value
                        ? null
                        : Convert.ToDateTime(reader["FirstScan"])
                });
            }
        }
    }

    private void BuildRankings()
    {
        PriorityModels = Models
            .Where(x => x.Actual > 0)
            .OrderByDescending(x => x.FirstProducedDays)
            .ThenBy(x => x.AverageActualOrder ?? decimal.MaxValue)
            .ThenByDescending(x => x.ProductionDays)
            .ThenByDescending(x => x.Actual)
            .ToList();

        for (var index = 0; index < PriorityModels.Count; index++)
            PriorityModels[index].PriorityRank = index + 1;

        TopSapModels = Models
            .Where(x => x.SapPlan > 0)
            .OrderByDescending(x => x.SapHitRate)
            .ThenByDescending(x => x.ActualVsSapPct)
            .ThenByDescending(x => x.SapPlan)
            .Take(8)
            .ToList();

        TopChangeModels = Models
            .Where(x => x.ChangePlan > 0)
            .OrderByDescending(x => x.ChangeHitRate)
            .ThenByDescending(x => x.ActualVsChangePct)
            .ThenByDescending(x => x.ChangePlan)
            .Take(8)
            .ToList();

        TopChangeVsSapModels = Models
            .Where(x => x.SapPlan > 0 && x.ChangePlan > 0)
            .OrderByDescending(x => x.ChangeMeetsSapRate)
            .ThenByDescending(x => x.ChangeVsSapPct)
            .ThenByDescending(x => x.SapPlan)
            .Take(8)
            .ToList();

        ChartModels = Models
            .Where(x => x.SapPlan > 0 || x.ChangePlan > 0)
            .OrderByDescending(x => Math.Max(x.SapPlan, x.ChangePlan))
            .Take(12)
            .ToList();

        Models = Models
            .OrderBy(x => x.PriorityRank == 0 ? int.MaxValue : x.PriorityRank)
            .ThenByDescending(x => x.SapPlan)
            .ToList();
    }

    public sealed class ModelPerformance
    {
        public string ModelName { get; set; } = "";
        public string MachineCode { get; set; } = "";
        public long SapPlan { get; set; }
        public long ChangePlan { get; set; }
        public long Actual { get; set; }
        public int SapDays { get; set; }
        public int ChangeDays { get; set; }
        public int ProductionDays { get; set; }
        public int ActualMeetsSapDays { get; set; }
        public int ActualMeetsChangeDays { get; set; }
        public int ChangeMeetsSapDays { get; set; }
        public int FirstProducedDays { get; set; }
        public int FirstChangePlanDays { get; set; }
        public decimal? AverageActualOrder { get; set; }
        public decimal? AverageChangeOrder { get; set; }
        public int PriorityRank { get; set; }

        public decimal ActualVsSapPct => SapPlan > 0 ? Actual * 100m / SapPlan : 0m;
        public decimal ChangeVsSapPct => SapPlan > 0 ? ChangePlan * 100m / SapPlan : 0m;
        public decimal ActualVsChangePct => ChangePlan > 0 ? Actual * 100m / ChangePlan : 0m;
        public decimal SapHitRate => SapDays > 0 ? ActualMeetsSapDays * 100m / SapDays : 0m;
        public decimal ChangeHitRate => ChangeDays > 0 ? ActualMeetsChangeDays * 100m / ChangeDays : 0m;
        public decimal ChangeMeetsSapRate => SapDays > 0 ? ChangeMeetsSapDays * 100m / SapDays : 0m;

        public string Status => SapPlan == 0
            ? "Tanpa SAP"
            : SapHitRate >= 80m && ActualVsSapPct >= 90m
                ? "Konsisten"
                : SapHitRate >= 50m || ActualVsSapPct >= 80m
                    ? "Pantau"
                    : "Perlu perhatian";

        public string StatusKey => Status switch
        {
            "Konsisten" => "strong",
            "Pantau" => "watch",
            "Perlu perhatian" => "attention",
            _ => "no-sap"
        };
    }

    public sealed class DailyModelPerformance
    {
        public DateTime ProductionDate { get; set; }
        public string MachineCode { get; set; } = "";
        public string ModelName { get; set; } = "";
        public long SapPlan { get; set; }
        public long ChangePlan { get; set; }
        public long Actual { get; set; }
        public int? ActualOrder { get; set; }
        public int? ChangeOrder { get; set; }
        public DateTime? FirstScan { get; set; }
        public bool MeetsSap => SapPlan > 0 && Actual >= SapPlan;
        public bool MeetsChange => ChangePlan > 0 && Actual >= ChangePlan;
    }

    private const string PerformanceSql = """
WITH MasterModel AS (
    SELECT Product_Id, MachineCode, MAX(ProductName) AS ProductName
    FROM dbo.MasterData
    GROUP BY Product_Id, MachineCode
),
ActualSource AS (
    SELECT
        CASE WHEN CAST(o.SDate AS time) < '07:00:00'
             THEN CAST(DATEADD(day, -1, o.SDate) AS date)
             ELSE CAST(o.SDate AS date)
        END AS ProductionDate,
        o.MachineCode,
        ISNULL(m.ProductName, CONVERT(varchar(100), o.Product_Id)) AS RawModel,
        o.SDate
    FROM dbo.oeesn o
    LEFT JOIN MasterModel m
      ON m.Product_Id = o.Product_Id
     AND m.MachineCode = o.MachineCode
    WHERE o.SDate >= DATEADD(hour, 7, CAST(@StartDate AS datetime2))
      AND o.SDate < DATEADD(hour, 7, DATEADD(day, 1, CAST(@EndDate AS datetime2)))
      AND (@MachineLine = 'All' OR o.MachineCode = @MachineLine)
),
ActualNamed AS (
    SELECT a.*,
           UPPER(REPLACE(REPLACE(REPLACE(REPLACE(LTRIM(RTRIM(a.RawModel)), ' ', ''), '-', ''), '.', ''), '/', '')) AS BaseKey
    FROM ActualSource a
),
ActualNormalized AS (
    SELECT ProductionDate, MachineCode, RawModel, SDate,
           CASE
             WHEN BaseKey LIKE 'KIOS%18%' THEN CONCAT('KIOS18|', MachineCode)
             WHEN BaseKey LIKE 'KIOS%12%' THEN CONCAT('KIOS12|', MachineCode)
             WHEN BaseKey LIKE 'KIOS%9%'  THEN CONCAT('KIOS9|', MachineCode)
             WHEN BaseKey LIKE 'KIOS%7%'  THEN CONCAT('KIOS7|', MachineCode)
             WHEN BaseKey LIKE 'KIOS%5%'  THEN CONCAT('KIOS5|', MachineCode)
             ELSE CONCAT(BaseKey, '|', MachineCode)
           END AS ModelKey
    FROM ActualNamed
),
ActualDailyBase AS (
    SELECT ProductionDate, MachineCode, ModelKey,
           MAX(RawModel) AS ModelName,
           COUNT_BIG(*) AS Actual,
           MIN(SDate) AS FirstScan
    FROM ActualNormalized
    WHERE ProductionDate BETWEEN @StartDate AND @EndDate
    GROUP BY ProductionDate, MachineCode, ModelKey
),
ActualDaily AS (
    SELECT *, DENSE_RANK() OVER (
        PARTITION BY ProductionDate, MachineCode ORDER BY FirstScan, ModelKey
    ) AS ActualOrder
    FROM ActualDailyBase
),
SapSource AS (
    SELECT CAST(pp.CurrentDate AS date) AS ProductionDate,
           sp.MachineCode,
           CONVERT(varchar(100), sp.ProductName) AS RawModel,
           CAST(ISNULL(sp.SapPlanNormal, 0) + ISNULL(sp.SapPlanOvertime, 0) AS bigint) AS SapPlan
    FROM dbo.ProductionPlan pp
    INNER JOIN dbo.SapPlan sp ON sp.PlanId = pp.Id
    WHERE pp.CurrentDate BETWEEN @StartDate AND @EndDate
      AND (@MachineLine = 'All' OR sp.MachineCode = @MachineLine)
),
SapNamed AS (
    SELECT s.*,
           UPPER(REPLACE(REPLACE(REPLACE(REPLACE(LTRIM(RTRIM(s.RawModel)), ' ', ''), '-', ''), '.', ''), '/', '')) AS BaseKey
    FROM SapSource s
),
SapNormalized AS (
    SELECT ProductionDate, MachineCode, RawModel, SapPlan,
           CASE
             WHEN BaseKey LIKE 'KIOS%18%' THEN CONCAT('KIOS18|', MachineCode)
             WHEN BaseKey LIKE 'KIOS%12%' THEN CONCAT('KIOS12|', MachineCode)
             WHEN BaseKey LIKE 'KIOS%9%'  THEN CONCAT('KIOS9|', MachineCode)
             WHEN BaseKey LIKE 'KIOS%7%'  THEN CONCAT('KIOS7|', MachineCode)
             WHEN BaseKey LIKE 'KIOS%5%'  THEN CONCAT('KIOS5|', MachineCode)
             ELSE CONCAT(BaseKey, '|', MachineCode)
           END AS ModelKey
    FROM SapNamed
),
SapDaily AS (
    SELECT ProductionDate, MachineCode, ModelKey,
           MAX(RawModel) AS ModelName,
           SUM(SapPlan) AS SapPlan
    FROM SapNormalized
    GROUP BY ProductionDate, MachineCode, ModelKey
),
ChangeSource AS (
    SELECT CAST(pp.CurrentDate AS date) AS ProductionDate,
           pr.MachineCode,
           CONVERT(varchar(100), pr.ProductName) AS RawModel,
           CAST(ISNULL(pr.Quantity, 0) + ISNULL(pr.Overtime, 0) AS bigint) AS ChangePlan,
           pr.Id
    FROM dbo.ProductionPlan pp
    INNER JOIN dbo.ProductionRecords pr ON pr.PlanId = pp.Id
    WHERE pp.CurrentDate BETWEEN @StartDate AND @EndDate
      AND (@MachineLine = 'All' OR pr.MachineCode = @MachineLine)
),
ChangeNamed AS (
    SELECT c.*,
           UPPER(REPLACE(REPLACE(REPLACE(REPLACE(LTRIM(RTRIM(c.RawModel)), ' ', ''), '-', ''), '.', ''), '/', '')) AS BaseKey
    FROM ChangeSource c
),
ChangeNormalized AS (
    SELECT ProductionDate, MachineCode, RawModel, ChangePlan, Id,
           CASE
             WHEN BaseKey LIKE 'KIOS%18%' THEN CONCAT('KIOS18|', MachineCode)
             WHEN BaseKey LIKE 'KIOS%12%' THEN CONCAT('KIOS12|', MachineCode)
             WHEN BaseKey LIKE 'KIOS%9%'  THEN CONCAT('KIOS9|', MachineCode)
             WHEN BaseKey LIKE 'KIOS%7%'  THEN CONCAT('KIOS7|', MachineCode)
             WHEN BaseKey LIKE 'KIOS%5%'  THEN CONCAT('KIOS5|', MachineCode)
             ELSE CONCAT(BaseKey, '|', MachineCode)
           END AS ModelKey
    FROM ChangeNamed
),
ChangeDailyBase AS (
    SELECT ProductionDate, MachineCode, ModelKey,
           MAX(RawModel) AS ModelName,
           SUM(ChangePlan) AS ChangePlan,
           MIN(Id) AS FirstRecordId
    FROM ChangeNormalized
    GROUP BY ProductionDate, MachineCode, ModelKey
),
ChangeDaily AS (
    SELECT *, DENSE_RANK() OVER (
        PARTITION BY ProductionDate, MachineCode ORDER BY FirstRecordId, ModelKey
    ) AS ChangeOrder
    FROM ChangeDailyBase
),
DailyKeys AS (
    SELECT ProductionDate, MachineCode, ModelKey FROM SapDaily
    UNION
    SELECT ProductionDate, MachineCode, ModelKey FROM ChangeDaily
    UNION
    SELECT ProductionDate, MachineCode, ModelKey FROM ActualDaily
)
SELECT k.ProductionDate, k.MachineCode, k.ModelKey,
       COALESCE(a.ModelName, c.ModelName, s.ModelName, k.ModelKey) AS ModelName,
       ISNULL(s.SapPlan, 0) AS SapPlan,
       ISNULL(c.ChangePlan, 0) AS ChangePlan,
       ISNULL(a.Actual, 0) AS Actual,
       a.FirstScan, a.ActualOrder, c.ChangeOrder
INTO #DailyCombined
FROM DailyKeys k
LEFT JOIN SapDaily s
  ON s.ProductionDate = k.ProductionDate AND s.MachineCode = k.MachineCode AND s.ModelKey = k.ModelKey
LEFT JOIN ChangeDaily c
  ON c.ProductionDate = k.ProductionDate AND c.MachineCode = k.MachineCode AND c.ModelKey = k.ModelKey
LEFT JOIN ActualDaily a
  ON a.ProductionDate = k.ProductionDate AND a.MachineCode = k.MachineCode AND a.ModelKey = k.ModelKey;

WITH
ModelSummary AS (
    SELECT ModelKey, MachineCode,
           COALESCE(
               MAX(CASE WHEN Actual > 0 THEN ModelName END),
               MAX(CASE WHEN ChangePlan > 0 THEN ModelName END),
               MAX(ModelName)
           ) AS ModelName,
           SUM(SapPlan) AS SapPlan,
           SUM(ChangePlan) AS ChangePlan,
           SUM(Actual) AS Actual,
           SUM(CASE WHEN SapPlan > 0 THEN 1 ELSE 0 END) AS SapDays,
           SUM(CASE WHEN ChangePlan > 0 THEN 1 ELSE 0 END) AS ChangeDays,
           SUM(CASE WHEN Actual > 0 THEN 1 ELSE 0 END) AS ProductionDays,
           SUM(CASE WHEN SapPlan > 0 AND Actual >= SapPlan THEN 1 ELSE 0 END) AS ActualMeetsSapDays,
           SUM(CASE WHEN ChangePlan > 0 AND Actual >= ChangePlan THEN 1 ELSE 0 END) AS ActualMeetsChangeDays,
           SUM(CASE WHEN SapPlan > 0 AND ChangePlan >= SapPlan THEN 1 ELSE 0 END) AS ChangeMeetsSapDays,
           SUM(CASE WHEN ActualOrder = 1 THEN 1 ELSE 0 END) AS FirstProducedDays,
           SUM(CASE WHEN ChangeOrder = 1 THEN 1 ELSE 0 END) AS FirstChangePlanDays,
           CAST(AVG(CASE WHEN ActualOrder IS NOT NULL THEN CONVERT(decimal(10,2), ActualOrder) END) AS decimal(10,2)) AS AverageActualOrder,
           CAST(AVG(CASE WHEN ChangeOrder IS NOT NULL THEN CONVERT(decimal(10,2), ChangeOrder) END) AS decimal(10,2)) AS AverageChangeOrder
    FROM #DailyCombined
    GROUP BY ModelKey, MachineCode
)
SELECT ModelName, MachineCode, SapPlan, ChangePlan, Actual,
       SapDays, ChangeDays, ProductionDays,
       ActualMeetsSapDays, ActualMeetsChangeDays, ChangeMeetsSapDays,
       FirstProducedDays, FirstChangePlanDays, AverageActualOrder, AverageChangeOrder
FROM ModelSummary
WHERE SapPlan > 0 OR ChangePlan > 0 OR Actual > 0
ORDER BY SapPlan DESC, ChangePlan DESC, Actual DESC;

SELECT ProductionDate, MachineCode, ModelName, SapPlan, ChangePlan, Actual,
       ActualOrder, ChangeOrder, FirstScan
FROM #DailyCombined
WHERE SapPlan > 0 OR ChangePlan > 0 OR Actual > 0
ORDER BY ProductionDate DESC, MachineCode,
         CASE WHEN ActualOrder IS NULL THEN 999 ELSE ActualOrder END,
         CASE WHEN ChangeOrder IS NULL THEN 999 ELSE ChangeOrder END,
         ModelName;
""";
}
