using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Data.SqlClient;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Globalization;
using OfficeOpenXml;
using MonitoringSystem.Models;
using MonitoringSystem.Data;
using System;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;

namespace MonitoringSystem.Pages.ACOEE.LossTimeReport
{
    public class indexModel : PageModel
    {
        private readonly ApplicationDbContext _context;
        private readonly string _connectionString;
        private readonly IConfiguration _configuration;
        private readonly IWebHostEnvironment _webHostEnvironment;

        private readonly MonitoringSystem.Services.BreakTimeService _breakTimeService;

        public indexModel(ApplicationDbContext context, IConfiguration configuration, IWebHostEnvironment webHostEnvironment, MonitoringSystem.Services.BreakTimeService breakTimeService)
        {
            _context = context;
            _configuration = configuration;
            _connectionString = configuration.GetConnectionString("DefaultConnection");
            _webHostEnvironment = webHostEnvironment;
            _breakTimeService = breakTimeService;
        }
        //public indexModel(ApplicationDbContext context, IConfiguration configuration)
        //{
        //    _context = context;
        //    _connectionString = configuration.GetConnectionString("DefaultConnection");
        //}   
        //public indexModel(ApplicationDbContext context)
        //{
        //    _context = context;
        //}

        private int CalculateBreakOverlapSec(TimeSpan startTime, TimeSpan endTime, List<(TimeSpan Start, TimeSpan End)> breakTimes)
        {
            int totalOverlapSec = 0;
            foreach (var (breakStart, breakEnd) in breakTimes)
            {
                if (startTime < breakEnd && endTime > breakStart)
                {
                    var overlapStart = startTime > breakStart ? startTime : breakStart;
                    var overlapEnd = endTime < breakEnd ? endTime : breakEnd;
                    totalOverlapSec += (int)(overlapEnd - overlapStart).TotalSeconds;
                }
            }
            return totalOverlapSec;
        }

        private List<(TimeSpan Start, TimeSpan End)> GetAllBreakTimes()
        {
            return _breakTimeService.GetBreakTimesForDateAsync(DateTime.Today).Result
                .Select(b => (b.StartTime, b.EndTime))
                .ToList();
        }

        [BindProperty(SupportsGet = true)]
        public int SelectedYear { get; set; } = DateTime.Today.Year;

        // Trend Loss Time AC OEE hanya untuk Expander Kyoshin 635 (MachineLine "Expander"); nilai lain dari URL diabaikan
        private const string OnlyMachineLine = "Expander";

        [BindProperty(SupportsGet = true)]
        public string MachineLine { get; set; } = OnlyMachineLine;

        public override void OnPageHandlerExecuting(Microsoft.AspNetCore.Mvc.Filters.PageHandlerExecutingContext context)
        {
            MachineLine = OnlyMachineLine;
            base.OnPageHandlerExecuting(context);
        }

        [BindProperty]
        public string UploadMachineLine { get; set; }

        [BindProperty]
        public IFormFile UploadedExcel { get; set; }

        public string ChartDataJson { get; set; } = "{}";
        public List<string> Categories { get; set; } = new List<string>();
        public List<string> LegendCategories { get; set; } = new List<string>();
        public Dictionary<string, double[]> DetailActuals { get; set; } = new Dictionary<string, double[]>();
        public Dictionary<string, double[]> DetailPlans { get; set; } = new Dictionary<string, double[]>();

        private static readonly HashSet<string> WorkingLossCategories = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Quality Trouble",
            "Model Change Loss",
            "Material Shortage External",
            "Machine & Tools Trouble",
            "Man Power Adjustment",
            "Material Shortage Inhouse",
            "Material Shortage Internal",
            "Set Repairing Loss",
            "Gawse - External Bodies",
            "Rework",
            "Mold Change Loss"
        };

        // Pilihan "All" di UI berarti gabungan dua line yang tersedia: CU dan CS.
        // Gunakan cakupan line yang sama untuk actual dan BP.
        private static readonly string[] TrendMachineLines = { "MCH1-01", "MCH1-02" };

