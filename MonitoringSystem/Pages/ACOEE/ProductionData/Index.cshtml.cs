using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Data;
using MonitoringSystem.Pages.ACOEE;

namespace MonitoringSystem.Pages.ACOEE.ProductionData;

public class IndexModel : PageModel
{
    private readonly string _connectionString;
    private readonly ILogger<IndexModel> _logger;

    public IndexModel(IConfiguration configuration, ILogger<IndexModel> logger)
    {
        var panamonConnection = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("DefaultConnection is not configured.");

        var connectionBuilder = new SqlConnectionStringBuilder(panamonConnection)
        {
            InitialCatalog = "COBADAQ"
        };

        _connectionString = connectionBuilder.ConnectionString;
        _logger = logger;
    }

    public Dictionary<string, string> LatestMachineModels { get; private set; } =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["Evaporator"] = string.Empty,
            ["Condenser"] = string.Empty
        };

    public async Task OnGetAsync()
    {
        const string query = """
            ;WITH LatestMachineData AS
            (
                SELECT
                    LTRIM(RTRIM([Machine])) AS [Machine],
                    NULLIF(LTRIM(RTRIM([Model])), '') AS [Model],
                    ROW_NUMBER() OVER
                    (
                        PARTITION BY LTRIM(RTRIM([Machine]))
                        ORDER BY [tanggal] DESC, [Id] DESC
                    ) AS [RowNumber]
                FROM dbo.[DataMatang]
                WHERE LTRIM(RTRIM([Machine])) IN ('LINE-EVAPORATOR', 'LINE-CONDENSOR')
            )
            SELECT [Machine], [Model]
            FROM LatestMachineData
            WHERE [RowNumber] = 1;
            """;

        try
        {
            await using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();

            await using var command = new SqlCommand(query, connection);
            await using var reader = await command.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                var machine = reader.GetString(reader.GetOrdinal("Machine"));
                var model = reader.IsDBNull(reader.GetOrdinal("Model"))
                    ? string.Empty
                    : reader.GetString(reader.GetOrdinal("Model"));

                switch (machine.ToUpperInvariant())
                {
                    case "LINE-EVAPORATOR":
                        LatestMachineModels["Evaporator"] = model;
                        break;
                    case "LINE-CONDENSOR":
                        LatestMachineModels["Condenser"] = model;
                        break;
                }
            }
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Failed to load the latest Evaporator and Condenser models from COBADAQ.");
        }
    }

    public async Task<IActionResult> OnGetMachineDataAsync(string machine, DateTime productionDate)
    {
        var databaseMachine = machine.Trim().ToUpperInvariant() switch
        {
            "EVAPORATOR" => "LINE-EVAPORATOR",
            "CONDENSER" => "LINE-CONDENSOR",
            _ => null
        };

        if (databaseMachine is null || productionDate.Year is < 2000 or > 2100)
        {
            return BadRequest(new { message = "Machine or production date is invalid." });
        }

        var productionStart = productionDate.Date.AddHours(7);
        var productionEnd = productionStart.AddDays(1);

        const string query = """
            SELECT
                [LatestRun].[Model] AS [LatestModel],
                COALESCE([LatestRun].[ProdPlan], 0) AS [ProductionPlan],
                CASE
                    WHEN [LatestRun].[Id] IS NULL THEN CONVERT(bigint, 0)
                    ELSE
                    (
                        SELECT COUNT_BIG(*)
                        FROM dbo.[DataMatang] AS [ActualData]
                        WHERE LTRIM(RTRIM([ActualData].[Machine])) = @machine
                          AND [ActualData].[tanggal] >= @productionStart
                          AND [ActualData].[tanggal] < @productionEnd
                          AND
                          (
                              NULLIF(LTRIM(RTRIM([ActualData].[Model])), '') = [LatestRun].[Model]
                              OR
                              (
                                  NULLIF(LTRIM(RTRIM([ActualData].[Model])), '') IS NULL
                                  AND [LatestRun].[Model] IS NULL
                              )
                          )
                    )
                END AS [Actual],
                [OperatingScans].[FirstScan],
                [OperatingScans].[LastScan]
            FROM (VALUES (1)) AS [Seed]([Value])
            OUTER APPLY
            (
                SELECT TOP (1)
                    [Id],
                    NULLIF(LTRIM(RTRIM([Model])), '') AS [Model],
                    [ProdPlan]
                FROM dbo.[DataMatang]
                WHERE LTRIM(RTRIM([Machine])) = @machine
                  AND [tanggal] >= @productionStart
                  AND [tanggal] < @productionEnd
                ORDER BY [tanggal] DESC, [Id] DESC
            ) AS [LatestRun]
            OUTER APPLY
            (
                SELECT
                    MIN([ScanData].[tanggal]) AS [FirstScan],
                    MAX([ScanData].[tanggal]) AS [LastScan]
                FROM dbo.[DataMatang] AS [ScanData]
                WHERE @source IS NOT NULL
                  AND LTRIM(RTRIM([ScanData].[Source])) = @source
                  AND [ScanData].[tanggal] >= @productionStart
                  AND [ScanData].[tanggal] < @productionEnd
            ) AS [OperatingScans];
            """;

        try
        {
            await using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();

            await using var command = new SqlCommand(query, connection);
            command.Parameters.Add("@machine", SqlDbType.NVarChar, 100).Value = databaseMachine;
            command.Parameters.Add("@source", SqlDbType.NVarChar, 100).Value =
                databaseMachine == "LINE-EVAPORATOR" ? "Evaporator" : DBNull.Value;
            command.Parameters.Add("@productionStart", SqlDbType.DateTime2).Value = productionStart;
            command.Parameters.Add("@productionEnd", SqlDbType.DateTime2).Value = productionEnd;

            await using var reader = await command.ExecuteReaderAsync();
            if (!await reader.ReadAsync())
            {
                return NotFound(new { message = "Production data was not found." });
            }

            var latestModel = reader.IsDBNull(reader.GetOrdinal("LatestModel"))
                ? string.Empty
                : reader.GetString(reader.GetOrdinal("LatestModel"));
            var productionPlan = Convert.ToInt64(reader["ProductionPlan"]);
            var actual = Convert.ToInt64(reader["Actual"]);
            object? oeeMetrics = null;

            if (databaseMachine == "LINE-EVAPORATOR")
            {
                var firstScanOrdinal = reader.GetOrdinal("FirstScan");
                var lastScanOrdinal = reader.GetOrdinal("LastScan");
                var loadTimeMinutes = !reader.IsDBNull(firstScanOrdinal) && !reader.IsDBNull(lastScanOrdinal)
                    ? Math.Max(0d, (reader.GetDateTime(lastScanOrdinal) - reader.GetDateTime(firstScanOrdinal)).TotalMinutes)
                    : 0d;
                var operating = AcOeeMetricsCalculator.CalculateOperatingPercent(loadTimeMinutes);
                var ability = AcOeeMetricsCalculator.GenerateAbilityPercent();
                const double quality = 100d;

                oeeMetrics = new
                {
                    oee = AcOeeMetricsCalculator.CalculateOeePercent(operating, ability, quality),
                    ability,
                    operating,
                    quality
                };
            }

            return new JsonResult(new
            {
                model = latestModel,
                productionPlan,
                actual,
                productionStart,
                productionEnd,
                oeeMetrics
            });
        }
        catch (Exception exception)
        {
            _logger.LogError(exception,
                "Failed to load AC OEE production metrics for {Machine} between {Start} and {End}.",
                databaseMachine, productionStart, productionEnd);

            return StatusCode(StatusCodes.Status500InternalServerError,
                new { message = "Production data could not be loaded from COBADAQ." });
        }
    }
}
