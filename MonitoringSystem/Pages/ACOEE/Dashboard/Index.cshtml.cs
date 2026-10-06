using Microsoft.Data.SqlClient;
using Microsoft.AspNetCore.Mvc.RazorPages;
using MonitoringSystem.Pages.ACOEE;

namespace MonitoringSystem.Pages.ACOEE.Dashboard;

public class IndexModel : PageModel
{
    private readonly string _cobadaqConnectionString;
    private readonly ILogger<IndexModel> _logger;

    public IndexModel(IConfiguration configuration, ILogger<IndexModel> logger)
    {
        var panamonConnection = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("DefaultConnection is not configured.");

        _cobadaqConnectionString = new SqlConnectionStringBuilder(panamonConnection)
        {
            InitialCatalog = "COBADAQ"
        }.ConnectionString;

        _logger = logger;
    }

    public double EvaporatorOperatingPercent { get; private set; }
    public DateTime? EvaporatorFirstScan { get; private set; }
    public DateTime? EvaporatorLastScan { get; private set; }
    public double EvaporatorLoadTimeMinutes { get; private set; }
    public double ExpanderKyoshinOperatingPercent { get; private set; }
    public DateTime? ExpanderKyoshinFirstScan { get; private set; }
    public DateTime? ExpanderKyoshinLastScan { get; private set; }
    public double ExpanderKyoshinLoadTimeMinutes { get; private set; }
    public double CondenserOperatingPercent { get; private set; }
    public DateTime? CondenserFirstScan { get; private set; }
    public DateTime? CondenserLastScan { get; private set; }
    public double CondenserLoadTimeMinutes { get; private set; }
    public double ExpanderLiChinOperatingPercent { get; private set; }
    public DateTime? ExpanderLiChinFirstScan { get; private set; }
    public DateTime? ExpanderLiChinLastScan { get; private set; }
    public double ExpanderLiChinLoadTimeMinutes { get; private set; }

    public async Task OnGetAsync()
    {
        var now = DateTime.Now;
        var productionDate = now.TimeOfDay < TimeSpan.FromHours(7)
            ? now.Date.AddDays(-1)
            : now.Date;
        var productionStart = productionDate.AddHours(7);
        var productionEnd = productionStart.AddDays(1);

        const string query = """
            SELECT
                LTRIM(RTRIM(d.Source)) AS Source,
                MIN(d.tanggal) AS ScanPertama,
                MAX(d.tanggal) AS ScanTerakhir
            FROM dbo.DataMatang AS d
            WHERE LTRIM(RTRIM(d.Source)) IN ('Evaporator', 'Expander635', 'Condenser', 'LiChinExp')
              AND d.tanggal >= @productionStart
              AND d.tanggal < @productionEnd
            GROUP BY LTRIM(RTRIM(d.Source));
            """;

        try
        {
            await using var connection = new SqlConnection(_cobadaqConnectionString);
            await connection.OpenAsync(HttpContext.RequestAborted);

            await using var command = new SqlCommand(query, connection);
            command.Parameters.AddWithValue("@productionStart", productionStart);
            command.Parameters.AddWithValue("@productionEnd", productionEnd);

            await using var reader = await command.ExecuteReaderAsync(HttpContext.RequestAborted);
            while (await reader.ReadAsync(HttpContext.RequestAborted))
            {
                var source = reader.GetString(reader.GetOrdinal("Source"));
                var firstScan = reader.GetDateTime(reader.GetOrdinal("ScanPertama"));
                var lastScan = reader.GetDateTime(reader.GetOrdinal("ScanTerakhir"));
                var loadTimeMinutes = Math.Max(0d, (lastScan - firstScan).TotalMinutes);
                var operatingPercent = AcOeeMetricsCalculator.CalculateOperatingPercent(loadTimeMinutes);

                if (source.Equals("Evaporator", StringComparison.OrdinalIgnoreCase))
                {
                    EvaporatorFirstScan = firstScan;
                    EvaporatorLastScan = lastScan;
                    EvaporatorLoadTimeMinutes = loadTimeMinutes;
                    EvaporatorOperatingPercent = operatingPercent;
                }
                else if (source.Equals("Expander635", StringComparison.OrdinalIgnoreCase))
                {
                    ExpanderKyoshinFirstScan = firstScan;
                    ExpanderKyoshinLastScan = lastScan;
                    ExpanderKyoshinLoadTimeMinutes = loadTimeMinutes;
                    ExpanderKyoshinOperatingPercent = operatingPercent;
                }
                else if (source.Equals("Condenser", StringComparison.OrdinalIgnoreCase))
                {
                    CondenserFirstScan = firstScan;
                    CondenserLastScan = lastScan;
                    CondenserLoadTimeMinutes = loadTimeMinutes;
                    CondenserOperatingPercent = operatingPercent;
                }
                else if (source.Equals("LiChinExp", StringComparison.OrdinalIgnoreCase))
                {
                    ExpanderLiChinFirstScan = firstScan;
                    ExpanderLiChinLastScan = lastScan;
                    ExpanderLiChinLoadTimeMinutes = loadTimeMinutes;
                    ExpanderLiChinOperatingPercent = operatingPercent;
                }
            }
        }
        catch (Exception exception)
        {
            _logger.LogError(
                exception,
                "Failed to calculate dashboard operating percentages for production window {ProductionStart} - {ProductionEnd}.",
                productionStart,
                productionEnd);
        }
    }
}
