using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Data.SqlClient;
using ClosedXML.Excel;
using MonitoringSystem.Data;
using System.Text.Json;
using MonitoringSystem.Models;
using OfficeOpenXml;
using System.Globalization;
//using DocumentFormat.OpenXml.Drawing;

namespace MonitoringSystem.Pages.LossTime
{
    public class IndexModel : PageModel
    {
        private readonly ApplicationDbContext _context;

        public IndexModel(ApplicationDbContext context, IConfiguration configuration, IWebHostEnvironment webHostEnvironment)
        {
            _context = context;
            _configuration = configuration;
            connectionString = _configuration.GetConnectionString("DefaultConnection") ?? "";
            machineConnectionString = _configuration.GetConnectionString("MachineConnection") ?? "";
            _webHostEnvironment = webHostEnvironment;
        }

        private string connectionString;
        private string machineConnectionString;
        private readonly IConfiguration _configuration;
        private readonly IWebHostEnvironment _webHostEnvironment;

        public List<LossTimeRecord> LossTimeData { get; set; } = new List<LossTimeRecord>();
        public int TotalDuration { get; set; }
        public int CurrentPage { get; set; } = 1;
        public int PageSize { get; set; } = 10;
        public int TotalPages => (int)Math.Ceiling((double)TotalRecords / PageSize);
        public int TotalRecords { get; set; }
        public bool HasDataToDisplay => TotalRecords > 0;

        [BindProperty] public DateTime StartSelectedDate { get; set; } = DateTime.Today;
        [BindProperty] public DateTime EndSelectedDate { get; set; } = DateTime.Today;
        [BindProperty] public int SelectedMonth { get; set; } = DateTime.Today.Month;
        [BindProperty] public int SelectedYear { get; set; } = DateTime.Today.Year;
        [BindProperty] public int TargetYear { get; set; } = DateTime.Today.Year;
        [BindProperty] public int TargetMonth { get; set; } = DateTime.Today.Month;
        [BindProperty] public string MachineLine { get; set; } = "All";
        [BindProperty] public string SelectedSource { get; set; } = "Assembly";
        [BindProperty] public string SelectedMachineName { get; set; } = "All";
        [BindProperty] public List<string> SelectedShifts { get; set; } = new List<string> { "1", "2", "3" };
        [BindProperty] public int SelectedPageSize { get; set; } = 10;
        [BindProperty] public string AdditionalBreakTime1Start { get; set; } = "";
        [BindProperty] public string AdditionalBreakTime1End { get; set; } = "";
        [BindProperty] public string AdditionalBreakTime2Start { get; set; } = "";
        [BindProperty] public string AdditionalBreakTime2End { get; set; } = "";
        [BindProperty] public IFormFile UploadedExcel { get; set; }
        [BindProperty] public string UploadMachineLine { get; set; }

        public bool IsFiltering { get; set; } = false;
        public Dictionary<string, int> CategorySummary { get; set; } = new Dictionary<string, int>();
        public string ChartDataJson { get; set; }
        public string DailyChartDataJson { get; set; }

        // ? NEW: MTT sub-category daily chart data
        public string MttDailyChartDataJson { get; set; }

        public List<string> MachineNameList { get; set; } = new List<string>();
        public List<LossTimeRecord> AllMttRecords { get; set; } = new List<LossTimeRecord>();

        public List<string> AllCategories { get; set; } = new List<string>
        {
            "Model Change Loss",
            "Mold Change Loss",
            "Gawse - External Bodies",
            "Material Shortage External",
            "Material Shortage Internal",
            "Material Shortage Inhouse",
            "Man Power Adjustment",
            "Quality Trouble",
            "Machine & Tools Trouble",
            "Set Repairing Loss",
            "Rework",
            "General Assembly",
            "Loss Awal Hari",
            "Morning Assembly",
            "Other"
        };

        public Dictionary<string, string> CategoryAbbreviations = new()
        {
            { "Model Change Loss",          "Change Model" },
            { "Mold Change Loss",           "Mold Change" },
            { "Gawse - External Bodies",    "Gawse Ext" },
            { "Material Shortage External", "Mtrl Shortage Ex" },
            { "Man Power Adjustment",       "MP Adjust" },
            { "Material Shortage Internal", "Mtrl Shortage Int" },
            { "Material Shortage Inhouse",  "Mtrl Shortage Inhs" },
            { "Quality Trouble",            "Quality Trouble" },
            { "Machine & Tools Trouble",    "M/C Trouble" },
            { "Set Repairing Loss",         "Set Repair" },
            { "Rework",                     "Rework" },
            { "General Assembly",           "General Assy" },
            { "Loss Awal Hari",             "Loss Awal Hari" },
            { "Morning Assembly",           "Morning Assy" },
            { "Other",                      "Other" }
        };

        public Dictionary<string, string> CategoryFullNames = new()
        {
            { "Model Change Loss",          "Model Change Loss" },
            { "Mold Change Loss",           "Mold Change Loss" },
            { "Gawse - External Bodies",    "Gawse - External Bodies" },
            { "Material Shortage External", "Material Shortage External" },
            { "Man Power Adjustment",       "Man Power Adjustment" },
            { "Material Shortage Internal", "Material Shortage Internal" },
            { "Material Shortage Inhouse",  "Material Shortage Inhouse" },
            { "Quality Trouble",            "Quality Trouble" },
            { "Machine & Tools Trouble",    "Machine & Tools Trouble" },
            { "Set Repairing Loss",         "Set Repairing Loss" },
            { "Rework",                     "Rework" },
            { "General Assembly",           "General Assembly" },
            { "Loss Awal Hari",             "Loss Awal Hari" },
            { "Morning Assembly",           "Morning Assembly" },
            { "Other",                      "Other" }
        };

        private readonly Dictionary<string, string> CategoryColors = new Dictionary<string, string>
        {
            { "Model Change Loss",          "#FF6384" },
            { "Mold Change Loss",           "#FF8FAB" },
            { "Gawse - External Bodies",    "#36A2EB" },
            { "Material Shortage External", "#1A6EBF" },
            { "Man Power Adjustment",       "#FFCE56" },
            { "Material Shortage Internal", "#4BC0C0" },
            { "Material Shortage Inhouse",  "#9966FF" },
            { "Quality Trouble",            "#FF9F40" },
            { "Machine & Tools Trouble",    "#C9CBCF" },
            { "Set Repairing Loss",         "#A0A0A0" },
            { "Rework",                     "#FF9F80" },
            { "General Assembly",           "#198754" },
            { "Loss Awal Hari",             "#20C997" },
            { "Morning Assembly",           "#0D6EFD" },
            { "Other",                      "#77DD77" }
        };

        // ? NEW: MTT sub-category colors palette
        private readonly Dictionary<string, string> MttSubCategoryColors = new Dictionary<string, string>
        {
            { "Scanner FM CU",              "#FF6384" },
            { "Scanner Comp CU",            "#FF8FAB" },
            { "Scanner Robot CU",           "#FF4500" },
            { "Scanner Nameplate CU",       "#FF6347" },
            { "Scanner Label CU",           "#FF7F50" },
            { "Scanner Final CU",           "#FF8C00" },
            { "Scanner FM CS",              "#36A2EB" },
            { "Scanner Comp CS",            "#1A6EBF" },
            { "Scanner Robot CS",           "#4169E1" },
            { "Scanner Nameplate CS",       "#6495ED" },
            { "Scanner Label CS",           "#87CEEB" },
            { "Scanner Final CS",           "#00BFFF" },
            { "Bending Condensor Reguler",  "#FFCE56" },
            { "Straping Band",              "#FFD700" },
            { "Bending Condensor Bigcap",   "#FFA500" },
            { "Driver",                     "#9966FF" },
            { "Gas Charge",                 "#BA55D3" },
            { "Running Trip",               "#DDA0DD" },
            { "Vaccum",                     "#4BC0C0" },
            { "Conveyor",                   "#20C997" },
            { "Lifter Prouduct",            "#198754" },
            { "Laser",                      "#C9CBCF" },
        };

