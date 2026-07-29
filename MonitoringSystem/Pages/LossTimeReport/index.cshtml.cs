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

namespace MonitoringSystem.Pages.LossTimeReport
{
    public class indexModel : PageModel
    {
        private readonly ApplicationDbContext _context;
        private readonly string _connectionString;
        private readonly IConfiguration _configuration;
        private readonly IWebHostEnvironment _webHostEnvironment;

        public indexModel(ApplicationDbContext context, IConfiguration configuration, IWebHostEnvironment webHostEnvironment)
        {
            _context = context;
            _configuration = configuration;
            _connectionString = configuration.GetConnectionString("DefaultConnection");
            _webHostEnvironment = webHostEnvironment;
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

        [BindProperty(SupportsGet = true)]
        public int SelectedYear { get; set; } = DateTime.Today.Year;

        [BindProperty(SupportsGet = true)]
        public string MachineLine { get; set; } = "All";

        [BindProperty]
        public string UploadMachineLine { get; set; }

        [BindProperty]
        public IFormFile UploadedExcel { get; set; }

        public string ChartDataJson { get; set; } = "{}";
        public List<string> Categories { get; set; } = new List<string>();
        public List<string> LegendCategories { get; set; } = new List<string>();
        public Dictionary<string, double[]> DetailActuals { get; set; } = new Dictionary<string, double[]>();
        public Dictionary<string, double[]> DetailPlans { get; set; } = new Dictionary<string, double[]>();

        // Menampung total Working Loss saja (untuk ringkasan & grafik)
        public double[] TotalActualPerMonth { get; set; } = new double[12];
        public double[] TotalPlanPerMonth { get; set; } = new double[12];
        public double[] RatioLossVsWt { get; set; } = new double[12];

        public void OnGet()
        {
            string[] months = { "January", "February", "March", "April", "May", "June", "July", "August", "September", "October", "November", "December" };

            var actualsRaw = GetDetailedActualData(SelectedYear, MachineLine);

            var planQuery = _context.LossTimePlans.AsQueryable();
            planQuery = planQuery.Where(x => x.Year == SelectedYear);

            if (MachineLine != "All") planQuery = planQuery.Where(x => x.MachineLine == MachineLine);

            var plansRaw = planQuery.ToList()
                .GroupBy(x => new { Category = NormalizeCategoryName(x.Category), Month = x.Month })
                .Select(g => new { Category = g.Key.Category, Month = g.Key.Month, Total = g.Sum(x => x.TargetMinutes) })
                .ToList();

            var plansRatioRaw = planQuery.ToList()
                .GroupBy(x => x.Month)
                .Select(g => new { Month = g.Key, RatioVal = g.Max(x => x.Ratio) })
                .ToList();

            var workingTimeRaw = GetMonthlyWorkingTime(SelectedYear, MachineLine);

            // Semua kategori untuk Tabel
            var allCats = actualsRaw.Select(x => x.Category)
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
                    int arrayIndex = item.Month - 1;
                    actArr[arrayIndex] = Math.Round(item.Total, 1);
                }

                var catPlans = plansRaw.Where(x => x.Category == cat);
                foreach (var item in catPlans)
                {
                    int arrayIndex = item.Month - 1;
                    planArr[arrayIndex] = Math.Round(item.Total, 1);
                }

                DetailActuals.Add(cat, actArr);
                DetailPlans.Add(cat, planArr);
            }

            // Hitung Total (HANYA WORKING LOSS)
            for (int i = 0; i < 12; i++)
            {
                TotalActualPerMonth[i] = DetailActuals
                    .Where(x => GetCategoryGroup(x.Key) == "Working Loss")
                    .Sum(x => x.Value[i]);

                TotalPlanPerMonth[i] = DetailPlans
                    .Where(x => GetCategoryGroup(x.Key) == "Working Loss")
                    .Sum(x => x.Value[i]);

                int monthNum = i + 1;

                double workingTime = workingTimeRaw.ContainsKey(monthNum) ? workingTimeRaw[monthNum] : 0;
                if (workingTime > 0)
                {
                    RatioLossVsWt[i] = Math.Round((TotalActualPerMonth[i] / workingTime) * 100, 2);
                }
            }