        // Menampung total Working Loss saja (untuk ringkasan & grafik)
        public double[] TotalActualPerMonth { get; set; } = new double[12];
        public double[] TotalPlanPerMonth { get; set; } = new double[12];
        public double?[] RatioActualVsBp { get; set; } = new double?[12];

        public void OnGet()
        {
            string[] months = { "April", "May", "June", "July", "August", "September", "October", "November", "December", "January", "February", "March" };

            // Pengambilan actual dari logic line lama (CU/CS), termasuk agregasi "All",
            // dinonaktifkan sampai mapping backend untuk dropdown machine baru tersedia.
            // Struktur kategori tetap dikirim ke frontend dengan nilai actual 0.
            var actualsRaw = new List<MonthlyCategoryData>();

            // Pengambilan data BP/plan dinonaktifkan sementara.
            // Koleksi kosong dipertahankan agar struktur tabel dan chart BP di frontend tetap tersedia.
            var plansRaw = new List<MonthlyCategoryData>();

            // Semua kategori untuk Tabel
            var allCats = WorkingLossCategories
                          .Append("Other")
                          .Union(actualsRaw.Select(x => x.Category))
                          .Union(plansRaw.Select(x => x.Category))
                          .Distinct()
                          .ToList();

            Categories = allCats
                .OrderBy(c => {
                    string group = GetCategoryGroup(c);
                    return group == "Working Loss" ? 1 : 2;
                })
                .ThenBy(c => c)
                .ToList();

            // Khusus Legend & Data Grafik (Hanya Working Loss)
            LegendCategories = Categories
                .Where(c => GetCategoryGroup(c) == "Working Loss")
                .ToList();

            foreach (var cat in Categories)
            {
                double[] actArr = new double[12];
                double[] planArr = new double[12];

                var catActuals = actualsRaw.Where(x => x.Category == cat);
                foreach (var item in catActuals)
                {
                    int arrayIndex = (item.Month - 4 + 12) % 12;
                    actArr[arrayIndex] = Math.Round(item.Total, 2);
                }

                var catPlans = plansRaw.Where(x => x.Category == cat);
                foreach (var item in catPlans)
                {
                    int arrayIndex = (item.Month - 4 + 12) % 12;
                    planArr[arrayIndex] = Math.Round(item.Total, 2);
                }

                DetailActuals.Add(cat, actArr);
                DetailPlans.Add(cat, planArr);
            }

            // Hitung Total (HANYA WORKING LOSS)
            var currentMonthStart = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
            for (int i = 0; i < 12; i++)
            {
                TotalActualPerMonth[i] = DetailActuals
                    .Where(x => GetCategoryGroup(x.Key) == "Working Loss")
                    .Sum(x => x.Value[i]);

                TotalPlanPerMonth[i] = DetailPlans
                    .Where(x => GetCategoryGroup(x.Key) == "Working Loss")
                    .Sum(x => x.Value[i]);

                int calendarMonth = i + 4 <= 12 ? i + 4 : i - 8;
                int calendarYear = calendarMonth >= 4 ? SelectedYear : SelectedYear + 1;
                var fiscalMonthStart = new DateTime(calendarYear, calendarMonth, 1);

                // Bulan yang belum berjalan dibuat null agar garis rasio berhenti
                // di bulan sekarang, bukan turun ke angka nol pada bulan berikutnya.
                if (fiscalMonthStart > currentMonthStart)
                {
                    RatioActualVsBp[i] = null;
                    continue;
                }

                RatioActualVsBp[i] = TotalPlanPerMonth[i] > 0
                    ? Math.Round((TotalActualPerMonth[i] / TotalPlanPerMonth[i]) * 100, 2)
                    : null;
            }

            // Kirim ke Frontend: Hanya DetailActuals/DetailPlans yang masuk Working Loss untuk grafik
            var chartPayload = new
            {
                Labels = months,
                LegendCategories = LegendCategories,
                // Filter dictionary agar JS Chart hanya merender Working Loss
                Actuals = DetailActuals.Where(x => LegendCategories.Contains(x.Key)).ToDictionary(x => x.Key, x => x.Value),
                Plans = DetailPlans.Where(x => LegendCategories.Contains(x.Key)).ToDictionary(x => x.Key, x => x.Value),
                RatioActualVsBp = RatioActualVsBp
            };

            ChartDataJson = System.Text.Json.JsonSerializer.Serialize(chartPayload);
        }