        private readonly List<(TimeSpan Start, TimeSpan End)> FixedBreakTimes = new List<(TimeSpan, TimeSpan)>
        {
            (new TimeSpan(7, 0, 0),  new TimeSpan(7, 5, 0)),
            (new TimeSpan(9, 30, 0), new TimeSpan(9, 35, 0)),
            (new TimeSpan(15, 30, 0),new TimeSpan(15, 35, 0)),
            (new TimeSpan(18, 15, 0),new TimeSpan(18, 45, 0)),
        };


        public void OnGet(int pageNumber = 1, int pageSize = 10)
        {
            CurrentPage = pageNumber;
            PageSize = pageSize;
            SelectedPageSize = pageSize;
            SetDatesFromMonthYear();
            LoadBreakTimeForToday();
            LoadMachineNameList();
            LoadData();
        }

        public void SetDatesFromMonthYear()
        {
            StartSelectedDate = new DateTime(SelectedYear, SelectedMonth, 1);
            EndSelectedDate = StartSelectedDate.AddMonths(1).AddDays(-1);
        }

        public IActionResult OnPostFilter()
        {
            _cachedBreakTimes = null;
            CurrentPage = 1;
            PageSize = SelectedPageSize;
            SetDatesFromMonthYear();
            if (SelectedShifts == null || !SelectedShifts.Any())
                SelectedShifts = new List<string> { "1", "2", "3" };
            LoadBreakTimeForToday();
            LoadMachineNameList();
            IsFiltering = true;
            LoadData();
            return Page();
        }

        public IActionResult OnPostChangePage(int pageNumber, int pageSize, int selectedMonth, int selectedYear,
            string machineLine, List<string> selectedShifts,
            string additionalBreakTime1Start, string additionalBreakTime1End,
            string additionalBreakTime2Start, string additionalBreakTime2End,
            string selectedSource = "Assembly", string selectedMachineName = "All")
        {
            CurrentPage = pageNumber;
            PageSize = pageSize;
            SelectedMonth = selectedMonth;
            SelectedYear = selectedYear;
            SetDatesFromMonthYear();
            MachineLine = machineLine;
            SelectedShifts = selectedShifts ?? new List<string> { "1", "2", "3" };
            AdditionalBreakTime1Start = additionalBreakTime1Start;
            AdditionalBreakTime1End = additionalBreakTime1End;
            AdditionalBreakTime2Start = additionalBreakTime2Start;
            AdditionalBreakTime2End = additionalBreakTime2End;
            SelectedSource = selectedSource;
            SelectedMachineName = selectedMachineName;
            LoadMachineNameList();
            LoadData();
            return Page();
        }

        public IActionResult OnPostReset()
        {
            _cachedBreakTimes = null;
            ModelState.Clear();
            SelectedMonth = DateTime.Today.Month;
            SelectedYear = DateTime.Today.Year;
            SetDatesFromMonthYear();
            MachineLine = "All";
            SelectedShifts = new List<string> { "1", "2", "3" };
            SelectedPageSize = 10;
            PageSize = 10;
            IsFiltering = false;
            CurrentPage = 1;
            SelectedSource = "Assembly";
            SelectedMachineName = "All";
            LoadBreakTimeForToday();
            LoadMachineNameList();
            LoadData();
            return Page();
        }

        private void LoadMachineNameList()
        {
            MachineNameList.Clear();
            try
            {
                if (string.IsNullOrEmpty(machineConnectionString))
                {
                    Console.WriteLine("? MachineConnection string is empty!");
                    return;
                }
                using var conn = new SqlConnection(machineConnectionString);
                conn.Open();
                using var cmd = new SqlCommand("SELECT IdMachine, MachineName FROM [dbo].[MachineList] ORDER BY MachineName", conn);
                using var reader = cmd.ExecuteReader();
                while (reader.Read())
                    MachineNameList.Add(reader.GetString(reader.GetOrdinal("MachineName")));
                Console.WriteLine($"? Loaded {MachineNameList.Count} machines from MachineList");
            }
            catch (Exception ex) { Console.WriteLine($"? LoadMachineNameList error: {ex.Message}"); }
        }

        private void LoadBreakTimeForToday()
        {
            var today = DateOnly.FromDateTime(DateTime.Today);
            var latestBreakTime = _context.AdditionalBreakTimes
                .Where(bt => bt.Date == today)
                .OrderByDescending(bt => bt.CreatedAt)
                .FirstOrDefault();
            if (latestBreakTime != null)
            {
                AdditionalBreakTime1Start = latestBreakTime.BreakTime1Start?.ToString(@"hh\:mm");
                AdditionalBreakTime1End = latestBreakTime.BreakTime1End?.ToString(@"hh\:mm");
                AdditionalBreakTime2Start = latestBreakTime.BreakTime2Start?.ToString(@"hh\:mm");
                AdditionalBreakTime2End = latestBreakTime.BreakTime2End?.ToString(@"hh\:mm");
            }
        }

        private bool TryParseTimeSpan(string timeString, out TimeSpan result)
        {
            string[] formats = { "HH:mm", "H:mm", "HH:mm:ss", "H:mm:ss" };
            if (TimeSpan.TryParseExact(timeString, formats, null, out result)) return true;
            if (DateTime.TryParse(timeString, out DateTime dateTime)) { result = dateTime.TimeOfDay; return true; }
            result = TimeSpan.Zero; return false;
        }

        private bool IsInBreakTime(TimeSpan startTime, TimeSpan endTime, List<(TimeSpan Start, TimeSpan End)> breakTimes)
        {
            foreach (var (breakStart, breakEnd) in breakTimes)
                if ((startTime >= breakStart && startTime <= breakEnd) ||
                    (endTime >= breakStart && endTime <= breakEnd) ||
                    (startTime <= breakStart && endTime >= breakEnd)) return true;
            return false;
        }

        private void LoadData()
        {
            if (SelectedSource == "Machine") LoadDataFromMachine();
            else LoadDataFromAssembly();
        }

        private void LoadDataFromAssembly()
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            var breakTimes = GetAllBreakTimes();

            DateTime prevMonthDate = StartSelectedDate.AddMonths(-1);
            DateTime lastMonthStart = new DateTime(prevMonthDate.Year, prevMonthDate.Month, 1);
            DateTime lastMonthEnd = lastMonthStart.AddMonths(1).AddDays(-1);

            List<LossTimeRecord> currentRecords;
            List<LossTimeRecord> lastMonthRecords;

            if (HasActualsData())
            {
                Console.WriteLine("? Menggunakan data murni dari LossTimeActuals");
                currentRecords = GetActualsAsLossRecords();
                var allLastMonth = GetCombinedRecords(lastMonthStart, lastMonthEnd, lastMonthStart, lastMonthEnd, breakTimes);
                lastMonthRecords = allLastMonth.Where(r => r.Date >= lastMonthStart && r.Date <= lastMonthEnd).ToList();
            }
            else
            {
                Console.WriteLine("?? Tidak ada Actuals, menggunakan AssemblyLossTime");
                var allRecords = GetCombinedRecords(lastMonthStart, lastMonthEnd, StartSelectedDate, EndSelectedDate, breakTimes);
                lastMonthRecords = allRecords.Where(r => r.Date >= lastMonthStart && r.Date <= lastMonthEnd).ToList();
                currentRecords = allRecords.Where(r => r.Date >= StartSelectedDate && r.Date <= EndSelectedDate).ToList();
            }

            PrepareSummaryChartData(currentRecords, lastMonthRecords);
            PrepareDailyChartData(currentRecords);
            // ? NEW: Prepare MTT sub-category daily chart
            PrepareMttDailyChartData(currentRecords);

