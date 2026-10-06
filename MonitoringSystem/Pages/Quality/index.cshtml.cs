using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Data.SqlClient;
using System.Collections.Generic;
using System;
namespace MonitoringSystem.Pages.Quality
{
    public class QualityModel : PageModel
    {
        public string connectionString = "Server=10.83.33.103;User Id=sa;Password=sa;Database=PROMOSYS;Trusted_Connection=False;TrustServerCertificate=True;Encrypt=False;MultipleActiveResultSets=True";

        public int TotalPlan { get; set; }
        public int DefectQuantity { get; set; }
        public double DefectRatio { get; set; }
        public string errorMessage = "";

        public QualityDefectCatalog.LineDefinition Line { get; private set; } = QualityDefectCatalog.CU;

        // Pie: jumlah defect per station group (selalu semua station pada rentang tanggal).
        public List<StationQuantity> StationBreakdown { get; set; }

        // Defect Causes: [stationKey | "ALL"][categoryKey] -> total + top item.
        public Dictionary<string, Dictionary<string, CategoryCauses>> CausesByStation { get; set; }

        public List<YearlyDefectData> YearlyDefects { get; set; }

        [BindProperty(SupportsGet = true)]
        public string MachineCode { get; set; }

        [BindProperty(SupportsGet = true)]
        public string StartDate { get; set; }

        [BindProperty(SupportsGet = true)]
        public string EndDate { get; set; }
        public double CurrentTargetRatio { get; set; }
        [BindProperty]
        public double NewTargetRatio { get; set; }

        [BindProperty(SupportsGet = true)]
        public string Station { get; set; }

        public int ChartYear { get; private set; }

        public QualityModel()
        {
            StationBreakdown = new List<StationQuantity>();
            CausesByStation = new Dictionary<string, Dictionary<string, CategoryCauses>>();
            YearlyDefects = new List<YearlyDefectData>();
        }

        public async Task<IActionResult> OnPostUpdateTargetRatioAsync()
        {
            ModelState.Remove("MachineCode");
            ModelState.Remove("StartDate");
            ModelState.Remove("EndDate");
            ModelState.Remove("Station");

            if (ModelState.IsValid)
            {
                try
                {
                    using (SqlConnection connection = new SqlConnection(connectionString))
                    {
                        await connection.OpenAsync();
                        string insertQuery = "INSERT INTO TargetRatioDefect.dbo.TargetRatio (Ratio) VALUES (@NewRatio)";
                        using (SqlCommand command = new SqlCommand(insertQuery, connection))
                        {
                            command.Parameters.AddWithValue("@NewRatio", NewTargetRatio);
                            await command.ExecuteNonQueryAsync();
                        }
                    }
                }
                catch (Exception ex)
                {
                    errorMessage = "Database error while updating target ratio: " + ex.Message;
                    Console.WriteLine(errorMessage);
                    return Page();
                }
            }
            return RedirectToPage(new { MachineCode, StartDate, EndDate, Station });
        }

        public void OnGet()
        {
            if (string.IsNullOrEmpty(StartDate))
            {
                StartDate = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1).ToString("yyyy-MM-dd");
                EndDate = DateTime.Now.ToString("yyyy-MM-dd");
            }
            LoadData();
        }

        public void OnPost()
        {
            LoadData();
        }

