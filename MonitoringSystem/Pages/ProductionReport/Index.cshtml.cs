using Azure;
using DocumentFormat.OpenXml.Bibliography;
using DocumentFormat.OpenXml.Math;
using DocumentFormat.OpenXml.Spreadsheet;
using DocumentFormat.OpenXml.Wordprocessing;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Data.SqlClient;
using OfficeOpenXml;
using System.Globalization;
using System.Text;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace MonitoringSystem.Pages.ProductionReport
{
    public class IndexModel : PageModel
    {
        private readonly IWebHostEnvironment _webHostEnvironment;
        private readonly IConfiguration _configuration;
        private string? connectionString;
        [BindProperty] public IFormFile? UploadedFile { get; set; }
        [BindProperty] public string? TargetMachine { get; set; }

        public bool IsCurrentMonthView { get; private set; }
        public int DaysInMonth { get; private set; }
        public List<string> ChartLabels { get; private set; } = new List<string>();
        public List<decimal> NormalData { get; private set; } = new List<decimal>();
        public List<decimal> OvertimeData { get; private set; } = new List<decimal>();
        public List<int> OriginalPlanData { get; private set; } = new List<int>();
        public List<int?> PlanData { get; private set; } = new List<int?>();
        public List<int> OriginalPlanOvertimeData { get; private set; } = new List<int>();
        public List<int> NoOfDirectWorkers { get; private set; } = new List<int>();
        public List<int> DailyWorkTime { get; private set; } = new List<int>();
        public List<int> OvertimeOperators { get; private set; } = new List<int>();
        public List<int> OvertimeMinutes { get; private set; } = new List<int>();
        public List<double> DailyLossTime { get; private set; } = new List<double>();
        public List<int?> PlanOvertimeData { get; private set; } = new List<int?>();
        public List<int> EffectivePlanData { get; private set; } = new List<int>();
        public List<int> EffectivePlanOvertimeData { get; private set; } = new List<int>();
        public List<double> DailyNetManHours { get; private set; } = new List<double>();

        private class DailyData
        {
            public int Day { get; set; }
            public decimal Shift1_Unit { get; set; }
            public TimeSpan Shift1_EndTime { get; set; }
            public decimal Shift2_Unit { get; set; }
            public TimeSpan Shift2_EndTime { get; set; }
            public decimal Shift3_Unit { get; set; }
            public TimeSpan Shift3_EndTime { get; set; }
            public decimal NonShift_Unit { get; set; }
            public TimeSpan NonShift_EndTime { get; set; }
            public decimal Overtime_Unit { get; set; } = 0;
            public TimeSpan Overtime_EndTime { get; set; } = TimeSpan.Zero;
            public int? Plan { get; set; } = null;
            public int? PlanOvertime { get; set; } = null;
            public int OriginalPlan { get; set; } = 0;
            public int OtOriginalPlan { get; set; } = 0;
            public int NoOfOperator { get; set; } = 0;
            public int OtOperatorCount { get; set; } = 0;
            public TimeSpan LastOtTime { get; set; } = TimeSpan.Zero;
            public TimeSpan? OT_S1_Time { get; set; } = null;
            public TimeSpan? OT_S3_Time { get; set; } = null;
            public TimeSpan? OT_Normal_Time { get; set; } = null;
            public int Shift1_MaxOp { get; set; } = 0;
            public int Shift2_MaxOp { get; set; } = 0;
            public int Shift3_MaxOp { get; set; } = 0;
            public int NonShift_MaxOp { get; set; } = 0;
            public int Overtime_MaxOp { get; set; } = 0;
            public int Shift1_ActiveCount { get; set; } = 0;
            public int Shift2_ActiveCount { get; set; } = 0;
            public int Shift3_ActiveCount { get; set; } = 0;
            public int NonShift_ActiveCount { get; set; } = 0;
            public int Overtime_ActiveCount { get; set; } = 0;
            public bool HasAnyPlan { get; set; } = false;
        }

        public class RestTime { public int Duration { get; set; } public TimeSpan StartTime { get; set; } public TimeSpan EndTime { get; set; } }

        public IndexModel(IWebHostEnvironment webHostEnvironment, IConfiguration configuration)
        {
            _webHostEnvironment = webHostEnvironment;
            _configuration = configuration;
            this.connectionString = _configuration.GetConnectionString("DefaultConnection");
            ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
        }

        [BindProperty(SupportsGet = true)] public int SelectedMonth { get; set; } = DateTime.Now.Month;
        [BindProperty(SupportsGet = true)] public int SelectedYear { get; set; } = DateTime.Now.Year;
        [BindProperty(SupportsGet = true)] public string MachineLine { get; set; } = "All";
        [BindProperty(SupportsGet = true)] public List<string> SelectedShifts { get; set; } = new List<string>();

        public void OnGet()
        {
            if (!SelectedShifts.Any() || SelectedShifts.Contains("All"))
                SelectedShifts = new List<string> { "All" };
            else if (SelectedShifts.Count > 1 && SelectedShifts.Contains("All"))
                SelectedShifts = new List<string> { "All" };

            LoadChartData();
        }

        public IActionResult OnPost(string submitButton)
        {
            if (submitButton == "reset")
                return RedirectToPage(new { SelectedYear = DateTime.Now.Year, SelectedMonth = DateTime.Now.Month, MachineLine = "All" });

            return RedirectToPage(new
            {
                SelectedYear = this.SelectedYear,
                SelectedMonth = this.SelectedMonth,
                MachineLine = this.MachineLine,
                SelectedShifts = this.SelectedShifts
            });
        }

        [BindProperty] public int TargetMonth { get; set; }
        [BindProperty] public int TargetYear { get; set; }

        public IActionResult OnGetAjaxUpdate()
        {
            if (!SelectedShifts.Any() || SelectedShifts.Contains("All"))
                SelectedShifts = new List<string> { "All" };
            else if (SelectedShifts.Count > 1 && SelectedShifts.Contains("All"))
                SelectedShifts = new List<string> { "All" };

            LoadChartData();

            return new JsonResult(new
            {
                normalData = NormalData,
                overtimeData = OvertimeData,
                planData = PlanData,
                planOvertimeData = PlanOvertimeData,
                originalPlanData = OriginalPlanData,
                originalPlanOvertimeData = OriginalPlanOvertimeData,
                effectivePlanData = EffectivePlanData,
                effectivePlanOtData = EffectivePlanOvertimeData,
                chartLabels = ChartLabels,
                noOfDirectWorkers = NoOfDirectWorkers,
                overtimeOperators = OvertimeOperators,
                overtimeMinutes = OvertimeMinutes,
                dailyLossTime = DailyLossTime,
                dailyWorkTime = DailyWorkTime,
                dailyNetManHours = DailyNetManHours,
                isCurrentMonthView = IsCurrentMonthView,
                selectedShifts = SelectedShifts
            });
        }

        public IActionResult OnGetDownloadTemplate(string type)
        {
            if (string.IsNullOrEmpty(type) || (type.ToLower() != "cu" && type.ToLower() != "cs"))
                return NotFound("Invalid template type.");

            string wwwrootPath = _webHostEnvironment.WebRootPath;
            var templateFileName = $"template_{type.ToLower()}.xlsx";
            var templateFilePath = Path.Combine(wwwrootPath, "data", type.ToLower(), "plan", $"{type.ToLower()}_plan_template.xlsx");

            if (System.IO.File.Exists(templateFilePath))
                return File(System.IO.File.ReadAllBytes(templateFilePath), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", templateFileName);

            return NotFound($"Template file not found.");
        }

        public void LoadChartData()
        {
            this.connectionString = _configuration.GetConnectionString("DefaultConnection");
            var dailyLosses = GetDailyLossTimeTotals();
            bool isCurrentMonthView = (SelectedYear == DateTime.Now.Year && SelectedMonth == DateTime.Now.Month);
            this.IsCurrentMonthView = isCurrentMonthView;

            string dateFilter = isCurrentMonthView ? "AND CAST(SDate AS DATE) <= @TodayDate" : "";
            this.DaysInMonth = DateTime.DaysInMonth(SelectedYear, SelectedMonth);
            var combinedData = Enumerable.Range(1, this.DaysInMonth).Select(day => new DailyData { Day = day }).ToList();

            // 🛠️ PERBAIKAN: Ganti "WHERE" menjadi "AND" supaya tidak tabrakan di query SQL
            string shiftSelectionSql = "";
            if (!SelectedShifts.Contains("All") && SelectedShifts.Any())
            {
                var shiftConditions = new List<string>();
                foreach (var shift in SelectedShifts)
                {
                    if (shift == "NS" || shift == "ns")
                        shiftConditions.Add("ShiftMode = 'NON-SHIFT' OR ShiftMode = 'OVERTIME'");
                    else
                        shiftConditions.Add(
                            $"ShiftMode = 'SHIFT {shift}' OR ShiftMode = 'OVERTIME SHIFT {shift}'"
                        );
                }
                shiftSelectionSql = $"AND ({string.Join(" OR ", shiftConditions)})";
            }

            string planShiftFilter = "";
            string selectQuantityColumn = "SUM(ISNULL(pr.Quantity, 0))";
            string selectOvertimeColumn = "SUM(ISNULL(pr.Overtime, 0))";
            
            if (!SelectedShifts.Contains("All") && SelectedShifts.Any())
            {
                var conditions = SelectedShifts.Select(s => {
                    string suffix = s == "NS" ? "NS" : s;
                    return $"(pr.Shift LIKE '%{s}%' OR pr.QtyShift{suffix} IS NOT NULL OR pr.OvtShift{suffix} IS NOT NULL)";
                });
                planShiftFilter = $"AND ({string.Join(" OR ", conditions)})";

                var shiftSumTerms = new List<string>();
                var ovtShiftSumTerms = new List<string>();
                
                foreach (var shift in SelectedShifts)
                {
                    string colName = "";
                    string colNameOvt = "";
                    if (shift == "1") { colName = "QtyShift1"; colNameOvt = "OvtShift1"; }
                    else if (shift == "2") { colName = "QtyShift2"; colNameOvt = "OvtShift2"; }
                    else if (shift == "3") { colName = "QtyShift3"; colNameOvt = "OvtShift3"; }
                    else if (shift == "NS") { colName = "QtyShiftNS"; colNameOvt = "OvtShiftNS"; }

                    if (!string.IsNullOrEmpty(colName))
                    {
                        shiftSumTerms.Add($@"
                            ISNULL(pr.{colName}, 
                                CASE 
                                    WHEN pr.Shift LIKE '%{shift}%' 
                                    THEN ISNULL(pr.Quantity, 0) / NULLIF((LEN(pr.Shift) - LEN(REPLACE(pr.Shift, ',', '')) + 1), 0) 
                                    ELSE 0 
                                END
                            )
                        ");
                        
                        ovtShiftSumTerms.Add($@"
                            ISNULL(pr.{colNameOvt}, 
                                CASE 
                                    WHEN pr.Shift LIKE '%{shift}%' 
                                    THEN ISNULL(pr.Overtime, 0) / NULLIF((LEN(pr.Shift) - LEN(REPLACE(pr.Shift, ',', '')) + 1), 0) 
                                    ELSE 0 
                                END
                            )
                        ");
                    }
                }
                
                if (shiftSumTerms.Any())
                {
                    selectQuantityColumn = $"SUM({string.Join(" + ", shiftSumTerms)})";
                }
                if (ovtShiftSumTerms.Any())
                {
                    selectOvertimeColumn = $"SUM({string.Join(" + ", ovtShiftSumTerms)})";
                }
            }

            string planSql = $@"
    SELECT DAY(pp.CurrentDate) as Day, 
           {selectQuantityColumn} as TotalPlanQuantity, 
           {selectOvertimeColumn} as TotalPlanOvertime
    FROM ProductionPlan pp
    INNER JOIN ProductionRecords pr ON pp.Id = pr.PlanId
    WHERE YEAR(pp.CurrentDate) = @SelectedYear 
      AND MONTH(pp.CurrentDate) = @SelectedMonth
      {(MachineLine != "All"
                    ? "AND pr.MachineCode = @MachineLine"
                    : "AND pr.MachineCode IN ('MCH1-01', 'MCH1-02')")}
      {planShiftFilter}
    GROUP BY DAY(pp.CurrentDate)";

            string anyPlanSql = $@"
    SELECT DISTINCT DAY(pp.CurrentDate) as Day
    FROM ProductionPlan pp
    INNER JOIN ProductionRecords pr ON pp.Id = pr.PlanId
    WHERE YEAR(pp.CurrentDate) = @SelectedYear 
      AND MONTH(pp.CurrentDate) = @SelectedMonth
      AND pr.MachineCode IN ('MCH1-01', 'MCH1-02')
      {planShiftFilter}";

            string actualSql = $@"
WITH ShiftData AS (
    SELECT
        CASE
            WHEN CAST(SDate AS TIME) < '07:00:00' THEN CAST(DATEADD(DAY, -1, SDate) AS DATE)
            ELSE CAST(SDate AS DATE)
        END AS ReportDate,
        SDate,
        Product_Id,
        TotalUnit,
        NoOfOperator,
        ShiftMode AS Mode_Asli_Mesin,
        CASE 
            WHEN ShiftMode = 'NON-SHIFT' THEN
                CASE
                    WHEN CAST(SDate AS TIME) > '16:00:00' THEN 'OVERTIME'
                    ELSE 'NON-SHIFT'
                END
            WHEN ShiftMode LIKE 'OVERTIME%' THEN
                CASE 
                    WHEN MONTH(CAST(DATEADD(hour, -7, SDate) AS DATE)) = 7 AND YEAR(CAST(DATEADD(hour, -7, SDate) AS DATE)) = 2026 AND DAY(CAST(DATEADD(hour, -7, SDate) AS DATE)) <= 5 THEN 'OVERTIME'
                    WHEN CAST(SDate AS TIME) >= '15:45:00' AND CAST(SDate AS TIME) <= '19:45:59' THEN 'OVERTIME SHIFT 1'
                    WHEN CAST(SDate AS TIME) >= '19:46:00' AND CAST(SDate AS TIME) <= '23:14:59' THEN 'OVERTIME SHIFT 3'
                    WHEN CAST(SDate AS TIME) >= '23:15:00' OR CAST(SDate AS TIME) < '07:00:00' THEN 'SHIFT 3'
                    ELSE 'OVERTIME'
                END
            WHEN ShiftMode = 'SHIFT 2' AND MONTH(CAST(DATEADD(hour, -7, SDate) AS DATE)) IN (7, 8) AND YEAR(CAST(DATEADD(hour, -7, SDate) AS DATE)) = 2026 THEN
                CASE 
                    WHEN CAST(SDate AS TIME) >= '07:00:00' AND CAST(SDate AS TIME) <= '15:44:59' THEN 'SHIFT 1'
                    WHEN CAST(SDate AS TIME) >= '15:45:00' AND CAST(SDate AS TIME) <= '19:45:59' THEN 'OVERTIME SHIFT 1'
                    WHEN CAST(SDate AS TIME) >= '19:46:00' AND CAST(SDate AS TIME) <= '23:14:59' THEN 'OVERTIME SHIFT 3'
                    ELSE 'SHIFT 3'
                END
            WHEN ShiftMode = 'SHIFT 3' AND CAST(SDate AS TIME) >= '19:46:00' AND CAST(SDate AS TIME) <= '23:14:59' THEN 'OVERTIME SHIFT 3'
            ELSE ShiftMode
        END AS Status_Di_Web,
        MachineCode
    FROM oeesn
    WHERE (
        (YEAR(SDate) = @SelectedYear AND MONTH(SDate) = @SelectedMonth AND CAST(SDate AS TIME) >= '07:00:00')
        OR
        (SDate >= DATEADD(DAY, 1, DATEFROMPARTS(@SelectedYear, @SelectedMonth, 1)) AND SDate < DATEADD(MONTH, 1, DATEFROMPARTS(@SelectedYear, @SelectedMonth, 1)) AND CAST(SDate AS TIME) < '07:00:00')
    )
    {dateFilter}
    {(MachineLine != "All" ? "AND MachineCode = @MachineLine" : "AND MachineCode IN ('MCH1-01', 'MCH1-02')")}
),
GroupedData AS (
    SELECT 
        ReportDate,
        MachineCode,
        Mode_Asli_Mesin,
        Status_Di_Web,
        Product_Id,
        CASE 
            WHEN YEAR(ReportDate) = 2026 AND MONTH(ReportDate) >= 3 AND MONTH(ReportDate) <= 7 THEN MAX(TotalUnit)
            ELSE COUNT(Product_Id)
        END AS Estimasi_Produksi,
        MAX(SDate) AS Max_SDate,
        MAX(NoOfOperator) AS MaxOp
    FROM ShiftData
    GROUP BY 
        ReportDate, 
        MachineCode, 
        Mode_Asli_Mesin, 
        Status_Di_Web, 
        Product_Id,
        CASE
            WHEN CAST(SDate AS TIME) >= '07:00:00' AND CAST(SDate AS TIME) <= '15:44:59' THEN 1
            WHEN CAST(SDate AS TIME) >= '15:45:00' AND CAST(SDate AS TIME) <= '19:45:59' THEN 2
            WHEN CAST(SDate AS TIME) >= '19:46:00' AND CAST(SDate AS TIME) <= '23:14:59' THEN 3
            ELSE 4
        END
),
MachineDaily AS (
    SELECT 
        ReportDate,
        MachineCode,
        
        SUM(CASE WHEN Status_Di_Web = 'SHIFT 1' THEN Estimasi_Produksi ELSE 0 END) as S1_Unit,
        MAX(CASE WHEN Status_Di_Web = 'SHIFT 1' THEN CAST(Max_SDate AS TIME) END) as S1_Time,

        SUM(CASE WHEN Status_Di_Web = 'SHIFT 2' THEN Estimasi_Produksi ELSE 0 END) as S2_Unit,
        MAX(CASE WHEN Status_Di_Web = 'SHIFT 2' THEN CAST(Max_SDate AS TIME) END) as S2_Time,

        SUM(CASE WHEN Status_Di_Web = 'SHIFT 3' THEN Estimasi_Produksi ELSE 0 END) as S3_Unit,
        MAX(CASE WHEN Status_Di_Web = 'SHIFT 3' THEN CAST(Max_SDate AS TIME) END) as S3_Time,

        SUM(CASE WHEN Status_Di_Web = 'NON-SHIFT' THEN Estimasi_Produksi ELSE 0 END) as NS_Unit,
        MAX(CASE WHEN Status_Di_Web = 'NON-SHIFT' THEN CAST(Max_SDate AS TIME) END) as NS_Time,
        MAX(CASE WHEN Status_Di_Web = 'NON-SHIFT' THEN Estimasi_Produksi END) as NS_MaxUnit,

        SUM(CASE WHEN Status_Di_Web LIKE 'OVERTIME%' THEN Estimasi_Produksi ELSE 0 END) as OT_Unit,
        MAX(CASE WHEN Status_Di_Web LIKE 'OVERTIME%' AND Estimasi_Produksi > 0 THEN CAST(Max_SDate AS TIME) END) as OT_Time,
        MAX(CASE WHEN Status_Di_Web = 'OVERTIME SHIFT 1' AND Estimasi_Produksi > 0 THEN CAST(Max_SDate AS TIME) END) as OT_S1_Time,
        MAX(CASE WHEN Status_Di_Web = 'OVERTIME SHIFT 3' AND Estimasi_Produksi > 0 THEN CAST(Max_SDate AS TIME) END) as OT_S3_Time,
        MAX(CASE WHEN Status_Di_Web = 'OVERTIME' AND Estimasi_Produksi > 0 THEN CAST(Max_SDate AS TIME) END) as OT_Normal_Time,

        CASE WHEN SUM(CASE WHEN Status_Di_Web = 'SHIFT 1' THEN Estimasi_Produksi ELSE 0 END) > 0 OR MAX(CASE WHEN Status_Di_Web = 'SHIFT 1' THEN CAST(Max_SDate AS TIME) END) IS NOT NULL THEN 1 ELSE 0 END as S1_ActiveCount,
        CASE WHEN SUM(CASE WHEN Status_Di_Web = 'SHIFT 2' THEN Estimasi_Produksi ELSE 0 END) > 0 OR MAX(CASE WHEN Status_Di_Web = 'SHIFT 2' THEN CAST(Max_SDate AS TIME) END) IS NOT NULL THEN 1 ELSE 0 END as S2_ActiveCount,
        CASE WHEN SUM(CASE WHEN Status_Di_Web = 'SHIFT 3' THEN Estimasi_Produksi ELSE 0 END) > 0 OR MAX(CASE WHEN Status_Di_Web = 'SHIFT 3' THEN CAST(Max_SDate AS TIME) END) IS NOT NULL THEN 1 ELSE 0 END as S3_ActiveCount,
        CASE WHEN SUM(CASE WHEN Status_Di_Web = 'NON-SHIFT' THEN Estimasi_Produksi ELSE 0 END) > 0 OR MAX(CASE WHEN Status_Di_Web = 'NON-SHIFT' THEN CAST(Max_SDate AS TIME) END) IS NOT NULL THEN 1 ELSE 0 END as NS_ActiveCount,
        CASE WHEN SUM(CASE WHEN Status_Di_Web LIKE 'OVERTIME%' THEN Estimasi_Produksi ELSE 0 END) > 0 OR MAX(CASE WHEN Status_Di_Web LIKE 'OVERTIME%' THEN CAST(Max_SDate AS TIME) END) IS NOT NULL THEN 1 ELSE 0 END as OT_ActiveCount,

        MAX(CASE WHEN Status_Di_Web = 'SHIFT 1' THEN MaxOp END) as S1_MaxOp,
        MAX(CASE WHEN Status_Di_Web = 'SHIFT 2' THEN MaxOp END) as S2_MaxOp,
        MAX(CASE WHEN Status_Di_Web = 'SHIFT 3' THEN MaxOp END) as S3_MaxOp,
        MAX(CASE WHEN Status_Di_Web = 'NON-SHIFT' THEN MaxOp END) as NS_MaxOp,
        MAX(CASE WHEN Status_Di_Web LIKE 'OVERTIME%' THEN MaxOp END) as OT_MaxOp,

        MAX(MaxOp) as MaxOp,
        SUM(Estimasi_Produksi) as TotalUnit
    FROM GroupedData
    WHERE 1=1 {shiftSelectionSql.Replace("ShiftMode", "Status_Di_Web")}
    GROUP BY ReportDate, MachineCode
),
DailyAggregates AS (
    SELECT 
        ReportDate,
        SUM(ISNULL(S1_Unit, 0)) as S1_Unit,
        MAX(S1_Time) as S1_Time,
        SUM(ISNULL(S2_Unit, 0)) as S2_Unit,
        MAX(S2_Time) as S2_Time,
        SUM(ISNULL(S3_Unit, 0)) as S3_Unit,
        MAX(S3_Time) as S3_Time,
        SUM(ISNULL(NS_Unit, 0)) as NS_Unit,
        MAX(NS_Time) as NS_Time,
        SUM(ISNULL(OT_Unit, 0)) as OT_Unit,
        MAX(OT_Time) as OT_Time,
        MAX(OT_S1_Time) as OT_S1_Time,
        MAX(OT_S3_Time) as OT_S3_Time,
        MAX(OT_Normal_Time) as OT_Normal_Time,
        SUM(ISNULL(S1_MaxOp, 0)) as S1_MaxOp,
        SUM(ISNULL(S2_MaxOp, 0)) as S2_MaxOp,
        SUM(ISNULL(S3_MaxOp, 0)) as S3_MaxOp,
        SUM(ISNULL(NS_MaxOp, 0)) as NS_MaxOp,
        SUM(ISNULL(OT_MaxOp, 0)) as OT_MaxOp,
        SUM(ISNULL(S1_ActiveCount, 0)) as S1_ActiveCount,
        SUM(ISNULL(S2_ActiveCount, 0)) as S2_ActiveCount,
        SUM(ISNULL(S3_ActiveCount, 0)) as S3_ActiveCount,
        SUM(ISNULL(NS_ActiveCount, 0)) as NS_ActiveCount,
        SUM(ISNULL(OT_ActiveCount, 0)) as OT_ActiveCount,
        SUM(MaxOp) as MaxOp,
        MAX(TotalUnit) as TotalUnit
    FROM MachineDaily
    GROUP BY ReportDate
)
SELECT DAY(ReportDate) as Day, * FROM DailyAggregates ORDER BY ReportDate ASC;";
            string sapShiftFilter = "";
            if (!SelectedShifts.Contains("All") && SelectedShifts.Any())
            {
                var sapShiftConditions = SelectedShifts.Select(s => $"sp.Shift = '{s}'");
                sapShiftFilter = $"AND ({string.Join(" OR ", sapShiftConditions)})";
            }
            string sapPlanSql = $@"
                            SELECT DAY(pp.CurrentDate) as Day,
                                   SUM(ISNULL(sp.SapPlanNormal, 0)) as TotalSapNormal,
                                   SUM(ISNULL(sp.SapPlanOvertime, 0)) as TotalSapOvertime
                            FROM ProductionPlan pp
                            INNER JOIN SapPlan sp ON pp.Id = sp.PlanId
                            WHERE YEAR(pp.CurrentDate) = @SelectedYear
                              AND MONTH(pp.CurrentDate) = @SelectedMonth
                              {(MachineLine != "All"
                           ? "AND sp.MachineCode = @MachineLine"
                           : "AND sp.MachineCode IN ('MCH1-01', 'MCH1-02')")}
                              {sapShiftFilter}
                            GROUP BY DAY(pp.CurrentDate)";

            try
            {
                using (var conn = new SqlConnection(this.connectionString))
                {
                    conn.Open();

                    using (var planCmd = new SqlCommand(planSql, conn))
                    {
                        planCmd.Parameters.AddWithValue("@SelectedYear", SelectedYear);
                        planCmd.Parameters.AddWithValue("@SelectedMonth", SelectedMonth);
                        if (MachineLine != "All") planCmd.Parameters.AddWithValue("@MachineLine", MachineLine);

                        using (var reader = planCmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                var d = combinedData.FirstOrDefault(x => x.Day == (int)reader["Day"]);
                                if (d != null)
                                {
                                    d.Plan = reader["TotalPlanQuantity"] != DBNull.Value ? Convert.ToInt32(reader["TotalPlanQuantity"]) : (int?)null;
                                    d.PlanOvertime = reader["TotalPlanOvertime"] != DBNull.Value ? Convert.ToInt32(reader["TotalPlanOvertime"]) : (int?)null;
                                }
                            }
                        }
                    }

                    using (var anyPlanCmd = new SqlCommand(anyPlanSql, conn))
                    {
                        anyPlanCmd.Parameters.AddWithValue("@SelectedYear", SelectedYear);
                        anyPlanCmd.Parameters.AddWithValue("@SelectedMonth", SelectedMonth);

                        using (var reader = anyPlanCmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                var d = combinedData.FirstOrDefault(x => x.Day == (int)reader["Day"]);
                                if (d != null)
                                {
                                    d.HasAnyPlan = true;
                                }
                            }
                        }
                    }

                    using (var actualCmd = new SqlCommand(actualSql, conn))
                    {
                        actualCmd.Parameters.AddWithValue("@SelectedYear", SelectedYear);
                        actualCmd.Parameters.AddWithValue("@SelectedMonth", SelectedMonth);
                        if (isCurrentMonthView) actualCmd.Parameters.AddWithValue("@TodayDate", DateTime.Now.Date);
                        if (MachineLine != "All") actualCmd.Parameters.AddWithValue("@MachineLine", MachineLine);

                        using (var reader = actualCmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                var d = combinedData.FirstOrDefault(x => x.Day == (int)reader["Day"]);
                                if (d != null)
                                {
                                    // 🛠️ PERBAIKAN: Menggunakan Convert.ToDecimal() agar kebal dari InvalidCastException
                                    d.Shift1_Unit = reader["S1_Unit"] != DBNull.Value ? Convert.ToDecimal(reader["S1_Unit"]) : 0;
                                    d.Shift1_EndTime = reader["S1_Time"] != DBNull.Value ? (TimeSpan)reader["S1_Time"] : TimeSpan.Zero;
                                    d.Shift2_Unit = reader["S2_Unit"] != DBNull.Value ? Convert.ToDecimal(reader["S2_Unit"]) : 0;
                                    d.Shift2_EndTime = reader["S2_Time"] != DBNull.Value ? (TimeSpan)reader["S2_Time"] : TimeSpan.Zero;
                                    d.Shift3_Unit = reader["S3_Unit"] != DBNull.Value ? Convert.ToDecimal(reader["S3_Unit"]) : 0;
                                    d.Shift3_EndTime = reader["S3_Time"] != DBNull.Value ? (TimeSpan)reader["S3_Time"] : TimeSpan.Zero;
                                    d.NonShift_Unit = reader["NS_Unit"] != DBNull.Value ? Convert.ToDecimal(reader["NS_Unit"]) : 0;
                                    d.NonShift_EndTime = reader["NS_Time"] != DBNull.Value ? (TimeSpan)reader["NS_Time"] : TimeSpan.Zero;
                                    d.Overtime_Unit = reader["OT_Unit"] != DBNull.Value ? Convert.ToDecimal(reader["OT_Unit"]) : 0;
                                    d.Overtime_EndTime = reader["OT_Time"] != DBNull.Value ? (TimeSpan)reader["OT_Time"] : TimeSpan.Zero;
                                    d.OT_S1_Time = reader["OT_S1_Time"] != DBNull.Value ? (TimeSpan)reader["OT_S1_Time"] : (TimeSpan?)null;
                                    d.OT_S3_Time = reader["OT_S3_Time"] != DBNull.Value ? (TimeSpan)reader["OT_S3_Time"] : (TimeSpan?)null;
                                    d.OT_Normal_Time = reader["OT_Normal_Time"] != DBNull.Value ? (TimeSpan)reader["OT_Normal_Time"] : (TimeSpan?)null;
                                    d.Shift1_MaxOp = reader["S1_MaxOp"] != DBNull.Value ? Convert.ToInt32(reader["S1_MaxOp"]) : 0;
                                    d.Shift2_MaxOp = reader["S2_MaxOp"] != DBNull.Value ? Convert.ToInt32(reader["S2_MaxOp"]) : 0;
                                    d.Shift3_MaxOp = reader["S3_MaxOp"] != DBNull.Value ? Convert.ToInt32(reader["S3_MaxOp"]) : 0;
                                    d.NonShift_MaxOp = reader["NS_MaxOp"] != DBNull.Value ? Convert.ToInt32(reader["NS_MaxOp"]) : 0;
                                    d.Overtime_MaxOp = reader["OT_MaxOp"] != DBNull.Value ? Convert.ToInt32(reader["OT_MaxOp"]) : 0;
                                    d.Shift1_ActiveCount = reader["S1_ActiveCount"] != DBNull.Value ? Convert.ToInt32(reader["S1_ActiveCount"]) : 0;
                                    d.Shift2_ActiveCount = reader["S2_ActiveCount"] != DBNull.Value ? Convert.ToInt32(reader["S2_ActiveCount"]) : 0;
                                    d.Shift3_ActiveCount = reader["S3_ActiveCount"] != DBNull.Value ? Convert.ToInt32(reader["S3_ActiveCount"]) : 0;
                                    d.NonShift_ActiveCount = reader["NS_ActiveCount"] != DBNull.Value ? Convert.ToInt32(reader["NS_ActiveCount"]) : 0;
                                    d.Overtime_ActiveCount = reader["OT_ActiveCount"] != DBNull.Value ? Convert.ToInt32(reader["OT_ActiveCount"]) : 0;
                                    d.NoOfOperator = reader["MaxOp"] != DBNull.Value ? Convert.ToInt32(reader["MaxOp"]) : 0;
                                }
                            }
                        }
                    }

                    using (var sapCmd = new SqlCommand(sapPlanSql, conn))
                    {
                        sapCmd.Parameters.AddWithValue("@SelectedYear", SelectedYear);
                        sapCmd.Parameters.AddWithValue("@SelectedMonth", SelectedMonth);
                        if (MachineLine != "All") sapCmd.Parameters.AddWithValue("@MachineLine", MachineLine);

                        using (var reader = sapCmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                var d = combinedData.FirstOrDefault(x => x.Day == (int)reader["Day"]);
                                if (d != null)
                                {
                                    d.OriginalPlan = Convert.ToInt32(reader["TotalSapNormal"]);
                                    d.OtOriginalPlan = Convert.ToInt32(reader["TotalSapOvertime"]);
                                }
                            }
                        }
                    }
                } // using conn berakhir di sini
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error LoadChartData: " + ex.Message);
            }

            foreach (var data in combinedData)
            {
                ChartLabels.Add(data.Day.ToString());
                PlanData.Add(data.Plan);
                PlanOvertimeData.Add(data.PlanOvertime);
                OriginalPlanData.Add(data.OriginalPlan);
                OriginalPlanOvertimeData.Add(data.OtOriginalPlan);

                int totalOtMinutes = 0;

                if (data.OT_S1_Time != null)
                {
                    TimeSpan start = new TimeSpan(15, 45, 0);
                    TimeSpan breakStart = new TimeSpan(18, 15, 0);
                    TimeSpan breakEnd = new TimeSpan(18, 45, 0);
                    TimeSpan endTime = data.OT_S1_Time.Value;

                    if (endTime > start)
                    {
                        if (endTime <= breakStart)
                        {
                            totalOtMinutes += (int)(endTime - start).TotalMinutes;
                        }
                        else if (endTime > breakStart && endTime <= breakEnd)
                        {
                            totalOtMinutes += (int)(breakStart - start).TotalMinutes;
                        }
                        else
                        {
                            totalOtMinutes += (int)(endTime - start).TotalMinutes - 30; // Kurangi 30 menit istirahat
                        }
                    }
                    else 
                    {
                        totalOtMinutes += (int)(new TimeSpan(24, 0, 0) - start).TotalMinutes + (int)endTime.TotalMinutes - 30;
                    }
                }

                if (data.OT_S3_Time != null)
                {
                    TimeSpan start = new TimeSpan(19, 45, 0);
                    if (data.OT_S3_Time > start) totalOtMinutes += (int)(data.OT_S3_Time.Value - start).TotalMinutes;
                    else totalOtMinutes += (int)(new TimeSpan(24, 0, 0) - start).TotalMinutes + (int)data.OT_S3_Time.Value.TotalMinutes;
                }

                if (data.OT_Normal_Time != null)
                {
                    // For pure OVERTIME mode (non-split), assuming full day or just fallback
                    TimeSpan start = new TimeSpan(7, 0, 0);
                    if (data.OT_Normal_Time > start) totalOtMinutes += (int)(data.OT_Normal_Time.Value - start).TotalMinutes;
                    else totalOtMinutes += (int)(new TimeSpan(24, 0, 0) - start).TotalMinutes + (int)data.OT_Normal_Time.Value.TotalMinutes;
                }

                int baseOtMinutes = totalOtMinutes;
                if (totalOtMinutes > 0)
                {
                    int multiplierOT = (MachineLine == "All" && data.Overtime_ActiveCount > 0) ? data.Overtime_ActiveCount : 1;
                    totalOtMinutes = totalOtMinutes * multiplierOT;
                }

                var dayType = DetermineTypeOfDay(new DateTime(SelectedYear, SelectedMonth, data.Day).DayOfWeek);
                bool isWeekend = (dayType == "WEEKEND");

                int stdWorkingMinutes = 0;
                double dailyManMinutes = 0;

                if (data.Shift1_Unit > 0 || (!isWeekend && data.Shift1_EndTime != TimeSpan.Zero))
                {
                    int mins = (dayType == "FRIDAY") ? 418 : 458;
                    int multiplier = (MachineLine == "All" && data.Shift1_ActiveCount > 0) ? data.Shift1_ActiveCount : 1;
                    stdWorkingMinutes += (mins * multiplier);
                    int ops = data.Shift1_MaxOp > 0 ? data.Shift1_MaxOp : data.NoOfOperator;
                    dailyManMinutes += ops * mins;
                }
                    
                if (data.Shift2_Unit > 0 || (!isWeekend && data.Shift2_EndTime != TimeSpan.Zero))
                {
                    int multiplier = (MachineLine == "All" && data.Shift2_ActiveCount > 0) ? data.Shift2_ActiveCount : 1;
                    stdWorkingMinutes += (393 * multiplier);
                    int ops = data.Shift2_MaxOp > 0 ? data.Shift2_MaxOp : data.NoOfOperator;
                    dailyManMinutes += ops * 393;
                }
                    
                if (data.Shift3_Unit > 0 || (!isWeekend && data.Shift3_EndTime != TimeSpan.Zero))
                {
                    int multiplier = (MachineLine == "All" && data.Shift3_ActiveCount > 0) ? data.Shift3_ActiveCount : 1;
                    stdWorkingMinutes += (398 * multiplier);
                    int ops = data.Shift3_MaxOp > 0 ? data.Shift3_MaxOp : data.NoOfOperator;
                    dailyManMinutes += ops * 398;
                }
                    
                if (data.NonShift_Unit > 0 || (!isWeekend && data.NonShift_EndTime != TimeSpan.Zero))
                {
                    int multiplier = (MachineLine == "All" && data.NonShift_ActiveCount > 0) ? data.NonShift_ActiveCount : 1;
                    stdWorkingMinutes += (473 * multiplier);
                    int ops = data.NonShift_MaxOp > 0 ? data.NonShift_MaxOp : data.NoOfOperator;
                    dailyManMinutes += ops * 473;
                }

                if (baseOtMinutes > 0 || data.Overtime_Unit > 0)
                {
                    int otOps = data.Overtime_MaxOp > 0 ? data.Overtime_MaxOp : data.NoOfOperator;
                    dailyManMinutes += otOps * baseOtMinutes;
                }

                if (isWeekend)
                {
                    totalOtMinutes += stdWorkingMinutes;
                    stdWorkingMinutes = 0;
                }

                OvertimeMinutes.Add(totalOtMinutes);

                int overtimeOpCount = 0;
                if (isWeekend)
                {
                    overtimeOpCount = data.NoOfOperator;
                }
                else
                {
                    overtimeOpCount = (data.Overtime_Unit > 0 || totalOtMinutes > 0) ? data.NoOfOperator : 0;
                }
                OvertimeOperators.Add(overtimeOpCount);

                decimal normalUnits = 0;
                decimal overtimeUnits = 0;

                bool hasNormalActivity = data.Shift1_Unit > 0
                                      || data.Shift2_Unit > 0
                                      || data.Shift3_Unit > 0
                                      || data.NonShift_Unit > 0;

                if (isWeekend)
                {
                    normalUnits = 0;
                    overtimeUnits = data.Shift1_Unit
                                + data.Shift2_Unit
                                + data.Shift3_Unit
                                + data.NonShift_Unit
                                + data.Overtime_Unit;
                }
                else if (hasNormalActivity)
                {
                    normalUnits = data.Shift1_Unit
                                + data.Shift2_Unit
                                + data.Shift3_Unit
                                + data.NonShift_Unit;

                    overtimeUnits = data.Overtime_Unit;
                }
                else
                {
                    normalUnits = 0;
                    overtimeUnits = data.Overtime_Unit;
                }
                NormalData.Add(normalUnits);
                OvertimeData.Add(overtimeUnits);
                NoOfDirectWorkers.Add(data.NoOfOperator);

                dailyLosses.TryGetValue(data.Day, out int lossDurationSec);

                bool isShiftActive = (data.Shift1_Unit > 0 || data.Shift1_EndTime != TimeSpan.Zero) ||
                                     (data.Shift2_Unit > 0 || data.Shift2_EndTime != TimeSpan.Zero) ||
                                     (data.Shift3_Unit > 0 || data.Shift3_EndTime != TimeSpan.Zero) ||
                                     (data.NonShift_Unit > 0 || data.NonShift_EndTime != TimeSpan.Zero) ||
                                     (data.Overtime_Unit > 0 || totalOtMinutes > 0);

                if (!isShiftActive)
                {
                    lossDurationSec = 0;
                }

                DailyLossTime.Add(lossDurationSec / 60.0);

                int baseWorkMinutes = (normalUnits > 0 || overtimeUnits > 0) ? stdWorkingMinutes : 0;
                int totalWorkMinutes = baseWorkMinutes + totalOtMinutes;
                DailyWorkTime.Add(totalWorkMinutes);

                double lossRatio = (totalWorkMinutes > 0) ? (double)(lossDurationSec / 60) / totalWorkMinutes : 0;
                double netManMinutes = dailyManMinutes * (1.0 - lossRatio);
                DailyNetManHours.Add(netManMinutes / 60.0);
            }

            for (int i = 0; i < PlanData.Count; i++)
            {
                var data = combinedData[i];
                if (!data.HasAnyPlan)
                {
                    int effectiveNormal = PlanData[i].HasValue ? PlanData[i].Value : OriginalPlanData[i];
                    EffectivePlanData.Add(effectiveNormal);

                    int effectiveOt = PlanOvertimeData[i].HasValue ? PlanOvertimeData[i].Value : OriginalPlanOvertimeData[i];
                    EffectivePlanOvertimeData.Add(effectiveOt);
                }
                else
                {
                    int effectiveNormal = PlanData[i].HasValue ? PlanData[i].Value : 0;
                    EffectivePlanData.Add(effectiveNormal);

                    int effectiveOt = PlanOvertimeData[i].HasValue ? PlanOvertimeData[i].Value : 0;
                    EffectivePlanOvertimeData.Add(effectiveOt);
                }
            }
        }

        private readonly List<(TimeSpan Start, TimeSpan End)> RegularDayBreakTimes = new List<(TimeSpan, TimeSpan)>
        {
            (new TimeSpan(9, 30, 0), new TimeSpan(9, 35, 0)),
            (new TimeSpan(12, 0, 0), new TimeSpan(12, 45, 0)),
            (new TimeSpan(14, 30, 0), new TimeSpan(14, 35, 0)),
            (new TimeSpan(18, 15, 0), new TimeSpan(18, 45, 0))
        };

        private readonly List<(TimeSpan Start, TimeSpan End)> FridayBreakTimes = new List<(TimeSpan, TimeSpan)>
        {
            (new TimeSpan(9, 30, 0), new TimeSpan(9, 35, 0)),
            (new TimeSpan(11, 50, 0), new TimeSpan(13, 15, 0)),
            (new TimeSpan(14, 30, 0), new TimeSpan(14, 35, 0)),
            (new TimeSpan(18, 15, 0), new TimeSpan(18, 45, 0))
        };

        private bool IsInBreakTime(TimeSpan startTime, TimeSpan endTime, List<(TimeSpan Start, TimeSpan End)> breakTimes)
        {
            foreach (var (breakStart, breakEnd) in breakTimes)
            {
                if (startTime < breakEnd && endTime > breakStart) return true;
            }
            return false;
        }

        private Dictionary<int, int> GetDailyLossTimeTotals()
        {
            var dailyTotals = new Dictionary<int, int>();

            bool hasActuals = false;
            try
            {
                string checkSql = $@"
            SELECT COUNT(1) FROM LossTimeActuals 
            WHERE Month = @Month AND Year = @Year
            {(MachineLine != "All" ? "AND MachineLine = @MachineLine" : "AND MachineLine IN ('MCH1-01', 'MCH1-02')")}";

                using (var conn = new SqlConnection(this.connectionString))
                {
                    conn.Open();
                    using (var cmd = new SqlCommand(checkSql, conn))
                    {
                        cmd.Parameters.AddWithValue("@Month", SelectedMonth);
                        cmd.Parameters.AddWithValue("@Year", SelectedYear);
                        if (MachineLine != "All") cmd.Parameters.AddWithValue("@MachineLine", MachineLine);
                        hasActuals = (int)cmd.ExecuteScalar() > 0;
                    }
                }
            }
            catch (Exception ex) { Console.WriteLine($"Error check LossTimeActuals: {ex.Message}"); }

            if (hasActuals)
            {
                try
                {
                    string shiftFilter = "";
                    if (SelectedShifts.Any() && !SelectedShifts.Contains("All"))
                    {
                        var shiftList = string.Join(",", SelectedShifts.Select(s => $"'{s}'"));
                        shiftFilter = $"AND Shift IN ({shiftList})";
                    }

                    string actualsSql = $@"
                SELECT Day, SUM(Minutes) as TotalMinutes
                FROM LossTimeActuals
                WHERE Month = @Month AND Year = @Year AND Minutes > 0
                {(MachineLine != "All" ? "AND MachineLine = @MachineLine" : "AND MachineLine IN ('MCH1-01', 'MCH1-02')")}
                {shiftFilter}
                GROUP BY Day";

                    using (var conn = new SqlConnection(this.connectionString))
                    {
                        conn.Open();
                        using (var cmd = new SqlCommand(actualsSql, conn))
                        {
                            cmd.Parameters.AddWithValue("@Month", SelectedMonth);
                            cmd.Parameters.AddWithValue("@Year", SelectedYear);
                            if (MachineLine != "All") cmd.Parameters.AddWithValue("@MachineLine", MachineLine);

                            using (var reader = cmd.ExecuteReader())
                            {
                                while (reader.Read())
                                {
                                    int day = Convert.ToInt32(reader["Day"]);
                                    double totalMinutes = Convert.ToDouble(reader["TotalMinutes"]);
                                    dailyTotals[day] = (int)(totalMinutes * 60);
                                }
                            }
                        }
                    }
                }
                catch (Exception ex) { Console.WriteLine($"Error GetDailyLossTimeTotals (Actuals): {ex.Message}"); }

                return dailyTotals;
            }

            string shiftFilterSql = "";
            if (SelectedShifts.Any() && !SelectedShifts.Contains("All"))
            {
                var conditions = new List<string>();
                foreach (var shift in SelectedShifts)
                {
                    if (shift == "1") 
                    {
                        if (SelectedYear == 2026 && (SelectedMonth == 7 || SelectedMonth == 8))
                            conditions.Add("(CAST(Time AS TIME) >= '07:00:00' AND CAST(Time AS TIME) <= '19:45:00')");
                        else
                            conditions.Add("(CAST(Time AS TIME) >= '07:00:00' AND CAST(Time AS TIME) <= '15:45:00')");
                    }
                    else if (shift == "2") 
                    {
                        if (SelectedYear == 2026 && (SelectedMonth == 7 || SelectedMonth == 8))
                            conditions.Add("1=0");
                        else
                            conditions.Add("(CAST(Time AS TIME) > '15:45:00' AND CAST(Time AS TIME) <= '23:15:00')");
                    }
                    else if (shift == "3") 
                    {
                        if (SelectedYear == 2026 && (SelectedMonth == 7 || SelectedMonth == 8))
                            conditions.Add("(CAST(Time AS TIME) > '19:45:00' OR CAST(Time AS TIME) <= '07:00:00')");
                        else
                            conditions.Add("(CAST(Time AS TIME) > '23:15:00' OR CAST(Time AS TIME) <= '07:00:00')");
                    }
                }
                
                if (conditions.Any())
                {
                    shiftFilterSql = $"AND ({string.Join(" OR ", conditions)})";
                }
            }

            string lossTimeMachineFilter = (MachineLine == "All")
                ? "AND MachineCode IN ('MCH1-01', 'MCH1-02')"
                : "AND MachineCode = @Machine";

            string query = $@"
        SELECT 
            CAST(Date AS DATE) as FullDate,
            CAST(Time AS TIME) as StartTime, 
            CAST(EndDateTime AS TIME) as EndTime, 
            LossTime as Duration
        FROM AssemblyLossTime
        WHERE YEAR(Date) = @Year 
          AND MONTH(Date) = @Month 
          {lossTimeMachineFilter}
          {shiftFilterSql}";

            try
            {
                using (var connection = new SqlConnection(this.connectionString))
                {
                    using (var command = new SqlCommand(query, connection))
                    {
                        command.Parameters.AddWithValue("@Year", SelectedYear);
                        command.Parameters.AddWithValue("@Month", SelectedMonth);
                        if (MachineLine != "All") command.Parameters.AddWithValue("@Machine", MachineLine);

                        connection.Open();
                        using (var reader = command.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                var fullDate = (DateTime)reader["FullDate"];
                                var startTime = (TimeSpan)reader["StartTime"];

                                if (startTime >= TimeSpan.Zero && startTime < new TimeSpan(7, 0, 0))
                                    fullDate = fullDate.AddDays(-1);

                                var day = fullDate.Day;
                                var endTime = (TimeSpan)reader["EndTime"];
                                var duration = Convert.ToInt32(reader["Duration"]);

                                var dayType = DetermineTypeOfDay(fullDate.DayOfWeek);
                                
                                // Copy-paste Break Times dari Detail Loss Time
                                var breakTimes = new List<(TimeSpan Start, TimeSpan End)>
                                {
                                    (new TimeSpan(7, 0, 0), new TimeSpan(7, 5, 0)),
                                    (new TimeSpan(9, 30, 0), new TimeSpan(9, 35, 0)),
                                    (new TimeSpan(15, 30, 0), new TimeSpan(15, 35, 0)),
                                    (new TimeSpan(18, 15, 0), new TimeSpan(18, 45, 0))
                                };
                                // Tambahkan additional breaks
                                foreach (var ab in GetAdditionalBreakTimesForDate(fullDate))
                                {
                                    breakTimes.Add(ab);
                                }

                                bool isInBreakTime = false;
                                foreach (var (breakStart, breakEnd) in breakTimes)
                                {
                                    if (startTime < breakEnd && endTime > breakStart)
                                    {
                                        isInBreakTime = true;
                                        break;
                                    }
                                }

                                if (!isInBreakTime)
                                {
                                    if (!dailyTotals.ContainsKey(day)) dailyTotals[day] = 0;
                                    dailyTotals[day] += duration;
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex) { Console.WriteLine($"Error fetching loss time: {ex.Message}"); }

            return dailyTotals;
        }

        private List<(TimeSpan Start, TimeSpan End)> GetAdditionalBreakTimesForDate(DateTime date)
        {
            var additionalBreaks = new List<(TimeSpan, TimeSpan)>();
            try
            {
                using (var connection = new SqlConnection(this.connectionString))
                {
                    connection.Open();
                    string sql = @"
                SELECT TOP 1 BreakTime1Start, BreakTime1End, BreakTime2Start, BreakTime2End 
                FROM AdditionalBreakTimes 
                WHERE CAST(Date AS DATE) = @Date
                ORDER BY CreatedAt DESC";
                    using (var command = new SqlCommand(sql, connection))
                    {
                        command.Parameters.AddWithValue("@Date", date.Date);
                        using (var reader = command.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                if (!reader.IsDBNull(0) && !reader.IsDBNull(1))
                                    additionalBreaks.Add((reader.GetTimeSpan(0), reader.GetTimeSpan(1)));
                                if (!reader.IsDBNull(2) && !reader.IsDBNull(3))
                                    additionalBreaks.Add((reader.GetTimeSpan(2), reader.GetTimeSpan(3)));
                            }
                        }
                    }
                }
            }
            catch (Exception ex) { Console.WriteLine($"Error getting additional breaks: {ex.Message}"); }
            return additionalBreaks;
        }

        public int GetTotalRestTime(List<RestTime> listRestTime, TimeSpan StartTime, TimeSpan EndTime, TimeSpan CurrentTime) { int TotalRestTime = 0; bool isToday = (SelectedYear == DateTime.Now.Year && SelectedMonth == DateTime.Now.Month); TotalRestTime = listRestTime.Sum(rest => { if (isToday && rest.StartTime > CurrentTime) { return 0; } TimeSpan effectiveRestStart = rest.StartTime < StartTime ? StartTime : rest.StartTime; TimeSpan effectiveRestEnd = rest.EndTime > EndTime ? EndTime : rest.EndTime; if (isToday && effectiveRestEnd > CurrentTime) { effectiveRestEnd = CurrentTime; } return effectiveRestStart < effectiveRestEnd ? (int)(effectiveRestEnd - effectiveRestStart).TotalMinutes : 0; }); return TotalRestTime; }
        public string DetermineTypeOfDay(DayOfWeek day) { return day switch { DayOfWeek.Monday or DayOfWeek.Tuesday or DayOfWeek.Wednesday or DayOfWeek.Thursday => "REGULAR", DayOfWeek.Friday => "FRIDAY", DayOfWeek.Saturday or DayOfWeek.Sunday => "WEEKEND", _ => "REGULAR" }; }
        public List<RestTime> GetRestTime(string dayTipe) { List<RestTime> listRestTime = new List<RestTime>(); try { using (SqlConnection connection = new SqlConnection(this.connectionString)) { connection.Open(); string GetRestTime = @"SELECT Duration, StartTime, EndTime FROM RestTime WHERE DayType = @DayTipe"; using (SqlCommand command = new SqlCommand(GetRestTime, connection)) { command.Parameters.AddWithValue("@DayTipe", dayTipe); using (SqlDataReader dataReader = command.ExecuteReader()) { while (dataReader.Read()) { if (!dataReader.IsDBNull(0)) { listRestTime.Add(new RestTime { Duration = dataReader.GetInt32(0), StartTime = dataReader.GetTimeSpan(1), EndTime = dataReader.GetTimeSpan(2) }); } } } } } } catch (Exception ex) { Console.WriteLine("Exception GetRestTime: " + ex.ToString()); } return listRestTime; }
    }
}
