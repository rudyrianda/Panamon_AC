using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Data.SqlClient;
using OfficeOpenXml;

namespace MonitoringSystem.Pages.Summary
{
    public class MachineEfficiencyModel : PageModel
    {
        private readonly IConfiguration _config;
        private readonly IWebHostEnvironment _env;

        private static readonly List<(string Col, string Group)> LossColumns = new()
        {
            ("QualityTrouble",           "WorkingLoss"),
            ("ModelChangingLoss",        "WorkingLoss"),
            ("MaterialShortageExternal", "WorkingLoss"),
            ("MachineToolsTrouble",      "WorkingLoss"),
            ("ManPowerAdjustment",       "WorkingLoss"),
            ("MaterialShortageInhouse",  "WorkingLoss"),
            ("MaterialShortageInternal", "WorkingLoss"),
            ("SetRepairingLoss",         "WorkingLoss"),
            ("GawseExternalBodies",      "WorkingLoss"),
            ("Rework",                   "WorkingLoss"),
            ("MoldChangingLoss",         "WorkingLoss"),
            ("BreakTime",                "FixedLoss"),
            ("CompanyActivity",          "FixedLoss"),
            ("MorningAssembly",          "FixedLoss"),
            ("Cleaning",                 "FixedLoss"),
            ("StockOpname",              "FixedLoss"),
            ("GeneralAssembly",          "FixedLoss"),
            ("Maintenance",              "FixedLoss"),
            ("TrialRun",                 "FixedLoss"),
            ("TrainingEducation",        "FixedLoss"),
            ("FreeTalkingQC",            "FixedLoss"),
            ("NoProductionDay",          "FixedLoss"),
        };

        public MachineEfficiencyModel(IConfiguration config, IWebHostEnvironment env)
        {
            _config = config;
            _env = env;
        }

        public void OnGet() { }

        // ── Normalize Shift ────────────────────────────────
        private static string NormalizeShift(string? shift) => (shift ?? "") switch
        {
            "1" or "Shift 1" => "Shift 1",
            "2" or "Shift 2" => "Shift 2",
            "3" or "Shift 3" => "Shift 3",
            "NS" or "Non Shift" => "Non Shift",
            var s => s
        };

        // ── Hitung OEE & semua metric ──────────────────────
        private static (double? quality, double? operatingRatio, double? ability,
                        double? oee, double? achievement, string? category)
            CalcMetrics(MachineEfficiencyInput input)
        {
            double? quality = null;
            if (input.GoodProductionQty.HasValue && input.GoodProductionQty.Value > 0)
            {
                double defect = input.DefectQty ?? 0;
                quality = Math.Round(
                    ((input.GoodProductionQty.Value - defect) / input.GoodProductionQty.Value) * 100.0, 2);
            }

            double? operatingRatio = null;
            if (input.WorkingTime.HasValue && input.WorkingTime.Value > 0)
            {
                double totalLoss = input.LossItems?.Sum(x => x.LossMinutes ?? 0) ?? 0;
                operatingRatio = Math.Round(
                    ((input.WorkingTime.Value - totalLoss) / input.WorkingTime.Value) * 100.0, 2);
            }

            double? ability = null;
            if (input.PlanQty.HasValue && input.PlanQty.Value > 0 && input.GoodProductionQty.HasValue)
                ability = Math.Round(
                    (input.GoodProductionQty.Value / input.PlanQty.Value) * 100.0, 2);

            double? oee = null;
            if (operatingRatio.HasValue && ability.HasValue && quality.HasValue)
                oee = Math.Round(
                    (operatingRatio.Value * ability.Value * quality.Value) / 10000.0, 2);

            double? achievement = ability;

            string? category = oee.HasValue
                ? (oee.Value >= 85 ? "Good" : oee.Value >= 60 ? "Average" : "Poor")
                : null;

            return (quality, operatingRatio, ability, oee, achievement, category);
        }

        // ── GET: MachineList ───────────────────────────────
        public JsonResult OnGetMachineList()
        {
            var result = new List<object>();
            try
            {
                using var conn = OpenConn();
                using var cmd = new SqlCommand(
                    "SELECT IdMachine, MachineName FROM [dbo].[MachineList] ORDER BY MachineName", conn);
                using var r = cmd.ExecuteReader();
                while (r.Read())
                    result.Add(new { machineName = r["MachineName"]?.ToString() ?? "" });
            }
            catch (Exception ex) { return new JsonResult(new { error = ex.Message }); }
            return new JsonResult(result);
        }