            TotalRecords = currentRecords.Count;
            EnsureValidCurrentPage();
            LossTimeData = currentRecords
                .OrderByDescending(r => r.Date)
                .ThenBy(r => r.Location)
                .Skip((CurrentPage - 1) * PageSize)
                .Take(PageSize)
                .ToList();

            Console.WriteLine($"? LoadDataFromAssembly: {currentRecords.Count} records in {sw.ElapsedMilliseconds}ms");

            AllMttRecords = currentRecords
    .Where(r => r.Category == "Machine & Tools Trouble")
    .OrderByDescending(r => r.Date)
    .ThenBy(r => r.Location)
    .ToList();
        }


        private bool HasActualsData()
        {
            var query = _context.LossTimeActuals.Where(x => x.Month == SelectedMonth && x.Year == SelectedYear);
            if (!string.IsNullOrEmpty(MachineLine) && MachineLine != "All")
                query = query.Where(x => x.MachineLine == MachineLine);
            return query.Any();
        }

        private List<LossTimeRecord> GetActualsAsLossRecords()
        {
            var results = new List<LossTimeRecord>();
            var query = _context.LossTimeActuals
                .Where(x => x.Month == SelectedMonth && x.Year == SelectedYear && x.Minutes > 0);

            if (!string.IsNullOrEmpty(MachineLine) && MachineLine != "All")
                query = query.Where(x => x.MachineLine == MachineLine);

            if (SelectedShifts != null && SelectedShifts.Any() && SelectedShifts.Count < 3)
                query = query.Where(x => SelectedShifts.Contains(x.Shift));

            var actuals = query.ToList();
            foreach (var actual in actuals)
            {
                if (actual.Day < 1 || actual.Day > DateTime.DaysInMonth(actual.Year, actual.Month)) continue;

                results.Add(new LossTimeRecord
                {
                    RecordId = actual.Id,   // Id dari LossTimeActuals (EF primary key)
                    Date = new DateTime(actual.Year, actual.Month, actual.Day),
                    Category = actual.Category,
                    Duration = (int)(actual.Minutes * 60),
                    Location = actual.MachineLine,
                    Shift = string.IsNullOrEmpty(actual.Shift) ? "1" : actual.Shift,
                    LossTime = actual.Category,
                    Start = TimeSpan.Zero,
                    End = TimeSpan.Zero,
                    DetailedReason = string.IsNullOrEmpty(actual.DetailedReason)
                         ? $"Actual: {actual.Minutes} min (Shift {actual.Shift})"
                         : $"{actual.DetailedReason} | {actual.Minutes} min (Shift {actual.Shift})"
                });
            }

            Console.WriteLine($"? GetActualsAsLossRecords: {results.Count} records");
            return results;
        }

        private void LoadDataFromMachine()
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            DateTime prevMonthDate = StartSelectedDate.AddMonths(-1);
            DateTime lastMonthStart = new DateTime(prevMonthDate.Year, prevMonthDate.Month, 1);
            DateTime lastMonthEnd = lastMonthStart.AddMonths(1).AddDays(-1);

            var currentRecords = GetMachineRecords(StartSelectedDate, EndSelectedDate);
            var lastMonthRecords = GetMachineRecords(lastMonthStart, lastMonthEnd);

            PrepareSummaryChartData(currentRecords, lastMonthRecords);
            PrepareDailyChartData(currentRecords);
            // ? NEW: Prepare MTT sub-category daily chart for machine source too
            PrepareMttDailyChartData(currentRecords);

            TotalRecords = currentRecords.Count;
            EnsureValidCurrentPage();
            LossTimeData = currentRecords
                .OrderByDescending(r => r.Date)
                .Skip((CurrentPage - 1) * PageSize)
                .Take(PageSize)
                .ToList();

            Console.WriteLine($"? LoadDataFromMachine: {LossTimeData.Count} records in {sw.ElapsedMilliseconds}ms");

