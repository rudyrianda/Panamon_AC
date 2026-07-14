using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Data.SqlClient;
using MonitoringSystem.Data;
using MonitoringSystem.Models;
using static MonitoringSystem.Pages.Summary.SummaryModel;

namespace MonitoringSystem.Pages.Performance
{
    public class PerformanceModel : PageModel
    {
       
        //public string connectionString = "Dat public string connectionString = "Server=10.83.33.103;User Id=sa;Password=sa;Database=PROMOSYS;Trusted_Connection=False;TrustServerCertificate=True;Encrypt=False";a Source=DESKTOP-NBPATD6\\MSSQLSERVERR;trusted_connection=true;trustservercertificate=True;Database=PROMOSYS;Integrated Security=True;Encrypt=False";
        public string errorMessage = "";

        private readonly ApplicationDbContext _context;
        private readonly IServiceProvider _serviceProvider;
        public List<PlanQty> plansQty = new List<PlanQty>();

        // ✅ CACHE: Hindari query DB berulang untuk data yang sama
        private readonly Dictionary<string, TimeSpan> _firstTimeCache = new();
        private readonly Dictionary<string, TimeSpan> _lastTimeCache = new();
        private readonly Dictionary<string, int> _modelPlanCache = new();

        // ✅ CACHE: GetRestTime — query RestTime hanya 1x per dayType per request
        private readonly Dictionary<string, List<RestTime>> _restTimeCache = new();

        // ✅ CACHE: GetActualPerHour — hanya query 1x per request
        private List<ActualData>? _cachedActualPerHour = null;

        // ✅ CACHE: GetLastWorkingTime — hanya query 1x per request
        private readonly Dictionary<string, TimeSpan> _lastWorkingTimeCache = new();

        // ✅ FLAG: Pastikan LoadBreakTimes hanya dipanggil sekali per request
        private bool _breakTimesLoaded = false;

        public PerformanceModel(ApplicationDbContext context, IServiceProvider serviceProvider)
        {
            _context = context;
            _serviceProvider = serviceProvider;
        }

        public int TotalPlanForSummaryCU { get; set; }
        public int TotalPlanForSummaryCS { get; set; }

        [BindProperty]
        public DateTime SelectedDate { get; set; } = DateTime.Now.Date;

        [BindProperty]
        public string MachineCode { get; set; } = "MCH1-01";

        // ✅ COMMENTED: Tabel AdditionalBreakTime belum ada di database
        // Uncomment jika tabel sudah dibuat dengan script:
        // CREATE TABLE AdditionalBreakTime (Id INT IDENTITY PRIMARY KEY, Date DATE, BreakTime1Start TIME, BreakTime1End TIME, BreakTime2Start TIME, BreakTime2End TIME, CreatedAt DATETIME DEFAULT GETDATE())
        public TimeSpan? BreakTime1Start { get; set; }
        public TimeSpan? BreakTime1End { get; set; }
        public TimeSpan? BreakTime2Start { get; set; }
        public TimeSpan? BreakTime2End { get; set; }

        // ✅ CACHE PROPERTIES: Hasil query disimpan sekali, dipakai berkali-kali di view
        public int CachedPlan { get; set; }
        public int CachedTarget { get; set; }
        public int CachedActual { get; set; }
        public int CachedPlanTaktTime { get; set; }
        public int CachedEfficiency { get; set; }
        public int CachedWorkingTime { get; set; }
        public int CachedLossTime { get; set; }
        public int CachedDefect { get; set; }

        public List<ProductionAchievement> listProdAchieve = new List<ProductionAchievement>();
        public List<AssemblyTime> assemblyTimes = new List<AssemblyTime>();

        public int Plan { get; set; }
        public int Actual { get; set; }

        // ✅ OPTIMASI: Semua data diload sekali di sini, tidak ada query duplikat
        private void LoadAllData()
        {
            LoadBreakTimesFromDb(); // Aman: sudah di-comment isinya, return langsung
            GetHourlyAchievement();
            GetAssemblyTime();

            CachedPlan = GetProductionPlan();
            CachedActual = GetActualProduction();
            CachedTarget = GetTargetFromOEESN();
            CachedPlanTaktTime = GetPlanTaktTime();
            CachedEfficiency = GetEfficiencyFromOEESN();
            CachedWorkingTime = GetWorkingTimeBySummaryLogic();
            CachedLossTime = GetLossTimeBySummaryLogic();
            CachedDefect = GetTotalDefectBySummaryLogic();

            Plan = CachedPlan;
            Actual = CachedActual;
        }

        public void OnGet()
        {
            SelectedDate = DateTime.Today;
            MachineCode = MachineCode;
            LoadAllData();
        }

        public void OnPost()
        {
            if (string.IsNullOrEmpty(MachineCode))
                MachineCode = "MCH1-01";

            if (SelectedDate == default)
                SelectedDate = DateTime.Today;

            LoadAllData();
        }