        public string GetCategoryGroup(string categoryName)
        {
            if (string.IsNullOrWhiteSpace(categoryName)) return "Working Loss";
            string lowerCat = categoryName.ToLower().Trim();

            if (lowerCat.Contains("break time") || lowerCat.Contains("company activity") ||
                lowerCat.Contains("stock opname") || lowerCat.Contains("maintenance") ||
                lowerCat.Contains("trial run") || lowerCat.Contains("training education") ||
                lowerCat.Contains("free talking") || lowerCat.Contains("no production day") ||
                lowerCat.Contains("morning assembly") || lowerCat.Contains("cleaning") ||
                lowerCat.Contains("general assy"))
            {
                return "Fixed Loss";
            }
            return "Working Loss";
        }

        private string NormalizeCategoryName(string input, bool isPlan = false)
        {
            if (string.IsNullOrWhiteSpace(input)) return "Other";
            string name = input.Trim().ToLower();

            // Samakan dengan halaman Detail Loss Time untuk kategori unknown.
            if (name == "other" || name == "others" || name == "uncategorized")
                return "Other";

            if (name.Contains("change model") || name.Contains("model changing"))
                return "Model Change Loss";

            if (name.Contains("mold changing") || name.Contains("mold change"))
                return "Mold Change Loss";

            if (name.Contains("machine trouble") || name.Contains("machine tools trouble"))
                return "Machine & Tools Trouble";

            // Map all fixed/management loss to Other so they show up under Working Loss
            // HANYA UNTUK ACTUAL DATA. Untuk BP/Plan, JANGAN di-map agar mereka ter-filter keluar oleh GetCategoryGroup!
            if (!isPlan)
            {
                if (name.Contains("break time") || name.Contains("breaktime") || name.Contains("company activity") ||
                    name.Contains("stock opname") || name.Contains("maintenance") ||
                    name.Contains("trial run") || name.Contains("training education") ||
                    name.Contains("free talking") || name.Contains("no production day") ||
                    name.Contains("morning assembly") || name.Contains("cleaning") ||
                    name.Contains("general assy") || name.Contains("others") || name.Contains("fixed loss") || name.Contains("management loss"))
                {
                    return "Other";
                }
            }

            return new CultureInfo("en-US", false).TextInfo.ToTitleCase(name);
        }

        private bool TryNormalizeWorkingLossCategory(string? input, out string category)
        {
            category = string.Empty;
            if (string.IsNullOrWhiteSpace(input)) return false;

            var normalizedCategory = NormalizeCategoryName(input, true);
            if (!WorkingLossCategories.Contains(normalizedCategory)) return false;

            category = normalizedCategory;
            return true;
        }

        private class MonthlyCategoryData
        {
            public int Month { get; set; }
            public string Category { get; set; }
            public double Total { get; set; }
        }

