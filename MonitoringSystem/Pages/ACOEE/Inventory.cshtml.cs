using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Data.SqlClient;

namespace MonitoringSystem.Pages.ACOEE;

[ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
public class InventoryModel : PageModel
{
    private readonly string _connectionString;
    private readonly ILogger<InventoryModel> _logger;

    public InventoryModel(IConfiguration configuration, ILogger<InventoryModel> logger)
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

    [BindProperty(SupportsGet = true)]
    public int? FilterBulan { get; set; }

    [BindProperty(SupportsGet = true)]
    public int? FilterTahun { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? FilterMachineLine { get; set; }

    public int ActiveBulan { get; private set; }
    public int ActiveYear { get; private set; }
    public List<AcOeeInventoryRow> listData { get; private set; } = new();
    public Dictionary<int, decimal> DailyTotals { get; private set; } = new();

    public async Task OnGetAsync()
    {
        var now = DateTime.Now;
        ActiveBulan = FilterBulan is >= 1 and <= 12 ? FilterBulan.Value : now.Month;
        ActiveYear = FilterTahun is >= 2000 and <= 2100 ? FilterTahun.Value : now.Year;

        var firstProductionDay = new DateTime(ActiveYear, ActiveBulan, 1);
        var nextMonth = firstProductionDay.AddMonths(1);
        var filterByMachine = !string.IsNullOrWhiteSpace(FilterMachineLine) &&
                              !string.Equals(FilterMachineLine, "All", StringComparison.OrdinalIgnoreCase);
        var databaseMachine = filterByMachine ? ToDatabaseMachineName(FilterMachineLine!) : null;

        const string machinePredicate = "AND LTRIM(RTRIM([Machine])) = @machine";
        var query = $"""
            ;WITH DataShift AS
            (
                SELECT
                    NULLIF(LTRIM(RTRIM([Model])), '') AS [Model],
                    NULLIF(LTRIM(RTRIM([Machine])), '') AS [Machine],
                    CONVERT(date, DATEADD(hour, -7, [tanggal])) AS [TanggalProduksi]
                FROM dbo.[DataMatang]
                WHERE DATEADD(hour, -7, [tanggal]) >= @productionMonthStart
                  AND DATEADD(hour, -7, [tanggal]) < @nextProductionMonth
                  {(filterByMachine ? machinePredicate : string.Empty)}
            )
            SELECT
                [TanggalProduksi],
                [Machine],
                [Model],
                COUNT_BIG(*) AS [Quantity],
                GROUPING([TanggalProduksi]) AS [IsGrandTotal],
                GROUPING([Machine]) AS [IsDailyTotal],
                GROUPING([Model]) AS [IsMachineTotal]
            FROM DataShift
            GROUP BY ROLLUP
            (
                [TanggalProduksi],
                [Machine],
                [Model]
            )
            ORDER BY
                GROUPING([TanggalProduksi]) ASC,
                [TanggalProduksi] DESC,
                GROUPING([Machine]) ASC,
                [Machine] ASC,
                GROUPING([Model]) ASC,
                [Quantity] DESC;
            """;

        try
        {
            var dataMap = new Dictionary<string, AcOeeInventoryRow>(StringComparer.OrdinalIgnoreCase);

            await using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();

            await using var command = new SqlCommand(query, connection);
            command.Parameters.Add("@productionMonthStart", System.Data.SqlDbType.DateTime2).Value = firstProductionDay;
            command.Parameters.Add("@nextProductionMonth", System.Data.SqlDbType.DateTime2).Value = nextMonth;

            if (filterByMachine)
            {
                command.Parameters.Add("@machine", System.Data.SqlDbType.NVarChar, 100).Value = databaseMachine!;
            }

            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var isGrandTotal = Convert.ToInt32(reader["IsGrandTotal"]) == 1;
                var isDailyTotal = Convert.ToInt32(reader["IsDailyTotal"]) == 1;
                var isMachineTotal = Convert.ToInt32(reader["IsMachineTotal"]) == 1;
                var quantity = Convert.ToDecimal(reader["Quantity"]);

                if (isGrandTotal)
                {
                    continue;
                }

                var productionDay = Convert.ToDateTime(reader["TanggalProduksi"]).Day;

                // Take the SQL ROLLUP daily total directly, including rows whose model is empty.
                if (isDailyTotal)
                {
                    DailyTotals[productionDay] = quantity;
                    continue;
                }

                if (isMachineTotal)
                {
                    continue;
                }

                var model = reader["Model"] == DBNull.Value ? "CS-QN-" : reader["Model"].ToString()!;
                var databaseMachineName = reader["Machine"] == DBNull.Value ? "MACHINE KOSONG" : reader["Machine"].ToString()!;
                var machine = ToDisplayMachineName(databaseMachineName);
                var key = $"{machine}|{model}";

                if (!dataMap.TryGetValue(key, out var row))
                {
                    row = new AcOeeInventoryRow
                    {
                        Data_Id = key,
                        Model = model,
                        MachineLine = machine
                    };
                    dataMap[key] = row;
                }

                if (row.DailyValues.TryGetValue(productionDay, out var existingQuantity))
                {
                    row.DailyValues[productionDay] = (existingQuantity ?? 0) + quantity;
                }
                else
                {
                    row.DailyValues[productionDay] = quantity;
                }
            }

            listData = dataMap.Values
                .OrderBy(row => row.MachineLine)
                .ThenBy(row => row.Model)
                .ToList();
        }
        catch (Exception exception)
        {
            _logger.LogError(exception,
                "Failed to load AC OEE inventory from COBADAQ for {Month}/{Year} and machine {Machine}.",
                ActiveBulan, ActiveYear, FilterMachineLine ?? "All");

            TempData["StatusMessage"] = "error";
            TempData["Message"] = $"Inventory data could not be loaded from COBADAQ: {exception.Message}";
            listData = new List<AcOeeInventoryRow>();
            DailyTotals = new Dictionary<int, decimal>();
        }
    }

    private static string ToDatabaseMachineName(string machineName)
    {
        return machineName.Trim().ToUpperInvariant() switch
        {
            "CONDENSER" => "LINE-CONDENSOR",
            "EVAPORATOR" => "LINE-EVAPORATOR",
            _ => machineName.Trim()
        };
    }

    private static string ToDisplayMachineName(string machineName)
    {
        return machineName.Trim().ToUpperInvariant() switch
        {
            "LINE-CONDENSOR" => "Condenser",
            "LINE-EVAPORATOR" => "Evaporator",
            _ => machineName.Trim()
        };
    }
}

public class AcOeeInventoryRow
{
    public string Data_Id { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public string MachineLine { get; set; } = string.Empty;
    public Dictionary<int, decimal?> DailyValues { get; set; } = new();
}
