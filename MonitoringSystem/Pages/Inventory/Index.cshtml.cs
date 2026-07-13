using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using MonitoringSystem.Models;

namespace MonitoringSystem.Pages.Inventory
{
    public class indexModel : PageModel
    {
        private readonly ScaffoldedDbContext _context;
        private readonly ILogger<indexModel> _logger;

        public indexModel(ScaffoldedDbContext context, ILogger<indexModel> logger)
        {
            _context = context;
            _logger = logger;
        }

        // ── Filter Bulan & Tahun terpisah ────────────────────────
        [BindProperty(SupportsGet = true)]
        public int? FilterBulan { get; set; }

        [BindProperty(SupportsGet = true)]
        public int? FilterTahun { get; set; }

        [BindProperty(SupportsGet = true)]
        public string FilterMachineLine { get; set; }

        // Bulan & tahun yang benar-benar dipakai (fallback ke bulan/tahun sekarang)
        public int ActiveBulan { get; set; }
        public int ActiveYear { get; set; }

        public List<InventoryData> listData { get; set; } = new();

        // ─── GET ───────────────────────────────────────────────
        public async Task OnGetAsync()
        {
            listData = new List<InventoryData>();

            try
            {
                // ── Tentukan bulan & tahun aktif ──────────────────
                var now = DateTime.Now;
                ActiveBulan = (FilterBulan.HasValue && FilterBulan.Value >= 1 && FilterBulan.Value <= 12)
                    ? FilterBulan.Value
                    : now.Month;

                ActiveYear = (FilterTahun.HasValue && FilterTahun.Value >= 2000 && FilterTahun.Value <= 2100)
                    ? FilterTahun.Value
                    : now.Year;

                var startDate = new DateTime(ActiveYear, ActiveBulan, 1);
                var endDate = startDate.AddMonths(1);

                var conn = _context.Database.GetDbConnection();
                await conn.OpenAsync();

                using var cmd = conn.CreateCommand();

                var machineFilter = string.IsNullOrEmpty(FilterMachineLine)
                    ? "" : "AND o.MachineCode = @filterMachine";

                cmd.CommandText = $@"
                    SELECT 
                        o.Product_Id AS Data_Id,
                        ISNULL(m.ProductName, o.Product_Id) AS Model,
                        o.MachineCode,
                        DAY(o.Date) AS DayNum,
                        (MAX(o.GoodUnit) - MIN(o.GoodUnit) + 1) AS Actual
                    FROM OEESN o
                    LEFT JOIN MasterData m ON m.Product_Id = o.Product_Id
                    WHERE o.Date >= @startDate AND o.Date < @endDate
                    {machineFilter}
                    GROUP BY o.Product_Id, m.ProductName, o.MachineCode, DAY(o.Date)
                    ORDER BY o.MachineCode, m.ProductName, DayNum";

                var pStart = cmd.CreateParameter();
                pStart.ParameterName = "@startDate";
                pStart.Value = startDate;
                cmd.Parameters.Add(pStart);

                var pEnd = cmd.CreateParameter();
                pEnd.ParameterName = "@endDate";
                pEnd.Value = endDate;
                cmd.Parameters.Add(pEnd);

                if (!string.IsNullOrEmpty(FilterMachineLine))
                {
                    var pMachine = cmd.CreateParameter();
                    pMachine.ParameterName = "@filterMachine";
                    pMachine.Value = FilterMachineLine;
                    cmd.Parameters.Add(pMachine);
                }

                // ── Group hasil query per Product_Id + MachineCode ──
                var dataMap = new Dictionary<string, InventoryData>();

                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    var dataId = reader["Data_Id"]?.ToString();
                    var model = reader["Model"]?.ToString();
                    var machineLine = reader["MachineCode"]?.ToString();
                    var dayNum = Convert.ToInt32(reader["DayNum"]);
                    decimal? actual = reader["Actual"] == DBNull.Value ? null : Convert.ToDecimal(reader["Actual"]);

                    var key = $"{dataId}|{machineLine}";

                    if (!dataMap.TryGetValue(key, out var invData))
                    {
                        invData = new InventoryData
                        {
                            Data_Id = dataId,
                            Model = model,
                            MachineLine = machineLine,
                            DailyValues = new Dictionary<int, decimal?>()
                        };
                        dataMap[key] = invData;
                    }

                    invData.DailyValues[dayNum] = actual;
                }

                await conn.CloseAsync();

                listData = dataMap.Values.ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error loading Inventory data: {ex.Message}");
                TempData["StatusMessage"] = "error";
                TempData["Message"] = $"Error loading data: {ex.Message}";

                // Fallback biar view tetap bisa render walau query gagal
                if (ActiveBulan == 0) ActiveBulan = DateTime.Now.Month;
                if (ActiveYear == 0) ActiveYear = DateTime.Now.Year;
            }
        }
    }

    // ─── DATA MODEL ─────────────────────────────────────────────
    public class InventoryData
    {
        public string Data_Id { get; set; }
        public string Model { get; set; }
        public string MachineLine { get; set; }
        public Dictionary<int, decimal?> DailyValues { get; set; } = new();
    }
}