        private List<MonthlyCategoryData> GetDetailedActualData(int fiscalYear, string line)
        {
            var rawList = new List<MonthlyCategoryData>();
            DateTime startDate = new DateTime(fiscalYear, 4, 1);
            DateTime endDate = new DateTime(fiscalYear + 1, 3, 31);

            var actualsQuery = _context.LossTimeActuals.AsNoTracking().Where(x =>
                (x.Year == fiscalYear && x.Month >= 4) ||
                (x.Year == fiscalYear + 1 && x.Month <= 3)
            );
            actualsQuery = line == "All"
                ? actualsQuery.Where(x => TrendMachineLines.Contains(x.MachineLine))
                : actualsQuery.Where(x => x.MachineLine == line);

            // Semua bulan fiscal year
            var allFiscalMonths = new List<int> { 4, 5, 6, 7, 8, 9, 10, 11, 12, 1, 2, 3 };

            // Sumber actual ditentukan per bulan dan per line. Dengan begitu, pada
            // filter All, actual CU tidak menutup fallback data CS (atau sebaliknya).
            var selectedLines = line == "All" ? TrendMachineLines : new[] { line };
            var actualPeriods = actualsQuery
                .Select(x => new { x.Year, x.Month, x.MachineLine })
                .Distinct()
                .ToList();

            var missingPeriods = allFiscalMonths
                .SelectMany(month => selectedLines.Select(machineLine => new
                {
                    Year = month >= 4 ? fiscalYear : fiscalYear + 1,
                    Month = month,
                    MachineLine = machineLine
                }))
                .Where(period => !actualPeriods.Any(actual =>
                    actual.Year == period.Year &&
                    actual.Month == period.Month &&
                    actual.MachineLine == period.MachineLine))
                .Select((period, index) => new
                {
                    period.Year,
                    period.Month,
                    period.MachineLine,
                    ParameterName = $"@MachineCode{index}"
                })
                .ToList();

            // ✅ Ambil dari LossTimeActuals untuk bulan yang sudah ada
            if (actualPeriods.Any())
            {
                var grouped = actualsQuery
                    .GroupBy(x => new { x.Month, x.Category })
                    .Select(g => new
                    {
                        Month = g.Key.Month,
                        Category = g.Key.Category,
                        Total = g.Sum(x => x.Minutes)
                    }).ToList();

                foreach (var item in grouped)
                {
                    rawList.Add(new MonthlyCategoryData
                    {
                        Month = item.Month,
                        Category = NormalizeCategoryName(item.Category),
                        Total = item.Total
                    });
                }
            }

            // ✅ Fallback ke AssemblyLossTime hanya untuk kombinasi bulan-line yang belum ada
            if (missingPeriods.Any())
            {
                var dateConditions = string.Join(" OR ", missingPeriods.Select(period =>
                    $"(YEAR(Date) = {period.Year} AND MONTH(Date) = {period.Month} AND MachineCode = {period.ParameterName})"));

                string query = $@"SELECT MONTH(Date) AS MonthVal, Reason, LossTime,
                                         CAST(Time AS TIME) AS StartTime, CAST(EndDateTime AS TIME) AS EndTime
                                  FROM AssemblyLossTime 
                                  WHERE ({dateConditions})";

                try
                {
                    var breakTimes = GetAllBreakTimes();
                    using (var conn = new SqlConnection(_connectionString))
                    {
                        conn.Open();
                        using (var cmd = new SqlCommand(query, conn))
                        {
                            foreach (var period in missingPeriods)
                            {
                                cmd.Parameters.AddWithValue(period.ParameterName, period.MachineLine);
                            }

                            using (var reader = cmd.ExecuteReader())
                            {
                                while (reader.Read())
                                {
                                    int monthVal = Convert.ToInt32(reader["MonthVal"]);
                                    string reason = reader["Reason"]?.ToString();
                                    int durationSec = reader.IsDBNull(reader.GetOrdinal("LossTime")) ? 0 : Convert.ToInt32(reader["LossTime"]);
                                    
                                    TimeSpan startTime = reader.IsDBNull(reader.GetOrdinal("StartTime")) ? TimeSpan.Zero : reader.GetTimeSpan(reader.GetOrdinal("StartTime"));
                                    TimeSpan endTime = reader.IsDBNull(reader.GetOrdinal("EndTime")) ? TimeSpan.Zero : reader.GetTimeSpan(reader.GetOrdinal("EndTime"));
                                    
                                    if (endTime < startTime) endTime = endTime.Add(TimeSpan.FromDays(1));
                                    
                                    // LOGIC SAMAKAN DENGAN DETAIL LOSS (Overlap Break Time)
                                    int overlapSec = CalculateBreakOverlapSec(startTime, endTime, breakTimes);
                                    durationSec -= overlapSec;

                                    if (durationSec <= 0) continue;

                                    rawList.Add(new MonthlyCategoryData
                                    {
                                        Month = monthVal,
                                        Category = NormalizeCategoryName(reason),
                                        Total = durationSec / 60.0
                                    });
                                }
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Fallback error: {ex.Message}");
                }
            }

            // Gabungkan dan group ulang jika ada duplikat kategori
            return rawList
                .GroupBy(x => new { x.Month, x.Category })
                .Select(g => new MonthlyCategoryData
                {
                    Month = g.Key.Month,
                    Category = g.Key.Category,
                    Total = g.Sum(x => x.Total)
                }).ToList();
        }

        private Dictionary<int, double> GetMonthlyWorkingTime(int fiscalYear, string line)
        {
            var result = new Dictionary<int, double>();
            try
            {
                for (int m = 1; m <= 12; m++)
                {
                    var pr = new MonitoringSystem.Pages.ProductionReport.IndexModel(_webHostEnvironment, _configuration, _breakTimeService);
                    pr.SelectedYear = m >= 4 ? fiscalYear : fiscalYear + 1;
                    pr.SelectedMonth = m;
                    pr.MachineLine = line;
                    pr.SelectedShifts = new List<string> { "All" };
                    
                    // Untuk rasio Trend hanya DailyWorkTime yang dibutuhkan.
                    // Filter tahun dan line tetap diterapkan oleh model ini.
                    pr.LoadChartData(loadSupportingData: false);
                    
                    double totalWorkMinutes = pr.DailyWorkTime.Sum();
                    result.Add(m, totalWorkMinutes);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error getting Monthly Working Time from ProductionReport Logic: " + ex.Message);
            }
            return result;
        }

        public async Task<IActionResult> OnPostImportExcelAsync()
        {
            if (UploadedExcel == null || UploadedExcel.Length == 0)
            {
                TempData["Error"] = "File Excel BP belum dipilih.";
                return RedirectToPage(new { SelectedYear, MachineLine });
            }

            ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
            try
            {
                using (var stream = new MemoryStream())
                {
                    await UploadedExcel.CopyToAsync(stream);
                    using (var package = new ExcelPackage(stream))
                    {
                        var sheet = package.Workbook.Worksheets[0];
                        if (sheet.Dimension == null)
                        {
                            TempData["Error"] = "Sheet Excel BP kosong.";
                            return RedirectToPage(new { SelectedYear, MachineLine });
                        }

                        var newPlans = new List<LossTimePlan>();
                        for (int row = 4; row <= sheet.Dimension.Rows; row++)
                        {
                            var rawCategory = sheet.Cells[row, 2].Text?.Trim();

                            // Hanya 11 kategori Working Loss pada template yang boleh disimpan.
                            // Header, baris kosong, Fixed Loss, dan kategori lain dilewati.
                            if (!TryNormalizeWorkingLossCategory(rawCategory, out var catName)) continue;

                            int[] months = { 4, 5, 6, 7, 8, 9, 10, 11, 12, 1, 2, 3 };
                            int col = 3;
                            foreach (var m in months)
                            {
                                double.TryParse(sheet.Cells[row, col].Text, out double tVal);
                                decimal.TryParse(sheet.Cells[row, col + 1].Text, out decimal rVal);
                                newPlans.Add(new LossTimePlan
                                {
                                    Category = catName,
                                    MachineLine = UploadMachineLine,
                                    Month = m,
                                    Year = m >= 4 ? SelectedYear : SelectedYear + 1,
                                    TargetMinutes = tVal,
                                    Ratio = rVal * 100
                                });
                                col += 2;
                            }
                        }
                        var old = _context.LossTimePlans.Where(x => x.MachineLine == UploadMachineLine &&
                            ((x.Year == SelectedYear && x.Month >= 4) || (x.Year == SelectedYear + 1 && x.Month <= 3)));
                        _context.LossTimePlans.RemoveRange(old);
                        _context.LossTimePlans.AddRange(newPlans);
                        await _context.SaveChangesAsync();

                        TempData["Success"] = $"BP berhasil di-import: {newPlans.Count} data dari 11 kategori Working Loss. Fixed Loss tidak disimpan.";
                    }
                }
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Import BP gagal: {ex.Message}";
            }
            return RedirectToPage(new { SelectedYear, MachineLine });
        }

        public IActionResult OnGetDownloadTemplate()
        {
            var filePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "data", "planlosstime", "LossTimePlan_Template.xlsx");
            return System.IO.File.Exists(filePath) ? File(System.IO.File.ReadAllBytes(filePath), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "LossTimePlan_Template.xlsx") : (IActionResult)NotFound();
        }
    }
}