            // Kirim ke Frontend: Hanya DetailActuals/DetailPlans yang masuk Working Loss untuk grafik
            var chartPayload = new
            {
                Labels = months,
                LegendCategories = LegendCategories,
                // Filter dictionary agar JS Chart hanya merender Working Loss
                Actuals = DetailActuals.Where(x => LegendCategories.Contains(x.Key)).ToDictionary(x => x.Key, x => x.Value),
                Plans = DetailPlans.Where(x => LegendCategories.Contains(x.Key)).ToDictionary(x => x.Key, x => x.Value),
                RatioLossVsWt = RatioLossVsWt
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

        private string NormalizeCategoryName(string input)
        {
            if (string.IsNullOrWhiteSpace(input)) return "Uncategorized";
            string name = input.Trim().ToLower();

            // Sesuai request: changing -> change
            if (name.Contains("change model") || name.Contains("model changing"))
                return "Model Change Loss";

            if (name.Contains("mold changing") || name.Contains("mold change"))
                return "Mold Change Loss";

            if (name.Contains("machine trouble") || name.Contains("machine tools trouble"))
                return "Machine & Tools Trouble";

            if (name.Contains("break time") || name.Contains("breaktime"))
                return "Break Time";

            return new CultureInfo("en-US", false).TextInfo.ToTitleCase(name);
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
            DateTime startDate = new DateTime(fiscalYear, 1, 1);
            DateTime endDate = new DateTime(fiscalYear, 12, 31);

            var actualsQuery = _context.LossTimeActuals.Where(x => x.Year == fiscalYear);
            if (line != "All") actualsQuery = actualsQuery.Where(x => x.MachineLine == line);

            // Cek bulan mana yang sudah ada di LossTimeActuals
            var monthsWithActuals = actualsQuery.Select(x => x.Month).Distinct().ToList();

            // Semua bulan fiscal year
            var allFiscalMonths = new List<int> { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12 };

            // Bulan yang BELUM ada di LossTimeActuals → fallback
            var monthsMissing = allFiscalMonths.Where(m => !monthsWithActuals.Contains(m)).ToList();

            // ✅ Ambil dari LossTimeActuals untuk bulan yang sudah ada
            if (monthsWithActuals.Any())
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

            // ✅ Fallback ke AssemblyLossTime untuk bulan yang BELUM ada
            if (monthsMissing.Any())
            {
                var dateConditions = string.Join(" OR ", monthsMissing.Select(m =>
                {
                    return $"(YEAR(Date) = {fiscalYear} AND MONTH(Date) = {m})";
                }));

                string query = $@"SELECT MONTH(Date) AS MonthVal, Reason, 
                                 SUM(LossTime) / 60.0 AS TotalMinutes
                          FROM AssemblyLossTime 
                          WHERE ({dateConditions})";

                if (line != "All") query += " AND MachineCode = @MachineCode";
                query += " GROUP BY MONTH(Date), Reason";

                try
                {
                    using (var conn = new SqlConnection(_connectionString))
                    {
                        conn.Open();
                        using (var cmd = new SqlCommand(query, conn))
                        {
                            if (line != "All") cmd.Parameters.AddWithValue("@MachineCode", line);
                            using (var reader = cmd.ExecuteReader())
                            {
                                while (reader.Read())
                                {
                                    rawList.Add(new MonthlyCategoryData
                                    {
                                        Month = Convert.ToInt32(reader["MonthVal"]),
                                        Category = NormalizeCategoryName(reader["Reason"]?.ToString()),
                                        Total = Convert.ToDouble(reader["TotalMinutes"])
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
                    var pr = new MonitoringSystem.Pages.ProductionReport.IndexModel(_webHostEnvironment, _configuration);
                    pr.SelectedYear = fiscalYear;
                    pr.SelectedMonth = m;
                    pr.MachineLine = line;
                    pr.SelectedShifts = new List<string> { "All" };
                    
                    // Panggil LoadChartData yang akan memproses DailyWorkTime
                    pr.LoadChartData();
                    
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
            if (UploadedExcel == null || UploadedExcel.Length == 0) return RedirectToPage();
            ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
            try
            {
                using (var stream = new MemoryStream())
                {
                    await UploadedExcel.CopyToAsync(stream);
                    using (var package = new ExcelPackage(stream))
                    {
                        var sheet = package.Workbook.Worksheets[0];
                        var newPlans = new List<LossTimePlan>();
                        for (int row = 4; row <= sheet.Dimension.Rows; row++)
                        {
                            var catName = NormalizeCategoryName(sheet.Cells[row, 2].Text);
                            if (string.IsNullOrEmpty(catName) || catName.Contains("Total")) continue;
                            int[] months = { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12 };
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
                                    Year = SelectedYear,
                                    TargetMinutes = tVal,
                                    Ratio = rVal * 100
                                });
                                col += 2;
                            }
                        }
                        var old = _context.LossTimePlans.Where(x => x.MachineLine == UploadMachineLine && x.Year == SelectedYear);
                        _context.LossTimePlans.RemoveRange(old);
                        _context.LossTimePlans.AddRange(newPlans);
                        await _context.SaveChangesAsync();
                    }
                }
            }
            catch { }
            return RedirectToPage(new { SelectedYear, MachineLine });
        }

        public IActionResult OnGetDownloadTemplate()
        {
            var filePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "data", "planlosstime", "LossTimePlan_Template.xlsx");
            return System.IO.File.Exists(filePath) ? File(System.IO.File.ReadAllBytes(filePath), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "LossTimePlan_Template.xlsx") : (IActionResult)NotFound();
        }
    }
}