        private void LoadData()
        {
            Line = QualityDefectCatalog.ResolveLine(MachineCode);
            MachineCode = Line.MachineCode;
            if (!string.IsNullOrEmpty(Station) && !Line.Stations.Any(s => s.Key == Station) && Station != QualityDefectCatalog.OthersKey)
            {
                Station = "";
            }

            TotalPlan = 0;
            DefectQuantity = 0;
            DefectRatio = 100;
            StationBreakdown.Clear();
            CausesByStation.Clear();
            YearlyDefects.Clear();

            DateTime startDateParsed, endDateParsed;
            if (!DateTime.TryParse(StartDate, out startDateParsed))
            {
                startDateParsed = DateTime.Now.Date;
            }
            if (!DateTime.TryParse(EndDate, out endDateParsed))
            {
                endDateParsed = DateTime.Now.Date;
            }
            ChartYear = startDateParsed.Year;

            var rangeRows = new List<DefectRow>();
            var yearRows = new List<DefectRow>();

            try
            {
                using (SqlConnection connection = new SqlConnection(connectionString))
                {
                    connection.Open();

                    string getTargetRatio = "SELECT TOP 1 Ratio FROM TargetRatioDefect.dbo.TargetRatio ORDER BY ID DESC";
                    using (SqlCommand command = new SqlCommand(getTargetRatio, connection))
                    {
                        var result = command.ExecuteScalar();
                        if (result != DBNull.Value && result != null)
                        {
                            CurrentTargetRatio = Convert.ToDouble(result);
                        }
                    }

                    string getTotalProduction = @"
                    SELECT
                         COUNT(TotalUnit)
                    FROM
                        OEESN
                    WHERE
                        MachineCode = @MachineCode
                       AND CAST(Date AS DATE) BETWEEN @StartDate AND @EndDate;";

                    using (SqlCommand command = new SqlCommand(getTotalProduction, connection))
                    {
                        command.Parameters.AddWithValue("@MachineCode", MachineCode);
                        command.Parameters.AddWithValue("@StartDate", startDateParsed);
                        command.Parameters.AddWithValue("@EndDate", endDateParsed);
                        var result = command.ExecuteScalar();
                        if (result != DBNull.Value && result != null)
                        {
                            TotalPlan = Convert.ToInt32(result);
                        }
                    }

                    // Station/kategori dikelompokkan di C# (lihat QualityDefectCatalog),
                    // jadi query cukup mengambil agregat mentah.
                    string getRangeDefects = @"
                    SELECT
                        Station,
                        Cause,
                        Detail,
                        COUNT(*) AS DefectCount
                    FROM
                        NG_RPTS
                    WHERE
                        MachineCode = @MachineCode
                        AND CAST(SDate AS DATE) BETWEEN @StartDate AND @EndDate
                    GROUP BY
                        Station, Cause, Detail;";

                    using (SqlCommand command = new SqlCommand(getRangeDefects, connection))
                    {
                        command.Parameters.AddWithValue("@MachineCode", MachineCode);
                        command.Parameters.AddWithValue("@StartDate", startDateParsed);
                        command.Parameters.AddWithValue("@EndDate", endDateParsed);
                        using (SqlDataReader reader = command.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                rangeRows.Add(new DefectRow
                                {
                                    Station = reader.IsDBNull(0) ? null : reader.GetString(0),
                                    Cause = reader.IsDBNull(1) ? null : reader.GetString(1),
                                    Detail = reader.IsDBNull(2) ? null : reader.GetString(2),
                                    Quantity = reader.GetInt32(3)
                                });
                            }
                        }
                    }

                    string getYearlyDefects = @"
                    SELECT
                        MONTH(SDate) AS MonthNumber,
                        Station,
                        Cause,
                        COUNT(*) AS DefectCount
                    FROM
                        NG_RPTS
                    WHERE
                        MachineCode = @MachineCode
                        AND YEAR(SDate) = YEAR(@StartDate)
                    GROUP BY
                        MONTH(SDate), Station, Cause;";

                    using (SqlCommand command = new SqlCommand(getYearlyDefects, connection))
                    {
                        command.Parameters.AddWithValue("@MachineCode", MachineCode);
                        command.Parameters.AddWithValue("@StartDate", startDateParsed);
                        using (SqlDataReader reader = command.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                yearRows.Add(new DefectRow
                                {
                                    Month = reader.GetInt32(0),
                                    Station = reader.IsDBNull(1) ? null : reader.GetString(1),
                                    Cause = reader.IsDBNull(2) ? null : reader.GetString(2),
                                    Quantity = reader.GetInt32(3)
                                });
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                errorMessage = "Database error: " + ex.Message;
                Console.WriteLine(errorMessage);
            }

            foreach (var row in rangeRows.Concat(yearRows))
            {
                row.StationKey = QualityDefectCatalog.ResolveStation(Line, row.Station).Key;
            }

            bool MatchesStation(DefectRow row) => string.IsNullOrEmpty(Station) || row.StationKey == Station;

            DefectQuantity = rangeRows.Where(MatchesStation).Sum(r => r.Quantity);

            foreach (var station in Line.Stations)
            {
                StationBreakdown.Add(new StationQuantity
                {
                    Key = station.Key,
                    Label = station.Label,
                    Quantity = rangeRows.Where(r => r.StationKey == station.Key).Sum(r => r.Quantity)
                });
            }
            var othersQty = rangeRows.Where(r => r.StationKey == QualityDefectCatalog.OthersKey).Sum(r => r.Quantity);
            if (othersQty > 0)
            {
                StationBreakdown.Add(new StationQuantity { Key = QualityDefectCatalog.OthersKey, Label = QualityDefectCatalog.OthersLabel, Quantity = othersQty });
            }

            CausesByStation["ALL"] = BuildCauses(rangeRows);
            foreach (var station in StationBreakdown)
            {
                CausesByStation[station.Key] = BuildCauses(rangeRows.Where(r => r.StationKey == station.Key));
            }

            YearlyDefects = yearRows
                .Where(MatchesStation)
                .GroupBy(r => new { r.Month, Cause = QualityDefectCatalog.ItemLabel(r.Cause, null) })
                .Select(g => new YearlyDefectData { Month = g.Key.Month, Cause = g.Key.Cause, Quantity = g.Sum(r => r.Quantity) })
                .OrderBy(d => d.Month)
                .ThenByDescending(d => d.Quantity)
                .ToList();

            if (TotalPlan > 0)
            {
                DefectRatio = (1 - (double)DefectQuantity / TotalPlan) * 100;
            }
        }

        private Dictionary<string, CategoryCauses> BuildCauses(IEnumerable<DefectRow> rows)
        {
            var byCategory = rows
                .GroupBy(r => QualityDefectCatalog.ResolveCategory(Line, r.Cause, r.Detail).Key)
                .ToDictionary(g => g.Key, g => g.ToList());

            var result = new Dictionary<string, CategoryCauses>();
            foreach (var category in Line.Categories)
            {
                var categoryRows = byCategory.TryGetValue(category.Key, out var list) ? list : new List<DefectRow>();
                result[category.Key] = new CategoryCauses
                {
                    Total = categoryRows.Sum(r => r.Quantity),
                    Items = categoryRows
                        .GroupBy(r => QualityDefectCatalog.ItemLabel(r.Detail, r.Cause).ToUpperInvariant())
                        .Select(g => new DailyDefect
                        {
                            Cause = QualityDefectCatalog.ItemLabel(g.First().Detail, g.First().Cause),
                            Quantity = g.Sum(r => r.Quantity)
                        })
                        .OrderByDescending(d => d.Quantity)
                        .Take(5)
                        .ToList()
                };
            }
            return result;
        }

        private class DefectRow
        {
            public int Month { get; set; }
            public string? Station { get; set; }
            public string? Cause { get; set; }
            public string? Detail { get; set; }
            public int Quantity { get; set; }
            public string StationKey { get; set; } = "";
        }

        public class DailyDefect
        {
            public string Cause { get; set; }
            public int Quantity { get; set; }
        }

        public class StationQuantity
        {
            public string Key { get; set; } = "";
            public string Label { get; set; } = "";
            public int Quantity { get; set; }
        }

        public class CategoryCauses
        {
            public int Total { get; set; }
            public List<DailyDefect> Items { get; set; } = new();
        }

        public class YearlyDefectData
        {
            public int Month { get; set; }
            public string Cause { get; set; }
            public int Quantity { get; set; }
        }
    }
}
