using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Data.SqlClient;
using MonitoringSystem.Models;

namespace MonitoringSystem.Pages.Inventory
{
    public class IndexModel : PageModel
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<IndexModel> _logger;

        private string ConnectionString => _configuration.GetConnectionString("DefaultConnection");

        public IndexModel(IConfiguration configuration, ILogger<IndexModel> logger)
        {
            _configuration = configuration;
            _logger = logger;
        }

        // ── Filter ────────────────────────────────────────────────────────────
        [BindProperty(SupportsGet = true)]
        public string MachineLine { get; set; } = "All";

        [BindProperty(SupportsGet = true)]
        public int SelectedMonth { get; set; } = DateTime.Now.Month;

        [BindProperty(SupportsGet = true)]
        public int SelectedYear { get; set; } = DateTime.Now.Year;

        // ── Pivot data ────────────────────────────────────────────────────────
        // DaysInMonth → jumlah kolom tanggal
        public int DaysInMonth { get; set; }

        // PivotData → key: Model name
        //             value: Dictionary<day(int 1-31), InventoryData>
        public Dictionary<string, Dictionary<int, InventoryData>> PivotData { get; set; } = new();

        // Urutan model agar konsisten
        public List<string> ModelList { get; set; } = new();

        public int TotalModels => ModelList.Count;

        // ── Handlers ─────────────────────────────────────────────────────────
        public void OnGet()
        {
            LoadInventoryData();
        }

        public IActionResult OnPostFilter()
        {
            LoadInventoryData();
            return Page();
        }

        public IActionResult OnPostReset()
        {
            MachineLine = "All";
            SelectedMonth = DateTime.Now.Month;
            SelectedYear = DateTime.Now.Year;
            return RedirectToPage();
        }

        // ── Data loading ──────────────────────────────────────────────────────
        private void LoadInventoryData()
        {
            DaysInMonth = DateTime.DaysInMonth(SelectedYear, SelectedMonth);
            PivotData.Clear();
            ModelList.Clear();

            var parameters = new List<SqlParameter>
            {
                new SqlParameter("@month", SelectedMonth),
                new SqlParameter("@year",  SelectedYear)
            };

            string lineFilter = MachineLine == "All"
                ? ""
                : "AND Source = @machineLine";

            if (MachineLine != "All")
                parameters.Add(new SqlParameter("@machineLine", MachineLine));

            // Query sama — ambil 1 row per Model per hari (Actual tertinggi)
            // RunningAssembly diambil dari OEESN untuk hari ini (hari jalan)
            string query = $@"
SELECT d.Id,
       d.tanggal,
       d.Source,
       d.Model,
       '-'  AS Type,
       d.Actual,
       ''   AS Issue,
       ''   AS Remark,
       ISNULL((
           SELECT COUNT(*)
           FROM   PROMOSYS.dbo.OEESN      o
           INNER JOIN PROMOSYS.dbo.MasterData m ON m.Product_Id = o.Product_Id
           WHERE  m.ProductName = d.Model
           AND    CAST(o.Date AS DATE) = CAST(GETDATE() AS DATE)
       ), 0) AS RunningAssembly
FROM (
    SELECT *,
           ROW_NUMBER() OVER (
               PARTITION BY Model, Source, CAST(tanggal AS DATE)
               ORDER BY Actual DESC
           ) AS rn
    FROM   COBADAQ.dbo.DataMatang
    WHERE  MONTH(tanggal) = @month
    AND    YEAR(tanggal)  = @year
    {lineFilter}
) d
WHERE d.rn = 1
ORDER BY d.Model, d.tanggal";

            // Baca flat list dulu, lalu pivot di C#
            var flatList = new List<InventoryData>();

            ExecuteReader(query, reader =>
            {
                flatList.Add(new InventoryData
                {
                    Id = reader.GetInt32(0),
                    Date = reader.GetDateTime(1),
                    Location = reader.GetString(2),
                    Model = reader.GetString(3),
                    Type = reader.GetString(4),
                    Stock = reader.GetInt32(5),
                    Issue = reader.GetString(6),
                    Remark = reader.GetString(7),
                    RunningAssembly = Convert.ToInt32(reader[8])
                });
            }, parameters.ToArray());

            // Pivot: group by Model → Dictionary<day, InventoryData>
            foreach (var row in flatList)
            {
                string modelKey = row.Model;
                int day = row.Date.Day;

                if (!PivotData.ContainsKey(modelKey))
                {
                    PivotData[modelKey] = new Dictionary<int, InventoryData>();
                    ModelList.Add(modelKey);
                }

                // Kalau 1 hari ada duplikat (beda Source), ambil yang Stock-nya lebih besar
                if (!PivotData[modelKey].ContainsKey(day) ||
                    PivotData[modelKey][day].Stock < row.Stock)
                {
                    PivotData[modelKey][day] = row;
                }
            }

            // Sort ModelList A-Z
            ModelList.Sort();
        }

        // ── Helper ────────────────────────────────────────────────────────────
        private void ExecuteReader(string query, Action<SqlDataReader> handleData,
                                   params SqlParameter[] parameters)
        {
            try
            {
                using var connection = new SqlConnection(ConnectionString);
                connection.Open();
                using var command = new SqlCommand(query, connection);
                if (parameters?.Length > 0)
                    command.Parameters.AddRange(parameters);
                using var reader = command.ExecuteReader();
                while (reader.Read())
                    handleData(reader);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading inventory data");
                throw;
            }
        }
    }
}