        public void GetProductionPlanDaily()
        {
            try
            {
                using (SqlConnection connection = new SqlConnection(connectionString))
                {
                    connection.Open();
                    string getTotalProduction = @"SELECT SUM(Quantity), ProductionRecords.MachineCode FROM ProductionRecords 
                                                  JOIN ProductionPlan ON ProductionRecords.PlanId = ProductionPlan.Id
                                                  WHERE ProductionPlan.CurrentDate = @SelectionDate
                                                  GROUP BY ProductionRecords.MachineCode;";
                    using (SqlCommand command = new SqlCommand(getTotalProduction, connection))
                    {
                        command.Parameters.AddWithValue("@SelectionDate", SelectedDate);
                        using (SqlDataReader dataReader = command.ExecuteReader())
                        {
                            while (dataReader.Read())
                            {
                                PlanQty plan = new PlanQty();
                                plan.Quantity = dataReader.GetInt32(0);
                                plan.MachineCode = dataReader.GetString(1);
                                plansQty.Add(plan);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Exception: " + ex.ToString());
            }
        }

        public int GetTargetFromOEESN()
        {
            int target = 0;
            try
            {
                using (SqlConnection connection = new SqlConnection(connectionString))
                {
                    connection.Open();
                    string query = @"
                    SELECT TOP 1 TargetUnit 
                    FROM OEESN 
                    WHERE MachineCode = @MachineCode 
                    ORDER BY SDate DESC;";

                    using (SqlCommand command = new SqlCommand(query, connection))
                    {
                        command.Parameters.AddWithValue("@MachineCode", MachineCode);
                        var result = command.ExecuteScalar();
                        if (result != null && result != DBNull.Value)
                            target = Convert.ToInt32(result);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error in GetTargetFromOEESN: " + ex.Message);
            }
            return target;
        }

        public int GetPlanForSummary(DateTime selectedDate, string machineCode)
        {
            return _context.HourlyPlanData
                .Where(x => x.SelectedDate == selectedDate && x.MachineCode == machineCode)
                .Sum(x => x.TotalPlan);
        }

        public void SavePlanToDatabase(int plan, string machineCode)
        {
            // ✅ OPTIMASI: Hanya simpan ke DB jika hari ini (tidak perlu update data historis)
            if (SelectedDate.Date != DateTime.Today) return;

            using (var scope = _serviceProvider.CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

                var existingRecord = context.HourlyPlanData
                    .FirstOrDefault(x => x.SelectedDate == DateTime.Today && x.MachineCode == machineCode);

                if (existingRecord != null)
                {
                    // ✅ OPTIMASI: Skip update jika nilai tidak berubah
                    if (existingRecord.TotalPlan == plan) return;

                    existingRecord.TotalPlan = plan;
                    existingRecord.UpdatedAt = DateTime.Now;
                    context.Update(existingRecord);
                }
                else
                {
                    context.HourlyPlanData.Add(new HourlyPlanData
                    {
                        MachineCode = machineCode,
                        SelectedDate = DateTime.Today,
                        TotalPlan = plan,
                        CreatedAt = DateTime.Now,
                        UpdatedAt = DateTime.Now
                    });
                }

                context.SaveChanges();
            }
        }

        private int CalculatePlanPerHourForSummary(string currentModel, string previousModel, TimeSpan startTime, TimeSpan endTime,
                                  TimeSpan currentTime, DateTime currentDate, int sut,
                                  List<Performance.PerformanceModel.RestTime> listRestTime)
        {
            var firstTimeModel = TimeSpan.Zero;
            var lastTimeModel = TimeSpan.Zero;
            var qtyPlan = 1;
            int planPerHour = 1;

            if (currentModel != null)
            {
                firstTimeModel = GetFirstTimeModel(startTime, endTime, currentModel);
                lastTimeModel = GetLastTimeModel(startTime, endTime, currentModel);
                qtyPlan = GetModelPlan(currentModel);
            }

            if (SelectedDate == currentDate)
            {
                if (currentTime >= startTime && currentTime <= endTime)
                {
                    planPerHour = currentModel == previousModel
                        ? CalculatePlan(startTime, currentTime, sut, listRestTime)
                        : CalculatePlan(firstTimeModel, lastTimeModel, sut, listRestTime);
                }
                else
                {
                    planPerHour = currentModel == previousModel
                        ? CalculatePlan(startTime, endTime, sut, listRestTime)
                        : CalculatePlan(firstTimeModel, lastTimeModel, sut, listRestTime);
                }
            }
            else
            {
                planPerHour = currentModel == previousModel
                    ? CalculatePlan(startTime, endTime, sut, listRestTime)
                    : CalculatePlan(firstTimeModel, lastTimeModel, sut, listRestTime);
            }

            planPerHour = Math.Min(planPerHour, qtyPlan);
            return planPerHour > 0 ? planPerHour : 1;
        }

        // ✅ COMMENTED: Tabel AdditionalBreakTime belum ada di database PROMOSYS
        // Untuk mengaktifkan kembali: buat tabel dulu, lalu hapus comment di bawah
        private void LoadBreakTimesFromDb()
        {
            // Guard: hanya load sekali per request
            if (_breakTimesLoaded) return;
            _breakTimesLoaded = true;

            // Set semua null — fitur AdditionalBreakTime belum aktif
            BreakTime1Start = null;
            BreakTime1End = null;
            BreakTime2Start = null;
            BreakTime2End = null;

            // ============================================================
            // UNCOMMENT BLOK INI JIKA TABEL AdditionalBreakTime SUDAH ADA
            // ============================================================
            // try
            // {
            //     using (SqlConnection connection = new SqlConnection(connectionString))
            //     {
            //         connection.Open();
            //         string query = @"SELECT TOP 1 BreakTime1Start, BreakTime1End, BreakTime2Start, BreakTime2End 
            //                      FROM AdditionalBreakTime 
            //                      WHERE Date = @Date
            //                      ORDER BY CreatedAt DESC";
            //         using (SqlCommand command = new SqlCommand(query, connection))
            //         {
            //             command.Parameters.AddWithValue("@Date", SelectedDate.Date);
            //             using (SqlDataReader reader = command.ExecuteReader())
            //             {
            //                 if (reader.Read())
            //                 {
            //                     BreakTime1Start = reader.IsDBNull(0) ? (TimeSpan?)null : reader.GetTimeSpan(0);
            //                     BreakTime1End   = reader.IsDBNull(1) ? (TimeSpan?)null : reader.GetTimeSpan(1);
            //                     BreakTime2Start = reader.IsDBNull(2) ? (TimeSpan?)null : reader.GetTimeSpan(2);
            //                     BreakTime2End   = reader.IsDBNull(3) ? (TimeSpan?)null : reader.GetTimeSpan(3);
            //                 }
            //             }
            //         }
            //     }
            // }
            // catch (Exception ex)
            // {
            //     Console.WriteLine("Error loading break times: " + ex.Message);
            // }
        }

        // ✅ COMMENTED: Selalu return false karena AdditionalBreakTime belum ada
        private bool IsOverlappingWithBreakTime(TimeSpan start, TimeSpan end, int toleranceSeconds = 60)
        {
            // Selalu false sampai tabel AdditionalBreakTime tersedia
            return false;

            // ============================================================
            // UNCOMMENT BLOK INI JIKA TABEL AdditionalBreakTime SUDAH ADA
            // ============================================================
            // TimeSpan tolerance = TimeSpan.FromSeconds(toleranceSeconds);
            // bool Overlaps(TimeSpan bStart, TimeSpan bEnd)
            // {
            //     return start < (bEnd + tolerance) && (end + tolerance) > bStart;
            // }
            // return (BreakTime1Start != null && BreakTime1End != null && Overlaps(BreakTime1Start.Value, BreakTime1End.Value))
            //     || (BreakTime2Start != null && BreakTime2End != null && Overlaps(BreakTime2Start.Value, BreakTime2End.Value));
        }

        [HttpGet]
        public IActionResult OnGetUpdatedData(string machineCode, DateTime selectedDate)
        {
            try
            {
                MachineCode = !string.IsNullOrEmpty(machineCode) ? machineCode : "MCH1-01";
                SelectedDate = selectedDate != default ? selectedDate : DateTime.Today;

                // ✅ OPTIMASI: Load achievement dulu baru hitung chart — tidak query ulang
                LoadBreakTimesFromDb();
                GetHourlyAchievement();

                var actualData = GetActualPerHour();
                var labels = actualData.Select(data => data.EndTime).ToList();
                var efficiencyData = CalculateCumulativeEfficiencyForChart(actualData);

                return new JsonResult(new
                {
                    Labels = labels ?? new List<string>(),
                    Efficiency = efficiencyData ?? new List<double>()
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in OnGetUpdatedData: {ex.Message}");
                return StatusCode(500, new { error = "Failed to fetch data", details = ex.Message });
            }
        }

        public List<double> CalculateCumulativeEfficiencyForChart(List<ActualData> actualData)
        {
            List<double> efficiencyData = new List<double> { 0 };

            var cumulativeActual = 0;
            var cumulativePlan = 0;

            for (int i = 1; i < actualData.Count; i++)
            {
                cumulativeActual += actualData[i].Actual;

                var matchingAchievement = listProdAchieve
                    .Where(achievement => achievement.EndTime.ToString(@"hh\:mm") == actualData[i].EndTime)
                    .ToList();

                foreach (var achievement in matchingAchievement)
                    cumulativePlan += CalculateHourlyPlan(achievement);

                double efficiency = cumulativePlan > 0
                    ? Math.Round((double)cumulativeActual / cumulativePlan * 100, 2)
                    : 0;

                efficiencyData.Add(efficiency);
            }

            return efficiencyData;
        }

        public int GetEfficiencyFromOEESN()
        {
            int efficiency = 0;
            try
            {
                using (SqlConnection connection = new SqlConnection(connectionString))
                {
                    connection.Open();
                    string query = @"
                SELECT TOP 1 Performance
                FROM OEESN
                WHERE MachineCode = @MachineCode
                ORDER BY SDate DESC;";

                    using (SqlCommand command = new SqlCommand(query, connection))
                    {
                        command.Parameters.AddWithValue("@MachineCode", MachineCode);
                        var result = command.ExecuteScalar();
                        if (result != null && result != DBNull.Value)
                            efficiency = Convert.ToInt32(result);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error in GetEfficiencyFromOEESN: " + ex.Message);
            }
            return efficiency;
        }

        private double CalculateRealtimeEfficiency(int cumulativeActual, int workingTime, double lossTime, int planTaktTime)
        {
            double netOperatingTimeSeconds = (workingTime - lossTime) * 60;
            if (netOperatingTimeSeconds <= 0 || cumulativeActual <= 0 || planTaktTime <= 0) return 0;
            return Math.Round(Math.Min((cumulativeActual * planTaktTime) / netOperatingTimeSeconds * 100, 120), 2);
        }

        private int CalculateHourlyPlan(ProductionAchievement achievement)
        {
            var currentTime = DateTime.Now.TimeOfDay;
            var currentDate = DateTime.Now.Date;
            var listRestTime = GetRestTime(DetermineTypeOfDay(DateTime.Today.DayOfWeek));
            var previousModel = "";

            var firstTimeModel = TimeSpan.Zero;
            var lastTimeModel = TimeSpan.Zero;
            var quantityPlan = 1;

            if (achievement.Model != null)
            {
                firstTimeModel = GetFirstTimeModel(achievement.StartTime, achievement.EndTime, achievement.Model);
                lastTimeModel = GetLastTimeModel(achievement.StartTime, achievement.EndTime, achievement.Model);
                quantityPlan = GetModelPlan(achievement.Model);
            }

            int planPerHour = 1;

            if (SelectedDate == currentDate)
            {
                if (currentTime >= achievement.StartTime && currentTime <= achievement.EndTime)
                {
                    planPerHour = achievement.Model == previousModel
                        ? CalculatePlan(achievement.StartTime, currentTime, achievement.SUT, listRestTime)
                        : CalculatePlan(firstTimeModel, lastTimeModel, achievement.SUT, listRestTime);
                }
                else
                {
                    planPerHour = achievement.Model == previousModel
                        ? CalculatePlan(achievement.StartTime, achievement.EndTime, achievement.SUT, listRestTime)
                        : CalculatePlan(firstTimeModel, achievement.EndTime, achievement.SUT, listRestTime);
                }
            }
            else
            {
                planPerHour = achievement.Model == previousModel
                    ? CalculatePlan(achievement.StartTime, achievement.EndTime, achievement.SUT, listRestTime)
                    : CalculatePlan(firstTimeModel, achievement.EndTime, achievement.SUT, listRestTime);
            }

            planPerHour = Math.Min(planPerHour, quantityPlan);
            return planPerHour > 0 ? planPerHour : 1;
        }

        public List<int> CalculateCumulativePlan()
        {
            List<int> cumulativePlan = new List<int> { 0 };
            foreach (var achievement in listProdAchieve)
            {
                int plan = CalculateSinglePlan(achievement.StartTime, achievement.EndTime, achievement.SUT, GetRestTime("REGULAR"));
                cumulativePlan.Add(plan + cumulativePlan.Last());
            }
            return cumulativePlan;
        }

        public int CalculateSinglePlan(TimeSpan startTime, TimeSpan endTime, int SUT, List<RestTime> restTimes)
        {
            TimeSpan effectiveTime = endTime - startTime;
            foreach (var rest in restTimes)
            {
                if (startTime < rest.EndTime && endTime > rest.StartTime)
                {
                    TimeSpan overlapStart = TimeSpan.FromTicks(Math.Max(startTime.Ticks, rest.StartTime.Ticks));
                    TimeSpan overlapEnd = TimeSpan.FromTicks(Math.Min(endTime.Ticks, rest.EndTime.Ticks));
                    effectiveTime -= (overlapEnd - overlapStart);
                }
            }
            return Convert.ToInt32(effectiveTime.TotalSeconds / SUT);
        }

        public (int EffectivePlan, int EffectivePlanOvertime) GetEffectiveDailyPlan()
        {
            int productionRecordsPlan = 0;
            int productionRecordsOt = 0;
            int sapPlanNormal = 0;
            int sapPlanOt = 0;

            try
            {
                using (SqlConnection connection = new SqlConnection(connectionString))
                {
                    connection.Open();

                    string planSql = @"
                        SELECT
                            SUM(ISNULL(pr.Quantity, 0))  AS TotalPlanQuantity,
                            SUM(ISNULL(pr.Overtime, 0))  AS TotalPlanOvertime
                        FROM ProductionPlan pp
                        INNER JOIN ProductionRecords pr ON pp.Id = pr.PlanId
                        WHERE CAST(pp.CurrentDate AS DATE) = @SelectedDate
                          AND pr.MachineCode = @MachineCode;";

                    using (SqlCommand cmd = new SqlCommand(planSql, connection))
                    {
                        cmd.Parameters.AddWithValue("@SelectedDate", SelectedDate.Date);
                        cmd.Parameters.AddWithValue("@MachineCode", MachineCode);
                        using (SqlDataReader reader = cmd.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                productionRecordsPlan = reader.IsDBNull(0) ? 0 : Convert.ToInt32(reader[0]);
                                productionRecordsOt = reader.IsDBNull(1) ? 0 : Convert.ToInt32(reader[1]);
                            }
                        }
                    }

                    string sapSql = @"
                        SELECT
                            SUM(ISNULL(sp.SapPlanNormal,   0)) AS TotalSapNormal,
                            SUM(ISNULL(sp.SapPlanOvertime, 0)) AS TotalSapOvertime
                        FROM ProductionPlan pp
                        INNER JOIN SapPlan sp ON pp.Id = sp.PlanId
                        WHERE CAST(pp.CurrentDate AS DATE) = @SelectedDate
                          AND sp.MachineCode = @MachineCode;";

                    using (SqlCommand cmd = new SqlCommand(sapSql, connection))
                    {
                        cmd.Parameters.AddWithValue("@SelectedDate", SelectedDate.Date);
                        cmd.Parameters.AddWithValue("@MachineCode", MachineCode);
                        using (SqlDataReader reader = cmd.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                sapPlanNormal = reader.IsDBNull(0) ? 0 : Convert.ToInt32(reader[0]);
                                sapPlanOt = reader.IsDBNull(1) ? 0 : Convert.ToInt32(reader[1]);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error in GetEffectiveDailyPlan: " + ex.Message);
            }

            int effectivePlan = productionRecordsPlan > 0 ? productionRecordsPlan : sapPlanNormal;
            int effectivePlanOt = productionRecordsOt > 0 ? productionRecordsOt : sapPlanOt;
            return (effectivePlan, effectivePlanOt);
        }

        public int GetProductionPlan()
        {
            var (effectivePlan, _) = GetEffectiveDailyPlan();
            return effectivePlan;
        }

        public int GetActualProduction()
        {
            int actual = 0;
            try
            {
                using (SqlConnection connection = new SqlConnection(connectionString))
                {
                    connection.Open();
                    string query = @"
                WITH ShiftData AS (
                    SELECT ShiftMode, TotalUnit
                    FROM OEESN
                    WHERE MachineCode = @MachineCode
                      AND (
                            (CAST(SDate AS DATE) = @SelectedDate AND CAST(SDate AS TIME) >= '07:00:00')
                            OR
                            (CAST(SDate AS DATE) = DATEADD(DAY, 1, @SelectedDate) AND CAST(SDate AS TIME) < '07:00:00')
                          )
                )
                SELECT
                    ISNULL(MAX(CASE WHEN ShiftMode = 'SHIFT 1'   THEN TotalUnit END), 0)
                  + ISNULL(MAX(CASE WHEN ShiftMode = 'SHIFT 2'   THEN TotalUnit END), 0)
                  + ISNULL(MAX(CASE WHEN ShiftMode = 'SHIFT 3'   THEN TotalUnit END), 0)
                  + ISNULL(MAX(CASE WHEN ShiftMode = 'NON-SHIFT' THEN TotalUnit END), 0)
                  + ISNULL(MAX(CASE WHEN ShiftMode = 'OVERTIME'  THEN TotalUnit END), 0)
                FROM ShiftData;";

                    using (SqlCommand command = new SqlCommand(query, connection))
                    {
                        command.Parameters.AddWithValue("@MachineCode", MachineCode);
                        command.Parameters.AddWithValue("@SelectedDate", SelectedDate.Date);
                        var result = command.ExecuteScalar();
                        if (result != null && result != DBNull.Value)
                            actual = Convert.ToInt32(result);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error in GetActualProduction: " + ex.Message);
            }
            return actual;
        }

        public int GetPlanTaktTime()
        {
            int planTaktTime = 0;
            try
            {
                using (SqlConnection connection = new SqlConnection(connectionString))
                {
                    connection.Open();
                    string getPlanTaktTime = @"SELECT TOP 1 MasterData.SUT FROM OEESN
                                                  JOIN MasterData ON OEESN.Product_Id = MasterData.Product_Id
                                               WHERE CAST(OEESN.SDate AS DATE) = @SelectedDate AND OEESN.MachineCode = @MachineCode
                                               ORDER BY SDate DESC;";
                    using (SqlCommand command = new SqlCommand(getPlanTaktTime, connection))
                    {
                        command.Parameters.AddWithValue("@SelectedDate", SelectedDate);
                        command.Parameters.AddWithValue("@MachineCode", MachineCode);
                        var Result = command.ExecuteScalar();
                        planTaktTime = Result != null ? (int)Result : 0;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Exception: " + ex.ToString());
            }
            return planTaktTime;
        }

        public void GetHourlyAchievement()
        {
            listProdAchieve.Clear();

            try
            {
                using (SqlConnection connection = new SqlConnection(connectionString))
                {
                    connection.Open();
                    string query = @"
                    SELECT MIN(OEESN.SDate) As FirstTime,
                           CAST(DATEADD(HOUR, DATEDIFF(HOUR, 0, OEESN.SDate), 0) AS TIME) AS StartTime,
                           CAST(DATEADD(HOUR, DATEDIFF(HOUR, 0, OEESN.SDate) + 1, 0) AS TIME) As EndTime,
                           Masterdata.ProductName As Model, 
                           MasterData.QtyHour As Target,
                           MasterData.SUT AS SUT,
                           COUNT(*) AS Actual
                    FROM OEESN
                    JOIN Masterdata ON OEESN.Product_Id = MasterData.Product_Id
                    WHERE CAST(SDate As DATE) = @Date AND OEESN.MachineCode = @MachineCode
                    GROUP BY DATEDIFF(HOUR, 0, SDate), Masterdata.ProductName, MasterData.QtyHour, MasterData.SUT
                    ORDER BY MIN(OEESN.SDate);";

                    using (SqlCommand command = new SqlCommand(query, connection))
                    {
                        command.Parameters.AddWithValue("@Date", SelectedDate);
                        command.Parameters.AddWithValue("@MachineCode", MachineCode);

                        using (SqlDataReader reader = command.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                var startTime = reader.GetTimeSpan(1);
                                var endTime = reader.GetTimeSpan(2);

                                // ✅ IsOverlappingWithBreakTime selalu return false — tidak ada filtering
                                if (IsOverlappingWithBreakTime(startTime, endTime))
                                    continue;

                                listProdAchieve.Add(new ProductionAchievement
                                {
                                    FirstTime = reader.GetDateTime(0),
                                    StartTime = startTime,
                                    EndTime = endTime,
                                    Time = $"{startTime:hh\\:mm} - {endTime:hh\\:mm}",
                                    Model = reader.GetString(3),
                                    Plan = reader.GetInt32(4),
                                    SUT = reader.GetInt32(5),
                                    Actual = reader.GetInt32(6)
                                });
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error in GetHourlyAchievement: " + ex.Message);
            }
        }

        // ✅ CACHE: GetFirstTimeModel — cek cache dulu sebelum query DB
        public TimeSpan GetFirstTimeModel(TimeSpan StartTime, TimeSpan EndTime, string model)
        {
            var cacheKey = $"first_{model}_{StartTime}_{EndTime}_{SelectedDate:yyyyMMdd}";
            if (_firstTimeCache.TryGetValue(cacheKey, out var cached)) return cached;

            TimeSpan FirstTime = TimeSpan.Zero;
            try
            {
                using (SqlConnection connection = new SqlConnection(connectionString))
                {
                    connection.Open();
                    string getFirstTime = @"SELECT CAST(MIN(OEESN.SDate) AS Time) FROM OEESN 
                                            JOIN MasterData ON OEESN.Product_Id = MasterData.Product_Id 
                                            WHERE MasterData.ProductName = @Model AND CAST(OEESN.SDate AS Time) >= @StartTime 
                                            AND CAST(OEESN.SDate AS Time) <= @EndTime AND CAST(OEESN.SDate AS DATE) = @CurrentDate";
                    using (SqlCommand command = new SqlCommand(getFirstTime, connection))
                    {
                        command.Parameters.AddWithValue("@Model", model);
                        command.Parameters.AddWithValue("@StartTime", StartTime);
                        command.Parameters.AddWithValue("@EndTime", EndTime);
                        command.Parameters.AddWithValue("@CurrentDate", SelectedDate);
                        var Result = command.ExecuteScalar();
                        FirstTime = Result != null ? (TimeSpan)Result : TimeSpan.Zero;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Exception: " + ex.ToString());
            }

            _firstTimeCache[cacheKey] = FirstTime;
            return FirstTime;
        }

        // ✅ CACHE: GetLastTimeModel — cek cache dulu sebelum query DB
        public TimeSpan GetLastTimeModel(TimeSpan StartTime, TimeSpan EndTime, string model)
        {
            var cacheKey = $"last_{model}_{StartTime}_{EndTime}_{SelectedDate:yyyyMMdd}";
            if (_lastTimeCache.TryGetValue(cacheKey, out var cached)) return cached;

            TimeSpan LastTime = TimeSpan.Zero;
            try
            {
                using (SqlConnection connection = new SqlConnection(connectionString))
                {
                    connection.Open();
                    string getLastTime = @"SELECT CAST(MAX(OEESN.SDate) AS Time) FROM OEESN 
                                            JOIN MasterData ON OEESN.Product_Id = MasterData.Product_Id 
                                            WHERE MasterData.ProductName = @Model AND CAST(OEESN.SDate AS Time) >= @StartTime 
                                            AND CAST(OEESN.SDate AS Time) <= @EndTime AND CAST(OEESN.SDate AS DATE) = @CurrentDate";
                    using (SqlCommand command = new SqlCommand(getLastTime, connection))
                    {
                        command.Parameters.AddWithValue("@Model", model);
                        command.Parameters.AddWithValue("@StartTime", StartTime);
                        command.Parameters.AddWithValue("@EndTime", EndTime);
                        command.Parameters.AddWithValue("@CurrentDate", SelectedDate);
                        var Result = command.ExecuteScalar();
                        LastTime = Result != null ? (TimeSpan)Result : TimeSpan.Zero;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Exception: " + ex.ToString());
            }

            _lastTimeCache[cacheKey] = LastTime;
            return LastTime;
        }

        public int CalculatePlan(TimeSpan startTime, TimeSpan endTime, int SUT, List<RestTime> restTime)
        {
            TimeSpan effectiveTime = endTime - startTime;
            foreach (var rest in restTime)
            {
                if (startTime < rest.EndTime && endTime > rest.StartTime)
                {
                    TimeSpan overlapStart = TimeSpan.FromTicks(Math.Max(startTime.Ticks, rest.StartTime.Ticks));
                    TimeSpan overlapEnd = TimeSpan.FromTicks(Math.Min(endTime.Ticks, rest.EndTime.Ticks));
                    effectiveTime -= (overlapEnd - overlapStart);
                }
            }
            return Convert.ToInt32(effectiveTime.TotalSeconds / SUT);
        }

        public int CalculateTotalPlanPerSUT(int DailyPlan, List<ProductionAchievement> ProdAchievement, List<RestTime> listRestTime)
        {
            int TotalPlanPerSUT = 0;
            int PlanPerSUT = 0;
            var PreviousModel = "";
            var CurrentDate = DateTime.Now.Date;
            var CurrentWorkingTime = DateTime.Now.TimeOfDay;

            foreach (var item in ProdAchievement)
            {
                var CurrentModel = item.Model;
                var SUT = item.SUT;
                var StartTime = item.StartTime;
                var EndTime = item.EndTime;
                var FirstTime_Model = TimeSpan.Zero;
                var LastTime_Model = TimeSpan.Zero;
                var QuantityPlan = 0;

                if (CurrentModel != null)
                {
                    FirstTime_Model = GetFirstTimeModel(StartTime, EndTime, CurrentModel);
                    LastTime_Model = GetLastTimeModel(StartTime, EndTime, CurrentModel);
                    QuantityPlan = GetModelPlan(CurrentModel);
                }

                if (SelectedDate == CurrentDate)
                {
                    if (CurrentWorkingTime >= StartTime && CurrentWorkingTime <= EndTime)
                        PlanPerSUT = CurrentModel == PreviousModel
                            ? CalculatePlan(StartTime, CurrentWorkingTime, SUT, listRestTime)
                            : CalculatePlan(FirstTime_Model, EndTime, SUT, listRestTime);
                    else
                        PlanPerSUT = CurrentModel == PreviousModel
                            ? CalculatePlan(StartTime, EndTime, SUT, listRestTime)
                            : CalculatePlan(FirstTime_Model, EndTime, SUT, listRestTime);
                }
                else
                {
                    PlanPerSUT = CurrentModel == PreviousModel
                        ? CalculatePlan(StartTime, EndTime, SUT, listRestTime)
                        : CalculatePlan(FirstTime_Model, EndTime, SUT, listRestTime);
                }

                PlanPerSUT = Math.Min(PlanPerSUT, QuantityPlan);
                PreviousModel = CurrentModel;
                TotalPlanPerSUT = Math.Min(TotalPlanPerSUT + PlanPerSUT, DailyPlan);
            }

            return TotalPlanPerSUT;
        }

        // ✅ CACHE: GetModelPlan — cek cache dulu sebelum query DB
        public int GetModelPlan(string model)
        {
            var cacheKey = $"modelplan_{model}_{MachineCode}_{SelectedDate:yyyyMMdd}";
            if (_modelPlanCache.TryGetValue(cacheKey, out var cached)) return cached;

            int totalQuantityPlan = 0;
            try
            {
                using (SqlConnection connection = new SqlConnection(connectionString))
                {
                    connection.Open();
                    string query = @"
                SELECT SUM(pr.Quantity)
                FROM ProductionRecords pr
                JOIN ProductionPlan pp ON pr.PlanId = pp.Id
                WHERE CAST(pp.CurrentDate AS DATE) = @SelectedDate
                  AND pr.MachineCode = @MachineCode;";

                    using (SqlCommand cmd = new SqlCommand(query, connection))
                    {
                        cmd.Parameters.AddWithValue("@SelectedDate", SelectedDate.Date);
                        cmd.Parameters.AddWithValue("@MachineCode", MachineCode);
                        var result = cmd.ExecuteScalar();
                        totalQuantityPlan = (result != null && result != DBNull.Value)
                            ? Convert.ToInt32(result) : 0;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error in GetModelPlan: " + ex.Message);
            }

            _modelPlanCache[cacheKey] = totalQuantityPlan;
            return totalQuantityPlan;
        }

        public int GetTotalDefectBySummaryLogic()
        {
            int totalDefect = 0;
            try
            {
                using (SqlConnection connection = new SqlConnection(connectionString))
                {
                    connection.Open();
                    string query = @"
                SELECT COUNT(*) 
                FROM NG_RPTS 
                WHERE CAST(SDate AS DATE) = @SelectedDate AND MachineCode = @MachineCode;";
                    using (SqlCommand command = new SqlCommand(query, connection))
                    {
                        command.Parameters.AddWithValue("@SelectedDate", SelectedDate);
                        command.Parameters.AddWithValue("@MachineCode", MachineCode);
                        var result = command.ExecuteScalar();
                        if (result != null && result != DBNull.Value)
                            totalDefect = Convert.ToInt32(result);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error in GetTotalDefectBySummaryLogic: " + ex.Message);
            }
            return totalDefect;
        }

        public int GetWorkingTimeBySummaryLogic()
        {
            int totalMinutes = 0;
            TimeSpan shiftStart = new TimeSpan(7, 05, 0);
            TimeSpan shiftEnd = new TimeSpan(23, 15, 0);

            try
            {
                using (SqlConnection connection = new SqlConnection(connectionString))
                {
                    connection.Open();

                    var listRestTime = new List<(TimeSpan StartTime, TimeSpan EndTime)>();
                    string restQuery = "SELECT StartTime, EndTime FROM RestTime WHERE DayType = @DayType;";
                    using (SqlCommand cmd = new SqlCommand(restQuery, connection))
                    {
                        cmd.Parameters.AddWithValue("@DayType", DetermineTypeOfDay(SelectedDate.DayOfWeek));
                        using (SqlDataReader reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                                listRestTime.Add((reader.GetTimeSpan(0), reader.GetTimeSpan(1)));
                        }
                    }

                    double totalShiftMinutes = (shiftEnd - shiftStart).TotalMinutes;
                    double totalRestMinutes = 0;
                    foreach (var rest in listRestTime)
                    {
                        var overlapStart = new TimeSpan(Math.Max(shiftStart.Ticks, rest.StartTime.Ticks));
                        var overlapEnd = new TimeSpan(Math.Min(shiftEnd.Ticks, rest.EndTime.Ticks));
                        if (overlapEnd > overlapStart)
                            totalRestMinutes += (overlapEnd - overlapStart).TotalMinutes;
                    }

                    if (SelectedDate.Date == DateTime.Today)
                    {
                        TimeSpan now = DateTime.Now.TimeOfDay;
                        TimeSpan effectiveEnd = now > shiftEnd ? shiftEnd : now;
                        if (effectiveEnd > shiftStart)
                        {
                            double elapsed = (effectiveEnd - shiftStart).TotalMinutes;
                            double restElapsed = listRestTime
                                .Where(r => r.StartTime < effectiveEnd)
                                .Sum(r =>
                                {
                                    var oStart = new TimeSpan(Math.Max(shiftStart.Ticks, r.StartTime.Ticks));
                                    var oEnd = new TimeSpan(Math.Min(effectiveEnd.Ticks, r.EndTime.Ticks));
                                    return oEnd > oStart ? (oEnd - oStart).TotalMinutes : 0;
                                });
                            totalMinutes = (int)Math.Max(0, elapsed - restElapsed);
                        }
                    }
                    else
                    {
                        totalMinutes = (int)(totalShiftMinutes - totalRestMinutes);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error in GetWorkingTimeBySummaryLogic: " + ex.Message);
            }

            return totalMinutes;
        }

        public int GetLossTimeBySummaryLogic()
        {
            int totalLossMinutes = 0;

            try
            {
                using (SqlConnection connection = new SqlConnection(connectionString))
                {
                    connection.Open();

                    // Cek dulu apakah ada data di LossTimeActuals untuk hari ini
                    string checkSql = @"SELECT COUNT(1) FROM LossTimeActuals 
                                WHERE Month = @Month AND Year = @Year AND MachineLine = @MachineCode";
                    bool hasActuals = false;
                    using (SqlCommand cmd = new SqlCommand(checkSql, connection))
                    {
                        cmd.Parameters.AddWithValue("@Month", SelectedDate.Month);
                        cmd.Parameters.AddWithValue("@Year", SelectedDate.Year);
                        cmd.Parameters.AddWithValue("@MachineCode", MachineCode);
                        hasActuals = (int)cmd.ExecuteScalar() > 0;
                    }

                    if (hasActuals)
                    {
                        // Pakai LossTimeActuals
                        string actualsSql = @"SELECT SUM(Minutes) FROM LossTimeActuals
                                      WHERE Month = @Month AND Year = @Year 
                                      AND Day = @Day AND MachineLine = @MachineCode AND Minutes > 0";
                        using (SqlCommand cmd = new SqlCommand(actualsSql, connection))
                        {
                            cmd.Parameters.AddWithValue("@Month", SelectedDate.Month);
                            cmd.Parameters.AddWithValue("@Year", SelectedDate.Year);
                            cmd.Parameters.AddWithValue("@Day", SelectedDate.Day);
                            cmd.Parameters.AddWithValue("@MachineCode", MachineCode);
                            var result = cmd.ExecuteScalar();
                            if (result != null && result != DBNull.Value)
                                totalLossMinutes = (int)Convert.ToDouble(result);
                        }
                        return totalLossMinutes;
                    }

                    // Fallback: pakai AssemblyLossTime (kode lama)
                    TimeSpan shiftStart = new TimeSpan(7, 0, 0);
                    TimeSpan shiftEnd = new TimeSpan(23, 15, 0);

                    string lossQuery = @"SELECT Time, EndDateTime 
                                 FROM AssemblyLossTime
                                 WHERE CAST(Date AS DATE) = @SelectedDate AND MachineCode = @MachineCode;";

                    var lossList = new List<(TimeSpan Start, TimeSpan End)>();
                    using (SqlCommand cmd = new SqlCommand(lossQuery, connection))
                    {
                        cmd.Parameters.AddWithValue("@SelectedDate", SelectedDate);
                        cmd.Parameters.AddWithValue("@MachineCode", MachineCode);
                        using (SqlDataReader reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                var start = reader.GetTimeSpan(0);
                                var end = reader.GetTimeSpan(1);
                                if (end < start) end = end.Add(TimeSpan.FromDays(1));
                                lossList.Add((start, end));
                            }
                        }
                    }

                    var breaks = new List<(TimeSpan Start, TimeSpan End)>();
                    string restQuery = "SELECT StartTime, EndTime FROM RestTime WHERE DayType = @DayType;";
                    using (SqlCommand cmd = new SqlCommand(restQuery, connection))
                    {
                        cmd.Parameters.AddWithValue("@DayType", DetermineTypeOfDay(SelectedDate.DayOfWeek));
                        using (SqlDataReader reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                                breaks.Add((reader.GetTimeSpan(0), reader.GetTimeSpan(1)));
                        }
                    }

                    var validLoss = lossList
                        .Where(l => l.Start >= shiftStart && l.End <= shiftEnd &&
                                    !breaks.Any(b => l.Start < b.End && l.End > b.Start))
                        .ToList();

                    totalLossMinutes = (int)validLoss.Sum(l => (l.End - l.Start).TotalMinutes);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error in GetLossTimeBySummaryLogic: " + ex.Message);
            }

            return totalLossMinutes;
        }

        public int GetCurrentSUT()
        {
            int SUT = 0;
            try
            {
                using (SqlConnection connection = new SqlConnection(connectionString))
                {
                    connection.Open();
                    string getTotalDefect = @"SELECT TOP 1 MasterData.SUT FROM OEESN 
                                              JOIN MasterData ON OEESN.Product_Id = MasterData.Product_Id 
                                              WHERE CAST(OEESN.SDate AS DATE) = @SelectedDate AND OEESN.MachineCode = @MachineCode
                                              ORDER BY SDate DESC;";
                    using (SqlCommand command = new SqlCommand(getTotalDefect, connection))
                    {
                        command.Parameters.AddWithValue("@SelectedDate", SelectedDate);
                        command.Parameters.AddWithValue("@MachineCode", MachineCode);
                        var Result = command.ExecuteScalar();
                        SUT = Result != null ? (int)Result : 0;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Exception: " + ex.ToString());
            }
            return SUT;
        }

        public List<RestTime> GetRestTime(string dayTipe)
        {
            // ✅ CACHE: Cek cache dulu — hindari query DB berulang per request
            if (_restTimeCache.TryGetValue(dayTipe, out var cachedList)) return cachedList;

            List<RestTime> listRestTime = new List<RestTime>();
            try
            {
                using (SqlConnection connection = new SqlConnection(connectionString))
                {
                    connection.Open();
                    string getRestTime = @"SELECT Duration, StartTime, EndTime FROM RestTime WHERE DayType = @DayTipe";
                    using (SqlCommand command = new SqlCommand(getRestTime, connection))
                    {
                        command.Parameters.AddWithValue("@DayTipe", dayTipe);
                        using (SqlDataReader dataReader = command.ExecuteReader())
                        {
                            while (dataReader.Read())
                            {
                                if (!dataReader.IsDBNull(0))
                                {
                                    listRestTime.Add(new RestTime
                                    {
                                        Duration = dataReader.GetInt32(0),
                                        StartTime = dataReader.GetTimeSpan(1),
                                        EndTime = dataReader.GetTimeSpan(2)
                                    });
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Exception: " + ex.ToString());
            }

            _restTimeCache[dayTipe] = listRestTime;
            return listRestTime;
        }

        public string DetermineTypeOfDay(DayOfWeek day)
        {
            return day switch
            {
                DayOfWeek.Monday or DayOfWeek.Tuesday or DayOfWeek.Wednesday or DayOfWeek.Thursday => "REGULAR",
                DayOfWeek.Friday => "FRIDAY",
                DayOfWeek.Saturday or DayOfWeek.Sunday => "WEEKEND",
                _ => throw new NotImplementedException()
            };
        }

        public void GetAssemblyTime()
        {
            try
            {
                using (SqlConnection connection = new SqlConnection(connectionString))
                {
                    connection.Open();
                    string getAssemblyProductionTime = @"SELECT MasterData.ProductName As Model, OEESN.MachineCode As MachineCode, 
                                                                MasterData.SUT As SUT, CAST(OEESN.SDate AS Time) As ProductionTime
                                                        FROM OEESN JOIN Masterdata ON OEESN.Product_Id = MasterData.Product_Id
                                                        WHERE CAST(OEESN.SDate AS DATE) = @Date AND OEESN.MachineCode = @MachineCode
                                                        ORDER BY CAST(OEESN.SDate AS TIME) ASC";
                    using (SqlCommand command = new SqlCommand(getAssemblyProductionTime, connection))
                    {
                        command.Parameters.AddWithValue("@Date", SelectedDate);
                        command.Parameters.AddWithValue("@MachineCode", MachineCode);
                        using (SqlDataReader reader = command.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                assemblyTimes.Add(new AssemblyTime
                                {
                                    Model = reader.GetString(0),
                                    MachineCode = reader.GetString(1),
                                    SUT = reader.GetInt32(2),
                                    ProductionTime = reader.GetTimeSpan(3)
                                });
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
            }
        }

        public int CalculateTotalLossTime(List<AssemblyTime> assemblyTimes, List<RestTime> restTimes)
        {
            int totalLossTime = 0;
            var sortedAssemblyTimes = assemblyTimes.OrderBy(p => p.ProductionTime).ToList();

            for (int i = 0; i < sortedAssemblyTimes.Count; i++)
            {
                var current = sortedAssemblyTimes[i];
                var expectedEndTime = current.ProductionTime.Add(TimeSpan.FromSeconds(current.SUT * 3));
                var actualEndTime = i < sortedAssemblyTimes.Count - 1 ? sortedAssemblyTimes[i + 1].ProductionTime : expectedEndTime;

                foreach (var rest in restTimes)
                {
                    if (current.ProductionTime < rest.EndTime && actualEndTime > rest.StartTime)
                    {
                        if (current.ProductionTime >= rest.StartTime && current.ProductionTime < rest.EndTime)
                        {
                            current.ProductionTime = rest.EndTime;
                            expectedEndTime = current.ProductionTime.Add(TimeSpan.FromSeconds(current.SUT * 3));
                        }
                        if (actualEndTime > rest.EndTime && current.ProductionTime <= rest.StartTime)
                            actualEndTime = rest.StartTime;
                        if (actualEndTime > rest.StartTime && actualEndTime <= rest.EndTime)
                            actualEndTime = rest.StartTime;
                    }
                }

                if (expectedEndTime < actualEndTime)
                    totalLossTime += Math.Max(0, (int)(actualEndTime - current.ProductionTime).TotalSeconds);
            }
            return totalLossTime;
        }

        public int GetManPower()
        {
            int NoOfOperator = 0;
            try
            {
                using (SqlConnection connection = new SqlConnection(connectionString))
                {
                    connection.Open();
                    string getManPower = @"SELECT DISTINCT TOP 1 NoOfOperator FROM OEESN WHERE CAST(SDate AS DATE) = @SelectedDate AND MachineCode = @MachineCode;";
                    using (SqlCommand command = new SqlCommand(getManPower, connection))
                    {
                        command.Parameters.AddWithValue("@SelectedDate", SelectedDate);
                        command.Parameters.AddWithValue("@MachineCode", MachineCode);
                        var Result = command.ExecuteScalar();
                        NoOfOperator = Result != null ? (int)Result : 0;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
            return NoOfOperator;
        }

        public TimeSpan GetLastWorkingTime(string MachineCode)
        {
            // ✅ CACHE: Cek cache dulu
            if (_lastWorkingTimeCache.TryGetValue(MachineCode, out var cachedTime)) return cachedTime;

            TimeSpan lastWorkingTime = TimeSpan.Zero;
            try
            {
                using (SqlConnection connection = new SqlConnection(connectionString))
                {
                    connection.Open();
                    string getLastWorkingTime = @"SELECT MAX(CAST(SDate AS Time)) FROM OEESN 
                                                  WHERE CAST(SDate AS Date) = @SelectedDate AND MachineCode = @MachineCode";
                    using (SqlCommand command = new SqlCommand(getLastWorkingTime, connection))
                    {
                        command.Parameters.AddWithValue("@SelectedDate", SelectedDate);
                        command.Parameters.AddWithValue("@MachineCode", MachineCode);
                        var Result = command.ExecuteScalar();
                        lastWorkingTime = Result != null ? (TimeSpan)Result : TimeSpan.Zero;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }

            _lastWorkingTimeCache[MachineCode] = lastWorkingTime;
            return lastWorkingTime;
        }

        public List<ActualData> GetActualPerHour()
        {
            // ✅ CACHE: Cek cache dulu — method ini dipanggil 2x (view + OnGetUpdatedData)
            if (_cachedActualPerHour != null) return _cachedActualPerHour;

            List<ActualData> actualData = new List<ActualData>();
            actualData.Add(new ActualData { StartTime = "07:00", EndTime = "07:00", Actual = 0 });

            try
            {
                var dbActuals = new Dictionary<string, int>();
                using (SqlConnection connection = new SqlConnection(connectionString))
                {
                    connection.Open();
                    string query = @"
                SELECT 
                    CAST(DATEADD(HOUR, DATEDIFF(HOUR, 0, OEESN.SDate) + 1, 0) AS TIME) As EndTime,
                    COUNT(*) AS Actual
                FROM OEESN
                WHERE CAST(SDate As DATE) = @Date AND OEESN.MachineCode = @MachineCode
                GROUP BY DATEDIFF(HOUR, 0, SDate), DATEADD(HOUR, DATEDIFF(HOUR, 0, SDate) + 1, 0)";

                    using (SqlCommand command = new SqlCommand(query, connection))
                    {
                        command.Parameters.AddWithValue("@Date", SelectedDate);
                        command.Parameters.AddWithValue("@MachineCode", MachineCode);
                        using (SqlDataReader reader = command.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                string timeKey = reader.GetTimeSpan(0).ToString(@"hh\:mm");
                                dbActuals[timeKey] = reader.GetInt32(1);
                            }
                        }
                    }
                }

                DateTime now = DateTime.Now;
                int startHour = 7;
                int endHour = SelectedDate.Date == now.Date ? now.Hour + 1 : 24;

                for (int h = startHour + 1; h <= endHour; h++)
                {
                    if (h > 23 && SelectedDate.Date != now.Date) break;
                    int displayHour = h % 24;
                    string label = $"{displayHour:D2}:00";

                    actualData.Add(new ActualData
                    {
                        StartTime = $"{(h - 1) % 24:D2}:00",
                        EndTime = label,
                        Actual = dbActuals.ContainsKey(label) ? dbActuals[label] : 0
                    });
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error in GetActualPerHour: " + ex.Message);
            }

            _cachedActualPerHour = actualData;
            return actualData;
        }

        public class ProductionAchievement
        {
            public string MachineCode { get; set; }
            public DateTime FirstTime { get; set; }
            public TimeSpan StartTime { get; set; }
            public TimeSpan EndTime { get; set; }
            public string? Time { get; set; }
            public string? Model { get; set; }
            public int Plan { get; set; }
            public int SUT { get; set; }
            public int Actual { get; set; }
        }

        public class RestTime
        {
            public int Duration { get; set; }
            public TimeSpan StartTime { get; set; }
            public TimeSpan EndTime { get; set; }
        }

        public class AssemblyTime
        {
            public string? Model { get; set; }
            public string? MachineCode { get; set; }
            public int SUT { get; set; }
            public TimeSpan ProductionTime { get; set; }
        }

        public class ActualData
        {
            public string StartTime { get; set; }
            public string EndTime { get; set; }
            public int Actual { get; set; }
        }
    }
}

public class PlanQty
{
    public int Quantity { get; set; }
    public string? MachineCode { get; set; }
}