            AllMttRecords = currentRecords
    .Where(r => r.Category == "Machine & Tools Trouble")
    .OrderByDescending(r => r.Date)
    .ThenBy(r => r.Location)
    .ToList();
        }

        private List<LossTimeRecord> GetMachineRecords(DateTime start, DateTime end)
        {
            var records = new List<LossTimeRecord>();
            if (string.IsNullOrEmpty(machineConnectionString)) { Console.WriteLine("? MachineConnection string is empty!"); return records; }

            string machineFilter = (!string.IsNullOrEmpty(SelectedMachineName) && SelectedMachineName != "All") ? "AND ml.MachineName = @MachineName" : "";
            string shiftFilter = "";
            if (SelectedShifts != null && SelectedShifts.Any() && SelectedShifts.Count < 3)
            {
                var shiftConds = new List<string>();
                if (SelectedShifts.Contains("1")) shiftConds.Add("e.Shift IN ('Shift 1','1')");
                if (SelectedShifts.Contains("2")) shiftConds.Add("e.Shift IN ('Shift 2','2')");
                if (SelectedShifts.Contains("3")) shiftConds.Add("e.Shift IN ('Shift 3','3')");
                if (shiftConds.Any()) shiftFilter = " AND (" + string.Join(" OR ", shiftConds) + ")";
            }

            string sql = $@"
                SELECT ml.MachineName, e.[Date], e.Shift, el.LossCategory, el.LossGroup, el.LossMinutes
                FROM [dbo].[Efficiency] e
                JOIN [dbo].[MachineList] ml ON ml.IdMachine = e.IdMachine
                INNER JOIN [dbo].[EfficiencyLoss] el ON el.EfficiencyID = e.ID
                WHERE CAST(e.[Date] AS DATE) >= @StartDate AND CAST(e.[Date] AS DATE) <= @EndDate
                  AND el.LossMinutes > 0 {machineFilter} {shiftFilter}
                ORDER BY e.[Date] DESC, ml.MachineName";

            try
            {
                using var conn = new SqlConnection(machineConnectionString);
                conn.Open();
                using var cmd = new SqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@StartDate", start.Date);
                cmd.Parameters.AddWithValue("@EndDate", end.Date);
                if (!string.IsNullOrEmpty(SelectedMachineName) && SelectedMachineName != "All")
                    cmd.Parameters.AddWithValue("@MachineName", SelectedMachineName);

                using var reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    var machineName = reader.IsDBNull(reader.GetOrdinal("MachineName")) ? "" : reader.GetString(reader.GetOrdinal("MachineName"));
                    var date = reader.GetDateTime(reader.GetOrdinal("Date"));
                    var shiftRaw = reader.IsDBNull(reader.GetOrdinal("Shift")) ? "" : reader.GetString(reader.GetOrdinal("Shift"));
                    var lossCategory = reader.IsDBNull(reader.GetOrdinal("LossCategory")) ? "" : reader.GetString(reader.GetOrdinal("LossCategory"));
                    var lossGroup = reader.IsDBNull(reader.GetOrdinal("LossGroup")) ? "" : reader.GetString(reader.GetOrdinal("LossGroup"));
                    var lossMinutes = reader.IsDBNull(reader.GetOrdinal("LossMinutes")) ? 0.0 : Convert.ToDouble(reader["LossMinutes"]);

                    string shiftNum = shiftRaw switch { "Shift 1" or "1" => "1", "Shift 2" or "2" => "2", "Shift 3" or "3" => "3", "Non Shift" or "NS" => "NS", _ => shiftRaw };
                    records.Add(new LossTimeRecord
                    {
                        // Machine source tidak punya RecordId yang bisa di-edit langsung, set 0
                        RecordId = 0,
                        Date = date,
                        LossTime = lossCategory,
                        Start = TimeSpan.Zero,
                        End = TimeSpan.Zero,
                        Duration = (int)(lossMinutes * 60),
                        Location = machineName,
                        Shift = shiftNum,
                        Category = CategorizeReason(lossCategory),
                        DetailedReason = $"{lossGroup} | {lossCategory}: {lossMinutes} min"
                    });
                }
            }
            catch (Exception ex) { Console.WriteLine($"? GetMachineRecords error: {ex.Message}\n{ex.StackTrace}"); }
            return records;
        }

        private List<LossTimeRecord> GetCombinedRecords(DateTime lastStart, DateTime lastEnd, DateTime currStart, DateTime currEnd, List<(TimeSpan Start, TimeSpan End)> breakTimes)
        {
            var records = new List<LossTimeRecord>();
            string query = BuildQueryForDateRange();
            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                connection.Open();
                using (SqlCommand command = new SqlCommand(query, connection))
                {
                    command.Parameters.AddWithValue("@StartDate", lastStart.Date);
                    command.Parameters.AddWithValue("@EndDate", currEnd.Date);
                    if (!string.IsNullOrEmpty(MachineLine) && MachineLine != "All")
                        command.Parameters.AddWithValue("@MachineLine", MachineLine);

                    using (SqlDataReader reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            TimeSpan startTime = reader.GetTimeSpan(reader.GetOrdinal("StartTime"));
                            TimeSpan endTime = reader.GetTimeSpan(reader.GetOrdinal("EndTime"));
                            if (IsInBreakTime(startTime, endTime, breakTimes)) continue;
                            string reason = reader.IsDBNull(reader.GetOrdinal("Reason")) ? string.Empty : reader.GetString(reader.GetOrdinal("Reason"));
                            records.Add(new LossTimeRecord
                            {
                                RecordId = reader.IsDBNull(reader.GetOrdinal("Id")) ? 0 : reader.GetInt32(reader.GetOrdinal("Id")),
                                Date = reader.GetDateTime(reader.GetOrdinal("Date")),
                                LossTime = reason,
                                Start = startTime,
                                End = endTime,
                                Duration = reader.IsDBNull(reader.GetOrdinal("LossTime")) ? 0 : reader.GetInt32(reader.GetOrdinal("LossTime")),
                                Location = reader.IsDBNull(reader.GetOrdinal("MachineCode")) ? string.Empty : reader.GetString(reader.GetOrdinal("MachineCode")),
                                Shift = reader.IsDBNull(reader.GetOrdinal("Shift")) ? string.Empty : reader.GetString(reader.GetOrdinal("Shift")),
                                Category = CategorizeReason(reason),
                                DetailedReason = reader.IsDBNull(reader.GetOrdinal("DetailedReason")) ? null : reader.GetString(reader.GetOrdinal("DetailedReason"))
                            });
                        }
                    }
                }
            }
            return records;
        }

        private string BuildQueryForDateRange()
        {
            string query = @"
SELECT Id, Date, Reason, DetailedReason, MachineCode,
       CAST(Time AS TIME) AS StartTime, CAST(EndDateTime AS TIME) AS EndTime, LossTime, 
       CASE WHEN CAST(Time AS TIME) >= '07:00:00' AND CAST(Time AS TIME) < '15:45:00' THEN '1'
            WHEN CAST(Time AS TIME) >= '15:45:00' AND CAST(Time AS TIME) < '23:15:00' THEN '2'
            ELSE '3' END AS Shift
FROM AssemblyLossTime 
WHERE Date >= @StartDate AND Date <= @EndDate";
            if (!string.IsNullOrEmpty(MachineLine) && MachineLine != "All")
                query += " AND MachineCode = @MachineLine";
            return query;
        }

        private void PrepareSummaryChartData(List<LossTimeRecord> currentRecords, List<LossTimeRecord> lastMonthRecords)
        {
            try
            {
                var categoryStats = AllCategories.Select(cat => new
                {
                    Name = cat,
                    S1 = currentRecords.Where(r => r.Category == cat && r.Shift == "1").Sum(r => r.Duration),
                    S2 = currentRecords.Where(r => r.Category == cat && r.Shift == "2").Sum(r => r.Duration),
                    S3 = currentRecords.Where(r => r.Category == cat && r.Shift == "3").Sum(r => r.Duration),
                    TotalCurrent = currentRecords.Where(r => r.Category == cat).Sum(r => r.Duration),
                    TotalLast = lastMonthRecords.Where(r => r.Category == cat).Sum(r => r.Duration)
                }).ToList();

                var withData = categoryStats.Where(x => x.TotalCurrent > 0).OrderByDescending(x => x.TotalCurrent).ToList();
                var withoutData = categoryStats.Where(x => x.TotalCurrent == 0).ToList();
                var sortedStats = withData.Concat(withoutData).ToList();
                var chartData = new
                {
                    labels = sortedStats.Select(x => x.Name).ToArray(),
                    fullLabels = sortedStats.Select(x => x.Name).ToArray(),
                    shift1Data = sortedStats.Select(x => Math.Round(x.S1 / 60.0, 2)).ToArray(),
                    shift2Data = sortedStats.Select(x => Math.Round(x.S2 / 60.0, 2)).ToArray(),
                    shift3Data = sortedStats.Select(x => Math.Round(x.S3 / 60.0, 2)).ToArray(),
                    lastMonthData = sortedStats.Select(x => Math.Round(x.TotalLast / 60.0, 2)).ToArray()
                };
                var options = new JsonSerializerOptions { Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping };
                ChartDataJson = JsonSerializer.Serialize(chartData, options);
            }
            catch (Exception ex) { Console.WriteLine($"? Error in PrepareSummaryChartData: {ex.Message}"); ChartDataJson = "{}"; }
        }

        private void PrepareDailyChartData(List<LossTimeRecord> currentRecords)
        {
            try
            {
                int daysInMonth = DateTime.DaysInMonth(SelectedYear, SelectedMonth);
                var days = Enumerable.Range(1, daysInMonth).ToArray();
                var dailyGroups = currentRecords
                    .GroupBy(r => new { Day = r.Date.Day, r.Category })
                    .ToDictionary(g => g.Key, g => g.Sum(x => x.Duration));

                var datasets = AllCategories.Select(category => new
                {
                    label = CategoryFullNames.ContainsKey(category) ? CategoryFullNames[category] : category,
                    data = days.Select(day =>
                    {
                        var key = new { Day = day, Category = category };
                        return dailyGroups.ContainsKey(key) ? Math.Round(dailyGroups[key] / 60.0, 2) : 0;
                    }).ToArray(),
                    backgroundColor = CategoryColors.ContainsKey(category) ? CategoryColors[category] : "#cccccc",
                    stack = "DayStack"
                }).ToList();

                var dailyChartData = new { labels = days.Select(d => d.ToString()).ToArray(), datasets };
                var options = new JsonSerializerOptions { Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping };
                DailyChartDataJson = JsonSerializer.Serialize(dailyChartData, options);
            }
            catch (Exception ex) { Console.WriteLine($"? Error in PrepareDailyChartData: {ex.Message}"); DailyChartDataJson = "{}"; }
        }

        // ? NEW METHOD: Prepare MTT sub-category daily chart data
        // Parsing DetailedReason dari records MTT untuk extract sub-category
        private void PrepareMttDailyChartData(List<LossTimeRecord> currentRecords)
        {
            try
            {
                int daysInMonth = DateTime.DaysInMonth(SelectedYear, SelectedMonth);
                var days = Enumerable.Range(1, daysInMonth).ToArray();

                // Filter hanya records MTT
                var mttRecords = currentRecords
                    .Where(r => r.Category == "Machine & Tools Trouble")
                    .ToList();

                // Extract sub-category dari DetailedReason
                // DetailedReason bisa berupa: "Vaccum error" / "Scanner FM CU" / "LossGroup | LossCategory: X min" dll
                // Strategy: cek apakah DetailedReason mengandung nama sub-category yang dikenal
                var allMttSubCategories = new List<string>
                {
                    "Scanner FM CU", "Scanner Comp CU", "Scanner Robot CU",
                    "Scanner Nameplate CU", "Scanner Label CU", "Scanner Final CU",
                    "Scanner FM CS", "Scanner Comp CS", "Scanner Robot CS",
                    "Scanner Nameplate CS", "Scanner Label CS", "Scanner Final CS",
                    "Bending Condensor Reguler", "Straping Band", "Bending Condensor Bigcap",
                    "Driver", "Gas Charge", "Running Trip", "Vaccum",
                    "Conveyor", "Lifter Prouduct", "Laser"
                };

                // Group records MTT per hari per sub-category
                // Sub-category diambil dari DetailedReason
                var mttDailySubData = new Dictionary<string, Dictionary<int, double>>();

                foreach (var record in mttRecords)
                {
                    string subCat = ExtractMttSubCategory(record.DetailedReason, allMttSubCategories);
                    if (!mttDailySubData.ContainsKey(subCat))
                        mttDailySubData[subCat] = new Dictionary<int, double>();

                    int day = record.Date.Day;
                    if (!mttDailySubData[subCat].ContainsKey(day))
                        mttDailySubData[subCat][day] = 0;

                    mttDailySubData[subCat][day] += record.Duration / 60.0;
                }

                // Sort sub-categories by total duration descending
                var sortedSubCats = mttDailySubData
                    .OrderByDescending(kv => kv.Value.Values.Sum())
                    .Select(kv => kv.Key)
                    .ToList();

                // Palette warna untuk sub-categories yang tidak ada di hardcode
                var colorPalette = new List<string>
                {
                    "#FF6384","#36A2EB","#FFCE56","#4BC0C0","#9966FF","#FF9F40",
                    "#C9CBCF","#FF8FAB","#1A6EBF","#20C997","#198754","#0D6EFD",
                    "#FF4500","#FF7F50","#FFD700","#DDA0DD","#BA55D3","#87CEEB",
                    "#4169E1","#6495ED","#FFA500","#00BFFF"
                };
                int colorIdx = 0;

                var datasets = sortedSubCats.Select(subCat =>
                {
                    string color = MttSubCategoryColors.ContainsKey(subCat)
                        ? MttSubCategoryColors[subCat]
                        : colorPalette[colorIdx++ % colorPalette.Count];

                    return new
                    {
                        label = subCat,
                        data = days.Select(day =>
                        {
                            return mttDailySubData.ContainsKey(subCat) && mttDailySubData[subCat].ContainsKey(day)
                                ? Math.Round(mttDailySubData[subCat][day], 2)
                                : 0.0;
                        }).ToArray(),
                        backgroundColor = color,
                        stack = "MttStack"
                    };
                }).ToList();

                var mttChartData = new
                {
                    labels = days.Select(d => d.ToString()).ToArray(),
                    datasets,
                    // Tambahkan summary: sub-cat paling besar total
                    topSubCategory = sortedSubCats.FirstOrDefault() ?? "",
                    topSubCategoryTotal = sortedSubCats.Any() && mttDailySubData.ContainsKey(sortedSubCats.First())
                        ? Math.Round(mttDailySubData[sortedSubCats.First()].Values.Sum(), 2)
                        : 0.0
                };

                var options = new JsonSerializerOptions { Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping };
                MttDailyChartDataJson = JsonSerializer.Serialize(mttChartData, options);

                Console.WriteLine($"? PrepareMttDailyChartData: {mttRecords.Count} MTT records, {sortedSubCats.Count} sub-categories");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"? Error in PrepareMttDailyChartData: {ex.Message}");
                MttDailyChartDataJson = "{}";
            }
        }

        // ? NEW: Helper untuk extract sub-category MTT dari DetailedReason
        private string ExtractMttSubCategory(string detailedReason, List<string> knownSubCategories)
        {
            if (string.IsNullOrEmpty(detailedReason)) return "Other MTT";

            // Cek apakah DetailedReason mengandung nama sub-category yang dikenal
            foreach (var subCat in knownSubCategories)
            {
                if (detailedReason.IndexOf(subCat, StringComparison.OrdinalIgnoreCase) >= 0)
                    return subCat;
            }

            // Cek keywords umum
            var dr = detailedReason.ToLower();
            if (dr.Contains("vaccum") || dr.Contains("vacuum")) return "Vaccum";
            if (dr.Contains("scanner")) return "Scanner FM CU"; // default scanner
            if (dr.Contains("laser")) return "Laser";
            if (dr.Contains("conveyor")) return "Conveyor";
            if (dr.Contains("driver")) return "Driver";
            if (dr.Contains("gas")) return "Gas Charge";
            if (dr.Contains("bending")) return "Bending Condensor Reguler";
            if (dr.Contains("straping") || dr.Contains("strapping")) return "Straping Band";
            if (dr.Contains("lifter")) return "Lifter Prouduct";
            if (dr.Contains("running") || dr.Contains("trip")) return "Running Trip";

            // Jika tidak dikenali, gunakan bagian pertama dari DetailedReason sebagai label
            // Batasi panjang
            var parts = detailedReason.Split(new char[] { '|', ':', '-', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length > 0)
            {
                var firstPart = parts[0].Trim();
                if (firstPart.Length > 30) firstPart = firstPart.Substring(0, 30) + "...";
                if (!string.IsNullOrEmpty(firstPart)) return firstPart;
            }

            return "Other MTT";
        }

        private void LoadPaginatedData(List<(TimeSpan Start, TimeSpan End)> breakTimes)
        {
            TotalRecords = GetTotalRecords(breakTimes);
            EnsureValidCurrentPage();
            string query = BuildQueryBase();
            query += " ORDER BY [Date] DESC, Time OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY";
            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                connection.Open();
                using (SqlCommand command = new SqlCommand(query, connection))
                {
                    AddQueryParameters(command, StartSelectedDate, EndSelectedDate);
                    command.Parameters.AddWithValue("@Offset", (CurrentPage - 1) * PageSize);
                    command.Parameters.AddWithValue("@PageSize", PageSize);
                    using (SqlDataReader reader = command.ExecuteReader())
                    {
                        LossTimeData.Clear();
                        while (reader.Read())
                        {
                            TimeSpan startTime = reader.GetTimeSpan(reader.GetOrdinal("StartTime"));
                            TimeSpan endTime = reader.GetTimeSpan(reader.GetOrdinal("EndTime"));
                            if (IsInBreakTime(startTime, endTime, breakTimes)) continue;
                            string reason = reader.IsDBNull(reader.GetOrdinal("Reason")) ? string.Empty : reader.GetString(reader.GetOrdinal("Reason"));
                            LossTimeData.Add(new LossTimeRecord
                            {
                                Nomor = reader.IsDBNull(reader.GetOrdinal("Id")) ? 0 : reader.GetInt32(reader.GetOrdinal("Id")),
                                Date = reader.IsDBNull(reader.GetOrdinal("Date")) ? DateTime.MinValue : reader.GetDateTime(reader.GetOrdinal("Date")),
                                LossTime = reason,
                                Start = startTime,
                                End = endTime,
                                Duration = reader.IsDBNull(reader.GetOrdinal("LossTime")) ? 0 : reader.GetInt32(reader.GetOrdinal("LossTime")),
                                Location = reader.IsDBNull(reader.GetOrdinal("MachineCode")) ? string.Empty : reader.GetString(reader.GetOrdinal("MachineCode")),
                                Shift = reader.IsDBNull(reader.GetOrdinal("Shift")) ? string.Empty : reader.GetString(reader.GetOrdinal("Shift")),
                                Category = CategorizeReason(reason),
                                DetailedReason = reader.IsDBNull(reader.GetOrdinal("DetailedReason")) ? null : reader.GetString(reader.GetOrdinal("DetailedReason"))
                            });
                        }
                    }
                }
            }
        }

        private List<LossTimeRecord> GetLossTimeRecords(DateTime start, DateTime end, List<(TimeSpan Start, TimeSpan End)> breakTimes)
        {
            var records = new List<LossTimeRecord>();
            string query = BuildQueryForDateRange();
            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                connection.Open();
                using (SqlCommand command = new SqlCommand(query, connection))
                {
                    command.Parameters.AddWithValue("@StartDate", start.Date);
                    command.Parameters.AddWithValue("@EndDate", end.Date);
                    if (!string.IsNullOrEmpty(MachineLine) && MachineLine != "All")
                        command.Parameters.AddWithValue("@MachineLine", MachineLine);
                    using (SqlDataReader reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            TimeSpan startTime = reader.GetTimeSpan(reader.GetOrdinal("StartTime"));
                            TimeSpan endTime = reader.GetTimeSpan(reader.GetOrdinal("EndTime"));
                            if (IsInBreakTime(startTime, endTime, breakTimes)) continue;
                            string reason = reader.IsDBNull(reader.GetOrdinal("Reason")) ? string.Empty : reader.GetString(reader.GetOrdinal("Reason"));
                            records.Add(new LossTimeRecord
                            {
                                Date = reader.GetDateTime(reader.GetOrdinal("Date")),
                                LossTime = reason,
                                Start = startTime,
                                End = endTime,
                                Duration = reader.IsDBNull(reader.GetOrdinal("LossTime")) ? 0 : reader.GetInt32(reader.GetOrdinal("LossTime")),
                                Shift = reader.IsDBNull(reader.GetOrdinal("Shift")) ? string.Empty : reader.GetString(reader.GetOrdinal("Shift")),
                                Category = CategorizeReason(reason)
                            });
                        }
                    }
                }
            }
            return records;
        }

        private void CalculateAllDataSummary(List<(TimeSpan Start, TimeSpan End)> breakTimes)
            => PrepareDailyChartData(GetLossTimeRecords(StartSelectedDate, EndSelectedDate, breakTimes));

        private string BuildQueryBase()
        {
            string query = @"
        SELECT Id, Date, Reason, DetailedReason, 
               CAST(Time AS TIME) AS StartTime, CAST(EndDateTime AS TIME) AS EndTime, LossTime, MachineCode, 
               CASE WHEN CAST(Time AS TIME) >= '07:00:00' AND CAST(Time AS TIME) < '15:45:00' THEN '1'
                    WHEN CAST(Time AS TIME) >= '15:45:00' AND CAST(Time AS TIME) < '23:15:00' THEN '2'
                    ELSE '3' END AS Shift
        FROM AssemblyLossTime 
        WHERE ((Date = @StartDate AND CAST(Time AS TIME) >= '07:00:00')
            OR (Date > @StartDate AND Date < @EndDate)
            OR (@IsHistorical = 1 AND Date = DATEADD(DAY, 1, @EndDate) AND CAST(Time AS TIME) < '07:00:00')
            OR (@IsHistorical = 0 AND Date = @EndDate AND CAST(Time AS TIME) <= @CurrentTime))";
            if (!string.IsNullOrEmpty(MachineLine) && MachineLine != "All")
                query += " AND MachineCode = @MachineLine";
            if (SelectedShifts != null && SelectedShifts.Any() && SelectedShifts.Count < 3)
            {
                var sc = new List<string>();
                if (SelectedShifts.Contains("1")) sc.Add("(CAST(Time AS TIME) >= '07:00:00' AND CAST(Time AS TIME) < '15:45:00')");
                if (SelectedShifts.Contains("2")) sc.Add("(CAST(Time AS TIME) >= '15:45:00' AND CAST(Time AS TIME) < '23:15:00')");
                if (SelectedShifts.Contains("3")) sc.Add("(CAST(Time AS TIME) >= '23:15:00' OR CAST(Time AS TIME) < '07:00:00')");
                if (sc.Any()) query += " AND (" + string.Join(" OR ", sc) + ")";
            }
            return query;
        }

        private void AddQueryParameters(SqlCommand command, DateTime start, DateTime end)
        {
            bool isHistorical = end.Date < DateTime.Today;
            command.Parameters.AddWithValue("@StartDate", start.Date);
            command.Parameters.AddWithValue("@EndDate", end.Date);
            command.Parameters.AddWithValue("@IsHistorical", isHistorical ? 1 : 0);
            command.Parameters.AddWithValue("@CurrentTime", isHistorical ? new TimeSpan(23, 59, 59) : DateTime.Now.TimeOfDay);
            if (!string.IsNullOrEmpty(MachineLine) && MachineLine != "All")
                command.Parameters.AddWithValue("@MachineLine", MachineLine);
        }

        private int GetTotalRecords(List<(TimeSpan Start, TimeSpan End)> breakTimes)
        {
            string baseQuery = BuildQueryBase();
            foreach (var (start, end) in breakTimes)
                baseQuery += $" AND NOT (CAST(Time AS TIME) BETWEEN '{start}' AND '{end}')";
            string countQuery = $"SELECT COUNT(*) FROM ({baseQuery}) AS CountQuery";
            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                connection.Open();
                using (SqlCommand command = new SqlCommand(countQuery, connection))
                {
                    AddQueryParameters(command, StartSelectedDate, EndSelectedDate);
                    return (int)command.ExecuteScalar();
                }
            }
        }

        private List<(TimeSpan Start, TimeSpan End)> _cachedBreakTimes = null;

        private List<(TimeSpan Start, TimeSpan End)> GetAllBreakTimes()
        {
            if (_cachedBreakTimes != null) return _cachedBreakTimes;
            var breakTimes = new List<(TimeSpan Start, TimeSpan End)>();
            breakTimes.AddRange(FixedBreakTimes);

            if (DateTime.Today.DayOfWeek == DayOfWeek.Friday)
                breakTimes.Add((new TimeSpan(11, 50, 0), new TimeSpan(13, 15, 0)));
            else
                breakTimes.Add((new TimeSpan(12, 0, 0), new TimeSpan(12, 45, 0)));

            if (!string.IsNullOrEmpty(AdditionalBreakTime1Start) && !string.IsNullOrEmpty(AdditionalBreakTime1End))
                if (TryParseTimeSpan(AdditionalBreakTime1Start, out TimeSpan s1) && TryParseTimeSpan(AdditionalBreakTime1End, out TimeSpan e1))
                    breakTimes.Add((s1, e1));
            if (!string.IsNullOrEmpty(AdditionalBreakTime2Start) && !string.IsNullOrEmpty(AdditionalBreakTime2End))
                if (TryParseTimeSpan(AdditionalBreakTime2Start, out TimeSpan s2) && TryParseTimeSpan(AdditionalBreakTime2End, out TimeSpan e2))
                    breakTimes.Add((s2, e2));

            _cachedBreakTimes = breakTimes;
            return breakTimes;
        }

        private void EnsureValidCurrentPage()
        {
            if (TotalRecords == 0) { CurrentPage = 1; return; }
            int maxPages = (int)Math.Ceiling((double)TotalRecords / PageSize);
            if (CurrentPage > maxPages) CurrentPage = maxPages;
            else if (CurrentPage < 1) CurrentPage = 1;
        }

        private string CategorizeReason(string reason)
        {
            var r = reason?.ToLower().Trim() ?? "";
            if (string.IsNullOrEmpty(r)) return "Other";

            switch (reason?.Trim())
            {
                case "ModelChangingLoss": return "Model Change Loss";
                case "MoldChangingLoss": return "Mold Change Loss";
                case "GawseExternalBodies": return "Gawse - External Bodies";
                case "MaterialShortageExternal": return "Material Shortage External";
                case "MaterialShortageInternal": return "Material Shortage Internal";
                case "MaterialShortageInhouse": return "Material Shortage Inhouse";
                case "ManPowerAdjustment": return "Man Power Adjustment";
                case "QualityTrouble": return "Quality Trouble";
                case "FreeTalkingQC": return "Quality Trouble";
                case "MachineToolsTrouble": return "Machine & Tools Trouble";
                case "SetRepairingLoss": return "Set Repairing Loss";
                case "Rework": return "Rework";
                case "MorningAssembly": return "Morning Assembly";
                case "GeneralAssembly": return "General Assembly";
                case "BreakTime":
                case "CompanyActivity":
                case "Cleaning":
                case "StockOpname":
                case "Maintenance":
                case "TrialRun":
                case "TrainingEducation":
                case "NoProductionDay": return "Other";
            }

            switch (reason?.Trim())
            {
                case "Model Change Loss": return "Model Change Loss";
                case "Model Changing Loss": return "Model Change Loss";
                case "Change Model": return "Model Change Loss";
                case "Mold Change Loss": return "Mold Change Loss";
                case "Mold Changing Loss": return "Mold Change Loss";
                case "Gawse - External Bodies": return "Gawse - External Bodies";
                case "Gawse - External Loss": return "Gawse - External Bodies";
                case "Material Shortage External": return "Material Shortage External";
                case "Material Shortage Internal": return "Material Shortage Internal";
                case "Material Shortage": return "Material Shortage Internal";
                case "Material Shortage Inhouse": return "Material Shortage Inhouse";
                case "Man Power Adjustment": return "Man Power Adjustment";
                case "Quality Trouble": return "Quality Trouble";
                case "Quality": return "Quality Trouble";
                case "Machine & Tools Trouble": return "Machine & Tools Trouble";
                case "Machine Trouble": return "Machine & Tools Trouble";
                case "Set Repairing Loss": return "Set Repairing Loss";
                case "Rework": return "Rework";
                case "Morning Assembly": return "Morning Assembly";
                case "General Assembly": return "General Assembly";
                case "Loss Awal Hari": return "Loss Awal Hari";
                case "Break Time": return "Other";
            }

            if (r.Contains("mold chang") || r.Contains("mold change")) return "Mold Change Loss";
            if (r.Contains("model chang") || r.Contains("change model")) return "Model Change Loss";
            if (r.Contains("gawse")) return "Gawse - External Bodies";
            if (r.Contains("inhouse") || r.Contains("in house") || r.Contains("in-house")) return "Material Shortage Inhouse";
            if (r.Contains("internal") || r.Contains("materialshortageinternal")) return "Material Shortage Internal";
            if (r.Contains("external") || r.Contains("materialshortageexternal")) return "Material Shortage External";
            if (r.Contains("material shortage") || r.Contains("materialshortage")) return "Material Shortage Internal";
            if (r.Contains("man power") || r.Contains("manpower")) return "Man Power Adjustment";
            if (r.Contains("quality") || r.Contains("freetalkingqc") || r.Contains("qc")) return "Quality Trouble";
            if (r.Contains("set repair")) return "Set Repairing Loss";
            if (r.Contains("machine") || r.Contains("tools") || r.Contains("breakdown")) return "Machine & Tools Trouble";
            if (r.Contains("rework") || r.Contains("re-work")) return "Rework";
            if (r.Contains("general assembly") || r.Contains("generalassembly")) return "General Assembly";
            if (r.Contains("awal hari")) return "Loss Awal Hari";
            if (r.Contains("morning") || r.Contains("briefing")) return "Morning Assembly";
            if (r.Contains("assembly")) return "General Assembly";

            Console.WriteLine($"?? Unmatched LossCategory: '{reason}' ? Other");
            return "Other";
        }

        private string NormalizeCategoryName(string input)
        {
            if (string.IsNullOrWhiteSpace(input)) return "SKIP";
            switch (input.Trim().ToUpper())
            {
                case "QUALITY TROUBLE":
                case "FREE TALKING/QC ACTIVITY":
                case "FREE TALKING/QC": return "Quality Trouble";
                case "MODEL CHANGING LOSS":
                case "MODEL CHANGE LOSS":
                case "CHANGE MODEL": return "Model Change Loss";
                case "MOLD CHANGING LOSS":
                case "MOLD CHANGE LOSS": return "Mold Change Loss";
                case "GAWSE - EXTERNAL BODIES":
                case "GAWSE - EXTERNAL LOSS": return "Gawse - External Bodies";
                case "MATERIAL SHORTAGE EXTERNAL": return "Material Shortage External";
                case "MACHINE & TOOLS TROUBLE":
                case "MACHINE TROUBLE": return "Machine & Tools Trouble";
                case "SET REPAIRING LOSS": return "Set Repairing Loss";
                case "MAN POWER ADJUSTMENT": return "Man Power Adjustment";
                case "MATERIAL SHORTAGE INHOUSE": return "Material Shortage Inhouse";
                case "MATERIAL SHORTAGE INTERNAL":
                case "MATERIAL SHORTAGE": return "Material Shortage Internal";
                case "REWORK": return "Rework";
                case "MORNING ASSEMBLY": return "Morning Assembly";
                case "GENERAL ASSEMBLY": return "General Assembly";
                case "LOSS AWAL HARI": return "Loss Awal Hari";
                case "BREAK TIME (AM/PM)":
                case "COMPANY ACTIVITY":
                case "CLEANING":
                case "STOCK OPNAME":
                case "MAINTENANCE":
                case "TRIAL RUN":
                case "TRAINING EDUCATION":
                case "NO PRODUCTION DAY": return "SKIP";
                default:
                    Console.WriteLine($"?? Unmapped category from Excel: '{input}' ? SKIP");
                    return "SKIP";
            }
        }

        public int GetTotalDurationAllCategories() => CategorySummary.Values.Sum();
        public double SecondsToMinutes(int seconds) => Math.Round(seconds / 60.0, 2);
        public List<int> GetPageSizeOptions() => new List<int> { 10 };

        public IActionResult OnPostExportExcel()
        {
            LoadBreakTimeForToday();
            SetDatesFromMonthYear();
            List<LossTimeRecord> exportData;
            if (SelectedSource == "Machine")
                exportData = GetMachineRecords(StartSelectedDate, EndSelectedDate).OrderByDescending(x => x.Date).ToList();
            else
            {
                if (HasActualsData())
                    exportData = GetActualsAsLossRecords().OrderByDescending(x => x.Date).ToList();
                else
                {
                    var breakTimes = GetAllBreakTimes();
                    exportData = GetCombinedRecords(StartSelectedDate, StartSelectedDate, StartSelectedDate, EndSelectedDate, breakTimes)
                        .OrderByDescending(x => x.Date).ToList();
                }
            }
            using (var workbook = new XLWorkbook())
            {
                var ws = workbook.Worksheets.Add("Loss Time Data");
                ws.Cell(1, 1).Value = "No"; ws.Cell(1, 2).Value = "Date"; ws.Cell(1, 3).Value = "Category";
                ws.Cell(1, 4).Value = "Start Time"; ws.Cell(1, 5).Value = "End Time"; ws.Cell(1, 6).Value = "Duration (Min)";
                ws.Cell(1, 7).Value = "Location"; ws.Cell(1, 8).Value = "Shift"; ws.Cell(1, 9).Value = "Detailed Reason";
                int row = 2; int index = 1;
                foreach (var item in exportData)
                {
                    ws.Cell(row, 1).Value = index++; ws.Cell(row, 2).Value = item.Date; ws.Cell(row, 3).Value = item.Category;
                    ws.Cell(row, 4).Value = item.Start.ToString(@"hh\:mm\:ss"); ws.Cell(row, 5).Value = item.End.ToString(@"hh\:mm\:ss");
                    ws.Cell(row, 6).Value = Math.Round(item.Duration / 60.0, 2); ws.Cell(row, 7).Value = item.Location;
                    ws.Cell(row, 8).Value = item.Shift; ws.Cell(row, 9).Value = item.DetailedReason;
                    row++;
                }
                using (var stream = new MemoryStream())
                {
                    workbook.SaveAs(stream);
                    return File(stream.ToArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                        $"LossTime_{SelectedSource}_{StartSelectedDate:yyyyMMdd}-{EndSelectedDate:yyyyMMdd}.xlsx");
                }
            }
        }

        public IActionResult OnGetDownloadTemplateActualLoss()
        {
            var filePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "data", "ActualLossTime", "Template_LossTime_Actual.xlsx");
            if (!System.IO.File.Exists(filePath)) return NotFound("File template tidak ditemukan di server.");
            var bytes = System.IO.File.ReadAllBytes(filePath);
            return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "Template_LossTime_Actual.xlsx");
        }

        public async Task<IActionResult> OnPostImportExcelActualAsync()
        {
            ExcelPackage.LicenseContext = LicenseContext.NonCommercial;

            if (UploadedExcel == null || UploadedExcel.Length == 0)
            { TempData["Error"] = "File Excel belum dipilih."; return RedirectToPage(new { TargetYear, TargetMonth, MachineLine }); }
            if (string.IsNullOrEmpty(UploadMachineLine) || UploadMachineLine == "All")
            { TempData["Error"] = "Pilih Machine Line spesifik sebelum upload."; return RedirectToPage(new { TargetYear, TargetMonth, MachineLine }); }
            if (TargetYear == 0) TargetYear = DateTime.Today.Year;
            if (TargetMonth == 0) TargetMonth = DateTime.Today.Month;

            Console.WriteLine($"[ImportActual] TargetYear={TargetYear}, TargetMonth={TargetMonth}, UploadMachineLine={UploadMachineLine}");

            try
            {
                using (var stream = new MemoryStream())
                {
                    await UploadedExcel.CopyToAsync(stream);
                    using (var package = new ExcelPackage(stream))
                    {
                        var sheet = package.Workbook.Worksheets[0];
                        if (sheet.Dimension == null)
                        { TempData["Error"] = "Sheet Excel kosong."; return RedirectToPage(new { TargetYear, TargetMonth, MachineLine }); }

                        int rowCount = sheet.Dimension.Rows;
                        int daysInMonth = DateTime.DaysInMonth(TargetYear, TargetMonth);
                        Console.WriteLine($"[ImportActual] Total rows: {rowCount}, Days in month: {daysInMonth}");

                        const int FIRST_DAY_COL = 3;
                        const int COLS_PER_DAY = 6;

                        var newActuals = new List<LossTimeActual>();

                        for (int row = 3; row <= rowCount; row++)
                        {
                            var catRaw = sheet.Cells[row, 2].Value?.ToString()?.Trim();
                            if (string.IsNullOrEmpty(catRaw)) continue;
                            var catLower = catRaw.ToLower();
                            if (catLower.Contains("loss (min)") || catLower.Contains("loss category") ||
                                catLower.Contains("working loss") || catLower.Contains("fixed loss")) continue;

                            var catName = NormalizeCategoryName(catRaw);
                            if (catName == "SKIP") { Console.WriteLine($"[ImportActual] Row {row} SKIP: '{catRaw}'"); continue; }

                            Console.WriteLine($"[ImportActual] Row {row}, Category: {catName}");

                            for (int day = 1; day <= daysInMonth; day++)
                            {
                                int dayStartCol = FIRST_DAY_COL + (day - 1) * COLS_PER_DAY;

                                var shiftMap = new (int MinOff, int ReasonOff, string Label)[]
                                {
                                    (0, 1, "1"),
                                    (2, 3, "2"),
                                    (4, 5, "3"),
                                };

                                foreach (var (minOff, reasonOff, label) in shiftMap)
                                {
                                    var cellValue = sheet.Cells[row, dayStartCol + minOff].Value;
                                    if (cellValue == null) continue;

                                    string cellStr = cellValue.ToString().Replace(",", ".").Trim();
                                    if (!double.TryParse(cellStr, NumberStyles.Any, CultureInfo.InvariantCulture, out double minutes)) continue;
                                    if (minutes <= 0) continue;

                                    string detailedReason = sheet.Cells[row, dayStartCol + reasonOff].Value?.ToString()?.Trim() ?? "";

                                    newActuals.Add(new LossTimeActual
                                    {
                                        Category = catName,
                                        MachineLine = UploadMachineLine,
                                        Day = day,
                                        Month = TargetMonth,
                                        Year = TargetYear,
                                        Minutes = minutes,
                                        Shift = label,
                                        DetailedReason = detailedReason,
                                        CreatedAt = DateTime.Now
                                    });

                                    Console.WriteLine($"[ImportActual]  ? Day {day} Shift {label}: {minutes} min | {detailedReason}");
                                }
                            }
                        }

                        Console.WriteLine($"[ImportActual] Total data parsed: {newActuals.Count}");

                        if (newActuals.Any())
                        {
                            var daysInExcel = newActuals.Select(x => x.Day).Distinct().ToList();
                            var dataToDelete = _context.LossTimeActuals
                                .Where(x => x.MachineLine == UploadMachineLine && x.Month == TargetMonth && x.Year == TargetYear)
                                .ToList()
                                .Where(x => daysInExcel.Contains(x.Day))
                                .ToList();

                            _context.LossTimeActuals.RemoveRange(dataToDelete);
                            _context.LossTimeActuals.AddRange(newActuals);
                            await _context.SaveChangesAsync();

                            TempData["Success"] = $"Berhasil import {newActuals.Count} data Actual untuk {UploadMachineLine} (Bulan {TargetMonth}/{TargetYear}).";
                        }
                        else
                        {
                            TempData["Error"] = "Tidak ada data valid. Pastikan format sesuai template (data mulai dari baris 3, kolom B).";
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ImportActual] ERROR: {ex.Message}\n{ex.StackTrace}");
                TempData["Error"] = "Gagal Import: " + ex.Message;
            }

            return RedirectToPage(new { TargetYear, TargetMonth, MachineLine });
        }

        // ← method ini HARUS di dalam class IndexModel
        public async Task<IActionResult> OnPostEditDetailedReasonAsync()
        {
            try
            {
                var form = Request.Form;
                string recordSource = form["recordSource"].ToString();
                string newDetailedReason = form["newDetailedReason"].ToString();

                if (!int.TryParse(form["recordId"], out int recordId) || recordId <= 0)
                    return new JsonResult(new { success = false, message = "Record ID tidak valid." });

                if (recordSource == "Assembly")
                {
                    var actualRecord = await _context.LossTimeActuals.FindAsync(recordId);
                    if (actualRecord != null)
                    {
                        actualRecord.DetailedReason = newDetailedReason;
                        await _context.SaveChangesAsync();
                        return new JsonResult(new { success = true });
                    }

                    using var conn = new SqlConnection(connectionString);
                    await conn.OpenAsync();
                    using var cmd = new SqlCommand(
                        "UPDATE AssemblyLossTime SET DetailedReason = @Reason WHERE Id = @Id", conn);
                    cmd.Parameters.AddWithValue("@Reason", newDetailedReason);
                    cmd.Parameters.AddWithValue("@Id", recordId);
                    int rows = await cmd.ExecuteNonQueryAsync();
                    if (rows == 0)
                        return new JsonResult(new { success = false, message = "Record tidak ditemukan di database." });

                    return new JsonResult(new { success = true });
                }
                else
                {
                    return new JsonResult(new { success = false, message = "Edit tidak didukung untuk data Machine source." });
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ OnPostEditDetailedReason error: {ex.Message}");
                return new JsonResult(new { success = false, message = "Server error: " + ex.Message });
            }
        }

    }  // ← tutup class IndexModel

    public class LossTimeRecord
    {
        public int Nomor { get; set; }
        public int RecordId { get; set; }
        public DateTime Date { get; set; }
        public string LossTime { get; set; }
        public TimeSpan Start { get; set; }
        public TimeSpan End { get; set; }
        public int Duration { get; set; }
        public string Location { get; set; }
        public string Shift { get; set; }
        public string Category { get; set; }
        public string DetailedReason { get; set; }
    }
}  // ← tutup namespace