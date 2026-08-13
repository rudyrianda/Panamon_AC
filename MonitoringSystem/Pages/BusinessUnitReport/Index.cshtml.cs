using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Data.SqlClient;
using System.Globalization;

namespace MonitoringSystem.Pages.BusinessUnitReport
{
    public class BuChartData
    {
        public string BuName { get; set; } = "";
        public List<string> Labels { get; set; } = new List<string>();
        public List<int> ChangePlanData { get; set; } = new List<int>();
        public List<decimal> ActualNormalData { get; set; } = new List<decimal>();
        public List<decimal> ActualOvertimeData { get; set; } = new List<decimal>();
    }

    public class IndexModel : PageModel
    {
        private readonly IConfiguration _configuration;
        private string? connectionString;

        [BindProperty(SupportsGet = true)] public int SelectedMonth { get; set; } = DateTime.Now.Month;
        [BindProperty(SupportsGet = true)] public int SelectedYear { get; set; } = DateTime.Now.Year;
        [BindProperty(SupportsGet = true)] public List<string> SelectedShifts { get; set; } = new List<string>();

        public List<BuChartData> BuCharts { get; set; } = new List<BuChartData>();
        
        public int DaysInMonth { get; private set; }
        public bool IsCurrentMonthView { get; private set; }

        private class DailyData
        {
            public int Day { get; set; }
            public decimal Shift1_Unit { get; set; }
            public decimal Shift2_Unit { get; set; }
            public decimal Shift3_Unit { get; set; }
            public decimal NonShift_Unit { get; set; }
            public decimal Overtime_Unit { get; set; } = 0;
            public int Plan { get; set; }
            public int PlanOvertime { get; set; } = 0;
            public int OriginalPlan { get; set; } = 0;
        }

        public IndexModel(IConfiguration configuration)
        {
            _configuration = configuration;
            connectionString = _configuration.GetConnectionString("DefaultConnection");
        }

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
                return RedirectToPage(new { SelectedYear = DateTime.Now.Year, SelectedMonth = DateTime.Now.Month });

            return RedirectToPage(new
            {
                SelectedYear = this.SelectedYear,
                SelectedMonth = this.SelectedMonth,
                SelectedShifts = this.SelectedShifts
            });
        }

        private void LoadChartData()
        {
            this.IsCurrentMonthView = (SelectedYear == DateTime.Now.Year && SelectedMonth == DateTime.Now.Month);
            this.DaysInMonth = DateTime.DaysInMonth(SelectedYear, SelectedMonth);

            // AC Data (MCH1-01, MCH1-02) from DefaultConnection
            string defaultConn = _configuration.GetConnectionString("DefaultConnection");
            var acData = FetchBuData("AC", defaultConn, 
                "AND pr.MachineCode IN ('MCH1-01', 'MCH1-02')", 
                "AND MachineCode IN ('MCH1-01', 'MCH1-02')", 
                "AND sp.MachineCode IN ('MCH1-01', 'MCH1-02')");
            BuCharts.Add(acData);

            // LS Data from LSConnection
            string lsConn = _configuration.GetConnectionString("LSConnection");
            if (!string.IsNullOrEmpty(lsConn)) {
                var lsData = FetchLsData("LS", lsConn);
                BuCharts.Add(lsData);
            } else {
                // Fallback if not configured
                BuCharts.Add(new BuChartData { BuName = "LS", Labels = acData.Labels, ChangePlanData = acData.ChangePlanData, ActualNormalData = acData.ActualNormalData, ActualOvertimeData = acData.ActualOvertimeData });
            }

            // AUD Data from AUDConnection
            string audConn = _configuration.GetConnectionString("AUDConnection");
            BuChartData audData = null;
            if (!string.IsNullOrEmpty(audConn)) {
                audData = FetchAudData("AUD", audConn);
                BuCharts.Add(audData);
            } else {
                audData = new BuChartData { BuName = "AUD", Labels = acData.Labels, ChangePlanData = acData.ChangePlanData, ActualNormalData = acData.ActualNormalData, ActualOvertimeData = acData.ActualOvertimeData };
                BuCharts.Add(audData);
            }

            // Ref Data from RefConnection
            string refConn = _configuration.GetConnectionString("RefConnection");
            if (!string.IsNullOrEmpty(refConn)) {
                BuCharts.Add(FetchBuData("Ref", refConn, "", "", ""));
            } else {
                BuCharts.Add(new BuChartData { BuName = "Ref", Labels = acData.Labels, ChangePlanData = acData.ChangePlanData, ActualNormalData = acData.ActualNormalData, ActualOvertimeData = acData.ActualOvertimeData });
            }

            // Fan Data from FanConnection
            string fanConn = _configuration.GetConnectionString("FanConnection");
            if (!string.IsNullOrEmpty(fanConn)) {
                BuCharts.Add(FetchBuData("Fan", fanConn, "", "", ""));
            } else {
                BuCharts.Add(new BuChartData { BuName = "Fan", Labels = acData.Labels, ChangePlanData = acData.ChangePlanData, ActualNormalData = acData.ActualNormalData, ActualOvertimeData = acData.ActualOvertimeData });
            }

            // WP Data from WPConnection
            string wpConn = _configuration.GetConnectionString("WPConnection");
            if (!string.IsNullOrEmpty(wpConn)) {
                BuCharts.Add(FetchBuData("WP", wpConn, "", "", ""));
            } else {
                BuCharts.Add(new BuChartData { BuName = "WP", Labels = acData.Labels, ChangePlanData = acData.ChangePlanData, ActualNormalData = acData.ActualNormalData, ActualOvertimeData = acData.ActualOvertimeData });
            }
        }