        // ── GET: LossCategories ────────────────────────────
        public JsonResult OnGetLossCategories()
        {
            static string ToLabel(string col) =>
                System.Text.RegularExpressions.Regex.Replace(
                    col, @"(?<=[a-z])(?=[A-Z])|(?<=[A-Z])(?=[A-Z][a-z])", " ");

            var workingLoss = LossColumns
                .Where(x => x.Group == "WorkingLoss")
                .Select(x => new { value = x.Col, label = ToLabel(x.Col) });

            var fixedLoss = LossColumns
                .Where(x => x.Group == "FixedLoss")
                .Select(x => new { value = x.Col, label = ToLabel(x.Col) });

            return new JsonResult(new { workingLoss, fixedLoss });
        }

        // ── GET: LoadExisting ──────────────────────────────
        public JsonResult OnGetLoadExisting(string machineName, DateTime date, string shift)
        {
            try
            {
                string shiftNorm = NormalizeShift(shift);
                using var conn = OpenConn();

                // Cari IdMachine dari MachineList
                int? idMachine = null;
                using (var cmdM = new SqlCommand(
                    "SELECT IdMachine FROM [dbo].[MachineList] WHERE MachineName = @Name", conn))
                {
                    cmdM.Parameters.AddWithValue("@Name", machineName);
                    var res = cmdM.ExecuteScalar();
                    if (res != null && res != DBNull.Value)
                        idMachine = Convert.ToInt32(res);
                }
                if (!idMachine.HasValue) return new JsonResult(new { found = false });

                // Ambil header dari Efficiency
                using var cmd = new SqlCommand(@"
                    SELECT TOP 1 ID, PlanQty, DefectQty, GoodProductionQty, WorkingTime
                    FROM [dbo].[Efficiency]
                    WHERE IdMachine = @IdMachine
                      AND CAST([Date] AS DATE) = CAST(@Date AS DATE)
                      AND Shift = @Shift
                    ORDER BY ID DESC", conn);
                cmd.Parameters.AddWithValue("@IdMachine", idMachine.Value);
                cmd.Parameters.AddWithValue("@Date", date);
                cmd.Parameters.AddWithValue("@Shift", shiftNorm);
                using var r = cmd.ExecuteReader();

                if (!r.Read()) return new JsonResult(new { found = false });

                int effId = Convert.ToInt32(r["ID"]);
                double? G(string col) => r[col] == DBNull.Value ? null : Convert.ToDouble(r[col]);
                var planQty = G("PlanQty");
                var defect = G("DefectQty");
                var goodQty = G("GoodProductionQty");
                var workTime = G("WorkingTime");
                r.Close();

                // Ambil loss dari EfficiencyLoss
                using var cmdL = new SqlCommand(@"
                    SELECT LossCategory, LossMinutes
                    FROM [dbo].[EfficiencyLoss]
                    WHERE EfficiencyID = @ID", conn);
                cmdL.Parameters.AddWithValue("@ID", effId);
                using var rL = cmdL.ExecuteReader();

                var lossDict = new Dictionary<string, double?>();
                while (rL.Read())
                {
                    string cat = rL["LossCategory"]?.ToString() ?? "";
                    double? min = rL["LossMinutes"] == DBNull.Value
                        ? null : Convert.ToDouble(rL["LossMinutes"]);
                    lossDict[cat] = min;
                }

                return new JsonResult(new
                {
                    found = true,
                    planQty,
                    defectQty = defect,
                    goodProductionQty = goodQty,
                    workingTime = workTime,
                    qualityTrouble = lossDict.GetValueOrDefault("QualityTrouble"),
                    modelChangingLoss = lossDict.GetValueOrDefault("ModelChangingLoss"),
                    materialShortageExternal = lossDict.GetValueOrDefault("MaterialShortageExternal"),
                    machineToolsTrouble = lossDict.GetValueOrDefault("MachineToolsTrouble"),
                    manPowerAdjustment = lossDict.GetValueOrDefault("ManPowerAdjustment"),
                    materialShortageInhouse = lossDict.GetValueOrDefault("MaterialShortageInhouse"),
                    materialShortageInternal = lossDict.GetValueOrDefault("MaterialShortageInternal"),
                    setRepairingLoss = lossDict.GetValueOrDefault("SetRepairingLoss"),
                    gawseExternalBodies = lossDict.GetValueOrDefault("GawseExternalBodies"),
                    rework = lossDict.GetValueOrDefault("Rework"),
                    moldChangingLoss = lossDict.GetValueOrDefault("MoldChangingLoss"),
                    breakTime = lossDict.GetValueOrDefault("BreakTime"),
                    companyActivity = lossDict.GetValueOrDefault("CompanyActivity"),
                    morningAssembly = lossDict.GetValueOrDefault("MorningAssembly"),
                    cleaning = lossDict.GetValueOrDefault("Cleaning"),
                    stockOpname = lossDict.GetValueOrDefault("StockOpname"),
                    generalAssembly = lossDict.GetValueOrDefault("GeneralAssembly"),
                    maintenance = lossDict.GetValueOrDefault("Maintenance"),
                    trialRun = lossDict.GetValueOrDefault("TrialRun"),
                    trainingEducation = lossDict.GetValueOrDefault("TrainingEducation"),
                    freeTalkingQC = lossDict.GetValueOrDefault("FreeTalkingQC"),
                    noProductionDay = lossDict.GetValueOrDefault("NoProductionDay"),
                });
            }
            catch (Exception ex) { return new JsonResult(new { found = false, error = ex.Message }); }
        }

        // ── GET: Efficiency (untuk display tabel) ──────────
        public JsonResult OnGetEfficiency(int month, int year)
        {
            if (month < 1 || month > 12) return new JsonResult(new { error = "Bulan tidak valid (1-12)." });
            if (year < 2000 || year > 2100) return new JsonResult(new { error = "Tahun tidak valid." });

            var result = new List<object>();
            try
            {
                using var conn = OpenConn();
                string sql = @"
                    SELECT ml.MachineName,
                        ISNULL(CAST(e.OEE AS VARCHAR),'-')           AS OEE,
                        ISNULL(CAST(e.OperatingRatio AS VARCHAR),'-') AS OperatingRatio,
                        ISNULL(CAST(e.Ability AS VARCHAR),'-')        AS Ability,
                        ISNULL(CAST(e.Quality AS VARCHAR),'-')        AS Quality,
                        ISNULL(CAST(e.Achievement AS VARCHAR),'-')    AS Achievement,
                        CONVERT(VARCHAR, e.Date, 23)                  AS Date
                    FROM [dbo].[Efficiency] e
                    JOIN [dbo].[MachineList] ml ON ml.IdMachine = e.IdMachine
                    WHERE MONTH(e.Date) = @Month AND YEAR(e.Date) = @Year
                    ORDER BY ml.MachineName, e.Date";
                using var cmd = new SqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@Month", month);
                cmd.Parameters.AddWithValue("@Year", year);
                using var reader = cmd.ExecuteReader();
                while (reader.Read())
                    result.Add(new
                    {
                        machineName = reader["MachineName"]?.ToString() ?? "-",
                        oEE = reader["OEE"]?.ToString() ?? "-",
                        operatingRatio = reader["OperatingRatio"]?.ToString() ?? "-",
                        ability = reader["Ability"]?.ToString() ?? "-",
                        quality = reader["Quality"]?.ToString() ?? "-",
                        achievement = reader["Achievement"]?.ToString() ?? "-",
                        date = reader["Date"]?.ToString() ?? "-"
                    });
                return new JsonResult(result);
            }
            catch (Exception ex) { return new JsonResult(new { error = ex.Message }); }
        }

        // ── POST: Submit ───────────────────────────────────
        public JsonResult OnPostSubmit([FromBody] MachineEfficiencyInput input)
        {
            try
            {
                input.Shift = NormalizeShift(input.Shift);
                var (quality, operatingRatio, ability, oee, achievement, category) = CalcMetrics(input);

                using var conn = OpenConn();
                using var tx = conn.BeginTransaction();

                // Cari IdMachine
                int idMachine = 0;
                using (var cmdM = new SqlCommand(
                    "SELECT IdMachine FROM [dbo].[MachineList] WHERE MachineName = @Name", conn, tx))
                {
                    cmdM.Parameters.AddWithValue("@Name", input.MachineName ?? "");
                    var res = cmdM.ExecuteScalar();
                    if (res != null && res != DBNull.Value)
                        idMachine = Convert.ToInt32(res);
                }
                if (idMachine == 0)
                {
                    tx.Rollback();
                    return new JsonResult(new { success = false, error = "Machine tidak ditemukan di MachineList." });
                }

                // Cek existing di Efficiency
                int? existingId = null;
                using (var chk = new SqlCommand(@"
                    SELECT TOP 1 ID FROM [dbo].[Efficiency]
                    WHERE IdMachine = @IdMachine
                      AND CAST([Date] AS DATE) = CAST(@Date AS DATE)
                      AND Shift = @Shift", conn, tx))
                {
                    chk.Parameters.AddWithValue("@IdMachine", idMachine);
                    chk.Parameters.AddWithValue("@Date", input.Date);
                    chk.Parameters.AddWithValue("@Shift", input.Shift ?? "");
                    var res = chk.ExecuteScalar();
                    if (res != null && res != DBNull.Value)
                        existingId = Convert.ToInt32(res);
                }

                int effId;
                if (existingId.HasValue)
                {
                    // UPDATE
                    using var upd = new SqlCommand(@"
                        UPDATE [dbo].[Efficiency] SET
                            WorkingTime = @WorkingTime, PlanQty = @PlanQty,
                            GoodProductionQty = @GoodProductionQty, DefectQty = @DefectQty,
                            OEE = @OEE, OperatingRatio = @OperatingRatio,
                            Ability = @Ability, Quality = @Quality,
                            Achievement = @Achievement, Category = @Category
                        WHERE ID = @ID", conn, tx);
                    AddHeaderParams(upd, idMachine, input, quality, operatingRatio,
                                    ability, oee, achievement, category);
                    upd.Parameters.AddWithValue("@ID", existingId.Value);
                    upd.ExecuteNonQuery();

                    // Hapus loss lama
                    using var del = new SqlCommand(
                        "DELETE FROM [dbo].[EfficiencyLoss] WHERE EfficiencyID = @ID", conn, tx);
                    del.Parameters.AddWithValue("@ID", existingId.Value);
                    del.ExecuteNonQuery();

                    effId = existingId.Value;
                }
                else
                {
                    // INSERT
                    using var ins = new SqlCommand(@"
                        INSERT INTO [dbo].[Efficiency]
                            (IdMachine, Date, Shift, WorkingTime, PlanQty,
                             GoodProductionQty, DefectQty, OEE, OperatingRatio,
                             Ability, Quality, Achievement, Category)
                        VALUES
                            (@IdMachine, @Date, @Shift, @WorkingTime, @PlanQty,
                             @GoodProductionQty, @DefectQty, @OEE, @OperatingRatio,
                             @Ability, @Quality, @Achievement, @Category);
                        SELECT SCOPE_IDENTITY();", conn, tx);
                    AddHeaderParams(ins, idMachine, input, quality, operatingRatio,
                                    ability, oee, achievement, category);
                    effId = Convert.ToInt32(ins.ExecuteScalar());
                }

                InsertLossItems(conn, tx, effId, input.LossItems);
                tx.Commit();

                return new JsonResult(new
                {
                    success = true,
                    action = existingId.HasValue ? "updated" : "inserted"
                });
            }
            catch (Exception ex)
            {
                return new JsonResult(new { success = false, error = ex.Message });
            }
        }

        // ── POST: ImportExcel ──────────────────────────────
        public async Task<JsonResult> OnPostImportExcel(
            IFormFile file, string machineName, int month, int year)
        {
            if (file == null || file.Length == 0)
                return new JsonResult(new { success = false, error = "File tidak boleh kosong." });
            if (string.IsNullOrEmpty(machineName))
                return new JsonResult(new { success = false, error = "Machine harus dipilih." });

            ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
            var shifts = new[] { "Shift 1", "Shift 2", "Shift 3", "Non Shift" };

            const int ROW_QUALITY_TROUBLE = 3;
            const int ROW_MODEL_CHANGING_LOSS = 4;
            const int ROW_MATERIAL_SHORTAGE_EXT = 5;
            const int ROW_MACHINE_TOOLS_TROUBLE = 6;
            const int ROW_MAN_POWER_ADJUSTMENT = 7;
            const int ROW_MATERIAL_SHORTAGE_INHOUSE = 8;
            const int ROW_MATERIAL_SHORTAGE_INTERNAL = 9;
            const int ROW_SET_REPAIRING_LOSS = 10;
            const int ROW_GAWSE_EXTERNAL_BODIES = 11;
            const int ROW_REWORK = 12;
            const int ROW_MOLD_CHANGING_LOSS = 13;
            const int ROW_BREAK_TIME = 15;
            const int ROW_COMPANY_ACTIVITY = 16;
            const int ROW_MORNING_ASSEMBLY = 17;
            const int ROW_CLEANING = 18;
            const int ROW_STOCK_OPNAME = 19;
            const int ROW_GENERAL_ASSEMBLY = 20;
            const int ROW_MAINTENANCE = 21;
            const int ROW_TRIAL_RUN = 22;
            const int ROW_TRAINING_EDUCATION = 23;
            const int ROW_FREE_TALKING_QC = 24;
            const int ROW_NO_PRODUCTION_DAY = 25;
            const int ROW_PLAN = 27;
            const int ROW_GOOD_PRODUCTION_QTY = 28;
            const int ROW_DEFECT_QTY = 29;
            const int ROW_WORKING_TIME = 30;

            try
            {
                // Cari IdMachine dulu
                int idMachine = 0;
                using (var connM = OpenConn())
                using (var cmdM = new SqlCommand(
                    "SELECT IdMachine FROM [dbo].[MachineList] WHERE MachineName = @Name", connM))
                {
                    cmdM.Parameters.AddWithValue("@Name", machineName);
                    var res = cmdM.ExecuteScalar();
                    if (res != null && res != DBNull.Value)
                        idMachine = Convert.ToInt32(res);
                }
                if (idMachine == 0)
                    return new JsonResult(new { success = false, error = "Machine tidak ditemukan di database." });

                var newData = new List<MachineEfficiencyInput>();

                using var stream = new MemoryStream();
                await file.CopyToAsync(stream);
                using var package = new ExcelPackage(stream);
                var sheet = package.Workbook.Worksheets[0];
                int daysInMonth = DateTime.DaysInMonth(year, month);

                for (int day = 1; day <= daysInMonth; day++)
                {
                    for (int si = 0; si < 4; si++)
                    {
                        int col = 3 + (day - 1) * 4 + si;
                        double? D(int row) => TryParseDouble(sheet.Cells[row, col].Value);

                        var lossItems = new List<LossItem>();
                        void AddLoss(string cat, string grp, double? min)
                        {
                            if (min.HasValue && min.Value > 0)
                                lossItems.Add(new LossItem { LossCategory = cat, LossGroup = grp, LossMinutes = min });
                        }

                        AddLoss("QualityTrouble", "WorkingLoss", D(ROW_QUALITY_TROUBLE));
                        AddLoss("ModelChangingLoss", "WorkingLoss", D(ROW_MODEL_CHANGING_LOSS));
                        AddLoss("MaterialShortageExternal", "WorkingLoss", D(ROW_MATERIAL_SHORTAGE_EXT));
                        AddLoss("MachineToolsTrouble", "WorkingLoss", D(ROW_MACHINE_TOOLS_TROUBLE));
                        AddLoss("ManPowerAdjustment", "WorkingLoss", D(ROW_MAN_POWER_ADJUSTMENT));
                        AddLoss("MaterialShortageInhouse", "WorkingLoss", D(ROW_MATERIAL_SHORTAGE_INHOUSE));
                        AddLoss("MaterialShortageInternal", "WorkingLoss", D(ROW_MATERIAL_SHORTAGE_INTERNAL));
                        AddLoss("SetRepairingLoss", "WorkingLoss", D(ROW_SET_REPAIRING_LOSS));
                        AddLoss("GawseExternalBodies", "WorkingLoss", D(ROW_GAWSE_EXTERNAL_BODIES));
                        AddLoss("Rework", "WorkingLoss", D(ROW_REWORK));
                        AddLoss("MoldChangingLoss", "WorkingLoss", D(ROW_MOLD_CHANGING_LOSS));
                        AddLoss("BreakTime", "FixedLoss", D(ROW_BREAK_TIME));
                        AddLoss("CompanyActivity", "FixedLoss", D(ROW_COMPANY_ACTIVITY));
                        AddLoss("MorningAssembly", "FixedLoss", D(ROW_MORNING_ASSEMBLY));
                        AddLoss("Cleaning", "FixedLoss", D(ROW_CLEANING));
                        AddLoss("StockOpname", "FixedLoss", D(ROW_STOCK_OPNAME));
                        AddLoss("GeneralAssembly", "FixedLoss", D(ROW_GENERAL_ASSEMBLY));
                        AddLoss("Maintenance", "FixedLoss", D(ROW_MAINTENANCE));
                        AddLoss("TrialRun", "FixedLoss", D(ROW_TRIAL_RUN));
                        AddLoss("TrainingEducation", "FixedLoss", D(ROW_TRAINING_EDUCATION));
                        AddLoss("FreeTalkingQC", "FixedLoss", D(ROW_FREE_TALKING_QC));
                        AddLoss("NoProductionDay", "FixedLoss", D(ROW_NO_PRODUCTION_DAY));

                        double? planQty = D(ROW_PLAN);
                        double? goodQty = D(ROW_GOOD_PRODUCTION_QTY);
                        double? defectQty = D(ROW_DEFECT_QTY);
                        double? workingTime = D(ROW_WORKING_TIME);

                        bool hasData = planQty.HasValue || goodQty.HasValue
                            || defectQty.HasValue || workingTime.HasValue || lossItems.Count > 0;
                        if (!hasData) continue;

                        newData.Add(new MachineEfficiencyInput
                        {
                            MachineName = machineName,
                            Date = new DateTime(year, month, day),
                            Shift = shifts[si],
                            PlanQty = planQty,
                            GoodProductionQty = goodQty,
                            DefectQty = defectQty,
                            WorkingTime = workingTime,
                            LossItems = lossItems
                        });
                    }
                }

                using var conn = OpenConn();
                using var tx = conn.BeginTransaction();

                // Hapus data lama yang ada di Excel
                foreach (var item in newData)
                {
                    int? existingId = null;
                    using (var chk = new SqlCommand(@"
                        SELECT TOP 1 ID FROM [dbo].[Efficiency]
                        WHERE IdMachine = @IdMachine
                          AND CAST([Date] AS DATE) = CAST(@Date AS DATE)
                          AND Shift = @Shift", conn, tx))
                    {
                        chk.Parameters.AddWithValue("@IdMachine", idMachine);
                        chk.Parameters.AddWithValue("@Date", item.Date);
                        chk.Parameters.AddWithValue("@Shift", item.Shift ?? "");
                        var res = chk.ExecuteScalar();
                        if (res != null && res != DBNull.Value)
                            existingId = Convert.ToInt32(res);
                    }

                    if (existingId.HasValue)
                    {
                        using var delL = new SqlCommand(
                            "DELETE FROM [dbo].[EfficiencyLoss] WHERE EfficiencyID = @ID", conn, tx);
                        delL.Parameters.AddWithValue("@ID", existingId.Value);
                        delL.ExecuteNonQuery();

                        using var delH = new SqlCommand(
                            "DELETE FROM [dbo].[Efficiency] WHERE ID = @ID", conn, tx);
                        delH.Parameters.AddWithValue("@ID", existingId.Value);
                        delH.ExecuteNonQuery();
                    }
                }

                // Insert semua data baru
                int count = 0;
                foreach (var item in newData)
                {
                    var (quality, operatingRatio, ability, oee, achievement, category) = CalcMetrics(item);
                    using var ins = new SqlCommand(@"
                        INSERT INTO [dbo].[Efficiency]
                            (IdMachine, Date, Shift, WorkingTime, PlanQty,
                             GoodProductionQty, DefectQty, OEE, OperatingRatio,
                             Ability, Quality, Achievement, Category)
                        VALUES
                            (@IdMachine, @Date, @Shift, @WorkingTime, @PlanQty,
                             @GoodProductionQty, @DefectQty, @OEE, @OperatingRatio,
                             @Ability, @Quality, @Achievement, @Category);
                        SELECT SCOPE_IDENTITY();", conn, tx);
                    AddHeaderParams(ins, idMachine, item, quality, operatingRatio,
                                    ability, oee, achievement, category);
                    int effId = Convert.ToInt32(ins.ExecuteScalar());
                    InsertLossItems(conn, tx, effId, item.LossItems);
                    count++;
                }

                tx.Commit();
                return new JsonResult(new
                {
                    success = true,
                    message = $"Berhasil import {count} data untuk {machineName} bulan {month}/{year}."
                });
            }
            catch (Exception ex)
            {
                return new JsonResult(new { success = false, error = "Gagal Import: " + ex.Message });
            }
        }

        // ── Helpers ────────────────────────────────────────
        private static void InsertLossItems(SqlConnection conn, SqlTransaction tx,
            int effId, List<LossItem>? items)
        {
            if (items == null || items.Count == 0) return;
            foreach (var loss in items.Where(l => l.LossMinutes.HasValue && l.LossMinutes > 0))
            {
                using var cmd = new SqlCommand(@"
                    INSERT INTO [dbo].[EfficiencyLoss]
                        (EfficiencyID, LossCategory, LossGroup, LossMinutes)
                    VALUES
                        (@EfficiencyID, @LossCategory, @LossGroup, @LossMinutes)", conn, tx);
                cmd.Parameters.AddWithValue("@EfficiencyID", effId);
                cmd.Parameters.AddWithValue("@LossCategory", loss.LossCategory ?? "");
                cmd.Parameters.AddWithValue("@LossGroup", loss.LossGroup ?? "");
                cmd.Parameters.AddWithValue("@LossMinutes", (object?)loss.LossMinutes ?? DBNull.Value);
                cmd.ExecuteNonQuery();
            }
        }

        private static void AddHeaderParams(SqlCommand cmd, int idMachine,
            MachineEfficiencyInput input,
            double? quality, double? operatingRatio, double? ability,
            double? oee, double? achievement, string? category)
        {
            cmd.Parameters.AddWithValue("@IdMachine", idMachine);
            cmd.Parameters.AddWithValue("@Date", input.Date);
            cmd.Parameters.AddWithValue("@Shift", input.Shift ?? "");
            cmd.Parameters.AddWithValue("@WorkingTime", (object?)input.WorkingTime ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@PlanQty", (object?)input.PlanQty ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@GoodProductionQty", (object?)input.GoodProductionQty ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@DefectQty", (object?)input.DefectQty ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@OEE", (object?)oee ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@OperatingRatio", (object?)operatingRatio ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@Ability", (object?)ability ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@Quality", (object?)quality ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@Achievement", (object?)achievement ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@Category", (object?)category ?? DBNull.Value);
        }

        private SqlConnection OpenConn()
        {
            var conn = new SqlConnection(_config.GetConnectionString("MachineConnection")!);
            conn.Open();
            return conn;
        }

        private static double? TryParseDouble(object? val)
        {
            if (val == null) return null;
            return double.TryParse(val.ToString(), out double r) ? r : null;
        }

        public IActionResult OnGetDownloadTemplate()
        {
            var names = new[] { "TemplateMachine.xlsx", "Machine_input_template.xlsx", "template.xlsx" };
            string? path = null;
            foreach (var n in names)
            {
                var c = Path.Combine(_env.WebRootPath, "data", "MachineEfficiency", n);
                if (System.IO.File.Exists(c)) { path = c; break; }
            }
            if (path == null)
                return new NotFoundObjectResult(new { error = "Template tidak ditemukan." });
            return File(System.IO.File.ReadAllBytes(path),
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                "TemplateMachine.xlsx");
        }

        // ── Models ─────────────────────────────────────────
        public class LossItem
        {
            public string? LossCategory { get; set; }
            public string? LossGroup { get; set; }
            public double? LossMinutes { get; set; }
        }

        public class MachineEfficiencyInput
        {
            public string? MachineName { get; set; }
            public DateTime Date { get; set; }
            public string? Shift { get; set; }
            public double? PlanQty { get; set; }
            public double? DefectQty { get; set; }
            public double? GoodProductionQty { get; set; }
            public double? WorkingTime { get; set; }
            public List<LossItem>? LossItems { get; set; }
        }
    }
}