        private BuChartData FetchLsData(string buName, string connStr)
        {
            var buChart = new BuChartData { BuName = buName };
            var combinedData = Enumerable.Range(1, this.DaysInMonth).Select(day => new DailyData { Day = day }).ToList();
            
            if (string.IsNullOrEmpty(connStr)) return buChart;

            string sql = @"
                SELECT DAY([Date]) as Day, 
                       SUM(ISNULL([Target], 0)) as TotalTarget,
                       SUM(ISNULL([Actual], 0)) as TotalActual
                FROM [dbo].[FINAL1]
                WHERE YEAR([Date]) = @SelectedYear 
                  AND MONTH([Date]) = @SelectedMonth
                GROUP BY DAY([Date])";

            try
            {
                using (var conn = new SqlConnection(connStr))
                {
                    conn.Open();
                    using (var cmd = new SqlCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("@SelectedYear", SelectedYear);
                        cmd.Parameters.AddWithValue("@SelectedMonth", SelectedMonth);

                        using (var reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                var d = combinedData.FirstOrDefault(x => x.Day == (int)reader["Day"]);
                                if (d != null)
                                {
                                    d.Plan = Convert.ToInt32(reader["TotalTarget"]);
                                    d.Shift1_Unit = Convert.ToDecimal(reader["TotalActual"]);
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error FetchLsData: " + ex.Message);
            }

            bool isCurrentMonthView = (SelectedYear == DateTime.Now.Year && SelectedMonth == DateTime.Now.Month);
            int today = DateTime.Now.Day;

            foreach (var data in combinedData)
            {
                buChart.Labels.Add(data.Day.ToString());
                
                int effectivePlan = data.Plan;
                if (isCurrentMonthView && data.Day > today + 1)
                {
                    effectivePlan = 0;
                }
                buChart.ChangePlanData.Add(effectivePlan);
                buChart.ActualNormalData.Add(data.Shift1_Unit);
                buChart.ActualOvertimeData.Add(0); // No overtime data in FINAL1
            }

            return buChart;
        }

        private BuChartData FetchAudData(string buName, string connStr)
        {
            var buChart = new BuChartData { BuName = buName };
            var combinedData = Enumerable.Range(1, this.DaysInMonth).Select(day => new DailyData { Day = day }).ToList();
            
            if (string.IsNullOrEmpty(connStr)) return buChart;

            var unionQueries = new List<string>();
            for (int i = 1; i <= 11; i++)
            {
                unionQueries.Add($@"
                    SELECT Tanggal, Target, Actual 
                    FROM (
                        SELECT CAST([DateTime] AS DATE) as Tanggal, 
                               ISNULL([Target], 0) as Target, 
                               ISNULL([Actual], 0) as Actual,
                               ROW_NUMBER() OVER(PARTITION BY CAST([DateTime] AS DATE) ORDER BY [DateTime] DESC) as rn
                        FROM [dbo].[FINAL{i}]
                    ) t{i}
                    WHERE rn = 1");
            }
            string unions = string.Join(" UNION ALL ", unionQueries);

            string sql = $@"
                WITH AllData AS (
                    {unions}
                )
                SELECT DAY([Tanggal]) as Day, 
                       SUM([Target]) as TotalTarget,
                       SUM([Actual]) as TotalActual
                FROM AllData
                WHERE YEAR([Tanggal]) = @SelectedYear 
                  AND MONTH([Tanggal]) = @SelectedMonth
                GROUP BY DAY([Tanggal])";

            try
            {
                using (var conn = new SqlConnection(connStr))
                {
                    conn.Open();
                    using (var cmd = new SqlCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("@SelectedYear", SelectedYear);
                        cmd.Parameters.AddWithValue("@SelectedMonth", SelectedMonth);

                        using (var reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                var d = combinedData.FirstOrDefault(x => x.Day == (int)reader["Day"]);
                                if (d != null)
                                {
                                    d.Plan = Convert.ToInt32(reader["TotalTarget"]);
                                    d.Shift1_Unit = Convert.ToDecimal(reader["TotalActual"]);
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error FetchAudData: " + ex.Message);
            }

            bool isCurrentMonthView = (SelectedYear == DateTime.Now.Year && SelectedMonth == DateTime.Now.Month);
            int today = DateTime.Now.Day;

            foreach (var data in combinedData)
            {
                buChart.Labels.Add(data.Day.ToString());

                int effectivePlan = data.Plan;
                if (isCurrentMonthView && data.Day > today + 1)
                {
                    effectivePlan = 0;
                }
                buChart.ChangePlanData.Add(effectivePlan);
                buChart.ActualNormalData.Add(data.Shift1_Unit);
                buChart.ActualOvertimeData.Add(0); // No overtime data
            }

            return buChart;
        }

        private BuChartData FetchBuData(string buName, string connStr, string machineFilterPlan, string machineFilterActual, string machineFilterSap)
        {
            var buChart = new BuChartData { BuName = buName };
            var combinedData = Enumerable.Range(1, this.DaysInMonth).Select(day => new DailyData { Day = day }).ToList();
            
            if (string.IsNullOrEmpty(connStr)) return buChart;

            bool isCurrentMonthView = (SelectedYear == DateTime.Now.Year && SelectedMonth == DateTime.Now.Month);
            string dateFilter = isCurrentMonthView ? "AND CAST(SDate AS DATE) <= @TodayDate" : "";

            string shiftSelectionSql = "";
            if (!SelectedShifts.Contains("All") && SelectedShifts.Any())
            {
                var shiftConditions = new List<string>();
                foreach (var shift in SelectedShifts)
                {
                    if (shift == "NS" || shift == "ns")
                        shiftConditions.Add("ShiftMode = 'NON-SHIFT' OR ShiftMode = 'OVERTIME'");
                    else
                        shiftConditions.Add($"ShiftMode = 'SHIFT {shift}' OR ShiftMode = 'OVERTIME SHIFT {shift}'");
                }
                shiftSelectionSql = $"AND ({string.Join(" OR ", shiftConditions)})";
            }

            string planShiftFilter = "";
            string selectQuantityColumn = "SUM(ISNULL(pr.Quantity, 0))";
            
            if (!SelectedShifts.Contains("All") && SelectedShifts.Any())
            {
                var conditions = SelectedShifts.Select(s => {
                    string suffix = s == "NS" ? "NS" : s;
                    return $"(pr.Shift LIKE '%{s}%' OR pr.QtyShift{suffix} > 0 OR pr.OvtShift{suffix} > 0)";
                });
                planShiftFilter = $"AND ({string.Join(" OR ", conditions)})";

                var shiftSumTerms = new List<string>();
                foreach (var shift in SelectedShifts)
                {
                    string colName = "";
                    if (shift == "1") colName = "QtyShift1";
                    else if (shift == "2") colName = "QtyShift2";
                    else if (shift == "3") colName = "QtyShift3";
                    else if (shift == "NS") colName = "QtyShiftNS";

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
                    }
                }
                if (shiftSumTerms.Any())
                {
                    selectQuantityColumn = $"SUM({string.Join(" + ", shiftSumTerms)})";
                }
            }

            string planSql = $@"
                SELECT DAY(pp.CurrentDate) as Day, 
                       {selectQuantityColumn} as TotalPlanQuantity
                FROM ProductionPlan pp
                INNER JOIN ProductionRecords pr ON pp.Id = pr.PlanId
                WHERE YEAR(pp.CurrentDate) = @SelectedYear 
                  AND MONTH(pp.CurrentDate) = @SelectedMonth
                  {machineFilterPlan}
                  {planShiftFilter}
                GROUP BY DAY(pp.CurrentDate)";

            string estimasiProduksiSql = buName == "Fan" 
                ? "MAX(TotalUnit) AS Estimasi_Produksi"
                : @"CASE 
                        WHEN MIN(TotalUnit) = MAX(TotalUnit) THEN 0
                        ELSE (MAX(TotalUnit) - MIN(TotalUnit)) 
                    END AS Estimasi_Produksi";

            string actualSql = $@"
                WITH ShiftData AS (
                    SELECT
                        CASE
                            WHEN CAST(SDate AS TIME) < '07:00:00' THEN CAST(DATEADD(DAY, -1, SDate) AS DATE)
                            ELSE CAST(SDate AS DATE)
                        END AS ReportDate,
                        SDate,
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
                                    WHEN CAST(SDate AS TIME) >= '15:45:00' AND CAST(SDate AS TIME) <= '18:00:00' THEN 'OVERTIME SHIFT 1'
                                    WHEN CAST(SDate AS TIME) > '18:00:00' AND CAST(SDate AS TIME) <= '23:15:00' THEN 'OVERTIME SHIFT 3'
                                    WHEN CAST(SDate AS TIME) > '23:15:00' OR CAST(SDate AS TIME) <= '07:00:00' THEN 'SHIFT 3'
                                    ELSE 'OVERTIME'
                                END
                            WHEN ShiftMode = 'SHIFT 2' AND MONTH(CAST(DATEADD(hour, -7, SDate) AS DATE)) IN (7, 8) AND YEAR(CAST(DATEADD(hour, -7, SDate) AS DATE)) = 2026 THEN
                                CASE 
                                    WHEN CAST(SDate AS TIME) >= '07:00:00' AND CAST(SDate AS TIME) <= '15:45:00' THEN 'SHIFT 1'
                                    WHEN CAST(SDate AS TIME) > '15:45:00' AND CAST(SDate AS TIME) <= '18:00:00' THEN 'OVERTIME SHIFT 1'
                                    WHEN CAST(SDate AS TIME) > '18:00:00' AND CAST(SDate AS TIME) <= '23:15:00' THEN 'OVERTIME SHIFT 3'
                                    ELSE 'SHIFT 3'
                                END
                            WHEN ShiftMode = 'SHIFT 3' AND CAST(SDate AS TIME) > '18:00:00' AND CAST(SDate AS TIME) <= '23:15:00' THEN 'OVERTIME SHIFT 3'
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
                    {machineFilterActual}
                ),
                GroupedData AS (
                    SELECT 
                        ReportDate,
                        MachineCode,
                        Mode_Asli_Mesin,
                        Status_Di_Web,
                        {estimasiProduksiSql},
                        MAX(SDate) AS Max_SDate,
                        MAX(NoOfOperator) AS MaxOp
                    FROM ShiftData
                    GROUP BY ReportDate, MachineCode, Mode_Asli_Mesin, Status_Di_Web
                ),
                MachineDaily AS (
                    SELECT 
                        ReportDate,
                        MachineCode,
                        SUM(CASE WHEN Status_Di_Web = 'SHIFT 1' THEN Estimasi_Produksi ELSE 0 END) as S1_Unit,
                        SUM(CASE WHEN Status_Di_Web = 'SHIFT 2' THEN Estimasi_Produksi ELSE 0 END) as S2_Unit,
                        SUM(CASE WHEN Status_Di_Web = 'SHIFT 3' THEN Estimasi_Produksi ELSE 0 END) as S3_Unit,
                        SUM(CASE WHEN Status_Di_Web = 'NON-SHIFT' THEN Estimasi_Produksi ELSE 0 END) as NS_Unit,
                        SUM(CASE WHEN Status_Di_Web LIKE 'OVERTIME%' THEN Estimasi_Produksi ELSE 0 END) as OT_Unit
                    FROM GroupedData
                    WHERE 1=1 {shiftSelectionSql.Replace("ShiftMode", "Status_Di_Web")}
                    GROUP BY ReportDate, MachineCode
                ),
                DailyAggregates AS (
                    SELECT 
                        ReportDate,
                        SUM(ISNULL(S1_Unit, 0)) as S1_Unit,
                        SUM(ISNULL(S2_Unit, 0)) as S2_Unit,
                        SUM(ISNULL(S3_Unit, 0)) as S3_Unit,
                        SUM(ISNULL(NS_Unit, 0)) as NS_Unit,
                        SUM(ISNULL(OT_Unit, 0)) as OT_Unit
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
                       SUM(ISNULL(sp.SapPlanNormal, 0)) as TotalSapNormal
                FROM ProductionPlan pp
                INNER JOIN SapPlan sp ON pp.Id = sp.PlanId
                WHERE YEAR(pp.CurrentDate) = @SelectedYear
                  AND MONTH(pp.CurrentDate) = @SelectedMonth
                  {machineFilterSap}
                  {sapShiftFilter}
                GROUP BY DAY(pp.CurrentDate)";

            using (var conn = new SqlConnection(connStr))
            {
                try { conn.Open(); } 
                catch (Exception ex) { 
                    Console.WriteLine($"Error opening connection for {buName}: " + ex.Message); 
                    return buChart; 
                }

                try
                {
                    using (var planCmd = new SqlCommand(planSql, conn))
                    {
                        planCmd.Parameters.AddWithValue("@SelectedYear", SelectedYear);
                        planCmd.Parameters.AddWithValue("@SelectedMonth", SelectedMonth);

                        using (var reader = planCmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                var d = combinedData.FirstOrDefault(x => x.Day == (int)reader["Day"]);
                                if (d != null)
                                {
                                    d.Plan = Convert.ToInt32(reader["TotalPlanQuantity"]);
                                }
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error FetchBuData Plan for {buName}: " + ex.Message);
                }

                try
                {
                    using (var actualCmd = new SqlCommand(actualSql, conn))
                    {
                        actualCmd.Parameters.AddWithValue("@SelectedYear", SelectedYear);
                        actualCmd.Parameters.AddWithValue("@SelectedMonth", SelectedMonth);
                        if (isCurrentMonthView) actualCmd.Parameters.AddWithValue("@TodayDate", DateTime.Now.Date);

                        using (var reader = actualCmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                var d = combinedData.FirstOrDefault(x => x.Day == (int)reader["Day"]);
                                if (d != null)
                                {
                                    d.Shift1_Unit = reader["S1_Unit"] != DBNull.Value ? Convert.ToDecimal(reader["S1_Unit"]) : 0;
                                    d.Shift2_Unit = reader["S2_Unit"] != DBNull.Value ? Convert.ToDecimal(reader["S2_Unit"]) : 0;
                                    d.Shift3_Unit = reader["S3_Unit"] != DBNull.Value ? Convert.ToDecimal(reader["S3_Unit"]) : 0;
                                    d.NonShift_Unit = reader["NS_Unit"] != DBNull.Value ? Convert.ToDecimal(reader["NS_Unit"]) : 0;
                                    d.Overtime_Unit = reader["OT_Unit"] != DBNull.Value ? Convert.ToDecimal(reader["OT_Unit"]) : 0;
                                }
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error FetchBuData Actual for {buName}: " + ex.Message);
                }

                try
                {
                    using (var sapCmd = new SqlCommand(sapPlanSql, conn))
                    {
                        sapCmd.Parameters.AddWithValue("@SelectedYear", SelectedYear);
                        sapCmd.Parameters.AddWithValue("@SelectedMonth", SelectedMonth);

                        using (var reader = sapCmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                var d = combinedData.FirstOrDefault(x => x.Day == (int)reader["Day"]);
                                if (d != null)
                                {
                                    d.OriginalPlan = Convert.ToInt32(reader["TotalSapNormal"]);
                                }
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error FetchBuData SapPlan for {buName}: " + ex.Message);
                }
            }

            int today = DateTime.Now.Day;
            foreach (var data in combinedData)
            {
                buChart.Labels.Add(data.Day.ToString());
                
                int effectivePlan = data.Plan > 0 ? data.Plan : data.OriginalPlan;
                if (isCurrentMonthView && data.Day > today + 1)
                {
                    effectivePlan = 0;
                }
                buChart.ChangePlanData.Add(effectivePlan);

                decimal normalUnits = 0;
                decimal overtimeUnits = 0;

                bool hasNormalActivity = data.Shift1_Unit > 0 || data.Shift2_Unit > 0 || data.Shift3_Unit > 0 || data.NonShift_Unit > 0;

                if (hasNormalActivity)
                {
                    normalUnits = data.Shift1_Unit + data.Shift2_Unit + data.Shift3_Unit + data.NonShift_Unit;
                    overtimeUnits = data.Overtime_Unit;
                }
                else
                {
                    normalUnits = 0;
                    overtimeUnits = data.Overtime_Unit;
                }

                buChart.ActualNormalData.Add(normalUnits);
                buChart.ActualOvertimeData.Add(overtimeUnits);
            }
            
            return buChart;
        }
    }
}
