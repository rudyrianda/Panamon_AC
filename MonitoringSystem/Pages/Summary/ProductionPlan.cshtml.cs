using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Data.SqlClient;
using System.Text.Json;
using OfficeOpenXml;


namespace MonitoringSystem.Pages.Shared
{
    public class ProductionPlanModel : PageModel
    {
        private readonly IWebHostEnvironment _webHostEnvironment;

        public List<ProductName> listProducts = new List<ProductName>();
        public List<ProductionRecord> listRecords = new List<ProductionRecord>();
        public List<SapPlanRecord> listSapPlans = new List<SapPlanRecord>(); // ← BARU: SAP Plan dari Excel
        private readonly IConfiguration _configuration;
        private string connectionString;

        public string? ProductNames { get; set; }
        public string? MachineCode { get; set; }
        public string? TotalQuantity { get; set; }
        public string? TotalOvertime { get; set; }
        public string? GrandTotal { get; set; }
        public string? Comment { get; set; }
        public DateTime CurrentDate { get; set; }

        [BindProperty(SupportsGet = true)]
        public string? FilterMachineCode { get; set; }

        [BindProperty(SupportsGet = true)]
        public DateTime? FilterDate { get; set; }

        [BindProperty(SupportsGet = true)]
        public List<string>? FilterShifts { get; set; }

        bool allFieldsEmpty = true;

        public ProductionPlanModel(IConfiguration configuration, IWebHostEnvironment webHostEnvironment)
        {
            _configuration = configuration;
            connectionString = _configuration.GetConnectionString("DefaultConnection") ?? "";
            _webHostEnvironment = webHostEnvironment;
        }

        public void OnGet()
        {
            if (string.IsNullOrEmpty(FilterMachineCode)) FilterMachineCode = "MCH1-01";
            CurrentDate = FilterDate.HasValue ? FilterDate.Value.Date : DateTime.Now.Date;
            getListModelName();
            InsertProductionPlanNow();
            getTotalQuantity();
        }   


        public IActionResult getListModelName()
        {
            try
            {
                using (var connection = new SqlConnection(this.connectionString))
                {
                    connection.Open();
                    string query = @"SELECT ProductName FROM Product WHERE MachineCode = @MachineCode;";
                    using (SqlCommand command = new SqlCommand(query, connection))
                    {
                        command.Parameters.AddWithValue("@MachineCode", FilterMachineCode ?? "MCH1-01");
                        using (SqlDataReader dataReader = command.ExecuteReader())
                        {
                            while (dataReader.Read())
                            {
                                listProducts.Add(new ProductName { Name = dataReader.GetString(0) });
                            }
                        }
                    }
                }
                ProductNames = JsonSerializer.Serialize(listProducts);
                return Page();
            }
            catch (Exception ex)
            {
                Console.WriteLine("Exception: " + ex.ToString());
                return Page();
            }
        }

        public void getTotalQuantity()
        {
            try
            {
                using (var connection = new SqlConnection(this.connectionString))
                {
                    connection.Open();
                    string query = @"
                    SELECT 
                        SUM(PR.Quantity) as TotalNormal, 
                        SUM(PR.Overtime) as TotalOvt 
                    FROM ProductionRecords PR
                    INNER JOIN ProductionPlan PP ON PR.PlanId = PP.Id
                    WHERE PP.CurrentDate = @CurrentDate 
                    AND PR.MachineCode = @MachineCode;";

                    using (SqlCommand command = new SqlCommand(query, connection))
                    {
                        command.Parameters.AddWithValue("@CurrentDate", CurrentDate);
                        command.Parameters.AddWithValue("@MachineCode", FilterMachineCode ?? "MCH1-01");

                        using (SqlDataReader reader = command.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                int normal = reader.IsDBNull(0) ? 0 : reader.GetInt32(0);
                                int ovt = reader.IsDBNull(1) ? 0 : reader.GetInt32(1);
                                TotalQuantity = normal.ToString();
                                TotalOvertime = ovt.ToString();
                                GrandTotal = (normal + ovt).ToString();
                            }
                            else
                            {
                                TotalQuantity = "0";
                                TotalOvertime = "0";
                                GrandTotal = "0";
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

        public void InsertProductionPlanNow()
        {
            try
            {
                using (var connection = new SqlConnection(this.connectionString))
                {
                    connection.Open();

                    // Pastikan ProductionPlan untuk tanggal ini ada
                    string queryCheck = @"SELECT COUNT(1) FROM ProductionPlan WHERE CurrentDate = @CurrentDate;";
                    using (SqlCommand commandCheck = new SqlCommand(queryCheck, connection))
                    {
                        commandCheck.Parameters.AddWithValue("@CurrentDate", CurrentDate);
                        int count = (int)commandCheck.ExecuteScalar();
                        if (count == 0)
                        {
                            string queryInsert = @"INSERT INTO ProductionPlan (CurrentDate) VALUES (@CurrentDate);";
                            using (SqlCommand commandInsert = new SqlCommand(queryInsert, connection))
                            {
                                commandInsert.Parameters.AddWithValue("@CurrentDate", CurrentDate);
                                commandInsert.ExecuteNonQuery();
                            }
                        }
                    }

                    // ── 1. AMBIL SAP PLAN (dari tabel SapPlan) ──────────────────────────
                    string querySapPlan = @"
                        SELECT SP.Id, SP.ProductName, SP.SapPlanNormal, SP.SapPlanOvertime
                        FROM SapPlan SP
                        INNER JOIN ProductionPlan PP ON SP.PlanId = PP.Id
                        WHERE PP.CurrentDate = @CurrentDate
                        AND SP.MachineCode = @MachineCode
                        ORDER BY SP.Id ASC;";

                    using (SqlCommand cmdSap = new SqlCommand(querySapPlan, connection))
                    {
                        cmdSap.Parameters.AddWithValue("@CurrentDate", CurrentDate);
                        cmdSap.Parameters.AddWithValue("@MachineCode", FilterMachineCode ?? "MCH1-01");

                        using (SqlDataReader reader = cmdSap.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                listSapPlans.Add(new SapPlanRecord
                                {
                                    Id = reader.GetInt32(0),
                                    ModelName = reader.IsDBNull(1) ? "" : reader.GetString(1),
                                    SapPlanNormal = reader.IsDBNull(2) ? 0 : reader.GetInt32(2),
                                    SapPlanOvertime = reader.IsDBNull(3) ? 0 : reader.GetInt32(3)
                                });
                            }
                        }
                    }

                    // ── 2. AMBIL CHANGE PLAN (dari tabel ProductionRecords / web input) ─
                    string querySelectAllData = @"
                        SELECT 
                            PR.Id, PR.ProductName, PR.Quantity, MD.QtyHour, 
                            ROUND(CAST(PR.Quantity As float)/NULLIF(CAST(MD.QtyHour AS float),0), 2) AS Hour, 
                            PR.Lot, PR.Remark,
                            PR.Overtime, PR.NoDirectOfWorker, PR.NoDirectOfWorkerOvertime, PR.Shift
                        FROM ProductionRecords PR
                        LEFT JOIN MasterData MD ON PR.ProductName = MD.ProductName
                        INNER JOIN ProductionPlan PP ON PR.PlanId = PP.Id 
                        WHERE PP.CurrentDate = @CurrentDate 
                        AND PR.MachineCode = @MachineCode
                        ORDER BY PR.Id ASC;";

                    using (SqlCommand commandSelectAll = new SqlCommand(querySelectAllData, connection))
                    {
                        commandSelectAll.Parameters.AddWithValue("@CurrentDate", CurrentDate);
                        commandSelectAll.Parameters.AddWithValue("@MachineCode", FilterMachineCode ?? "MCH1-01");

                        using (SqlDataReader dataReader = commandSelectAll.ExecuteReader())
                        {
                            while (dataReader.Read())
                            {
                                ProductionRecord record = new ProductionRecord();
                                record.Id = dataReader.GetInt32(0);
                                record.ModelName = dataReader.IsDBNull(1) ? "" : dataReader.GetString(1);
                                record.Quantity = dataReader.IsDBNull(2) ? 0 : dataReader.GetInt32(2);
                                record.QtyHour = dataReader.IsDBNull(3) ? 0 : dataReader.GetInt32(3);
                                record.Hour = dataReader.IsDBNull(4) ? 0 : dataReader.GetDouble(4);
                                record.Lot = dataReader.IsDBNull(5) ? "" : dataReader.GetString(5);
                                record.Remark = dataReader.IsDBNull(6) ? "" : dataReader.GetString(6);
                                record.Overtime = dataReader.IsDBNull(7) ? null : dataReader.GetInt32(7);
                                record.NoDirectOfWorker = dataReader.IsDBNull(8) ? null : dataReader.GetInt32(8);
                                record.NoDirectOfWorkerOvertime = dataReader.IsDBNull(9) ? null : dataReader.GetInt32(9);
                                record.Shift = dataReader.IsDBNull(10) ? "" : dataReader.GetString(10);

                                // ── Cari SAP Plan yang cocok berdasarkan ProductName ──
                                var matchingSap = listSapPlans.FirstOrDefault(s =>
                                    s.ModelName?.Trim().ToLower() == record.ModelName?.Trim().ToLower());
                                if (matchingSap != null)
                                {
                                    record.SapPlanNormal = matchingSap.SapPlanNormal;
                                    record.SapPlanOvertime = matchingSap.SapPlanOvertime;
                                }

                                listRecords.Add(record);
                            }
                        }
                    }

                    // ── 3. AMBIL COMMENT ────────────────────────────────────────────────
                    string commentColumn = (FilterMachineCode == "MCH1-02") ? "Comment_CS" : "Comment_CU";
                    string querySelectComment = $"SELECT {commentColumn} FROM ProductionPlan WHERE CurrentDate = @CurrentDate";

                    using (SqlCommand commandSelectComment = new SqlCommand(querySelectComment, connection))
                    {
                        commandSelectComment.Parameters.AddWithValue("@CurrentDate", CurrentDate);
                        using (SqlDataReader dataComment = commandSelectComment.ExecuteReader())
                        {
                            if (dataComment.Read() && !dataComment.IsDBNull(0))
                            {
                                Comment = dataComment.GetString(0);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Exception Load Data: " + ex.ToString());
            }
        }

        public IActionResult OnPostInsertProduct()
        {
            string productName = Request.Form["ProductName"];

            if (string.IsNullOrEmpty(productName))
            {
                TempData["StatusMessage"] = "error";
                TempData["Message"] = "Model Name is required.";
                return RedirectToPage();
            }

            try
            {
                using (var connection = new SqlConnection(this.connectionString))
                {
                    connection.Open();
                    string query = @"INSERT INTO Product (ProductName, MachineCode) VALUES (@ProductName, 'MCH1-01');";
                    using (SqlCommand command = new SqlCommand(query, connection))
                    {
                        command.Parameters.AddWithValue("@ProductName", productName);
                        command.ExecuteNonQuery();
                    }
                }
                TempData["StatusMessage"] = "success";
                TempData["Message"] = "Product Model successfully inserted!";
                return RedirectToPage();
            }
            catch (Exception ex)
            {
                Console.WriteLine("Exception: " + ex.ToString());
                TempData["StatusMessage"] = "error";
                TempData["Message"] = "Error inserting product: " + ex.Message;
                return Page();
            }
        }

        public IActionResult OnPostInsertProductionRecord(
            List<int?> IdModel,
            List<string> ModelName,
            List<int?> Quantity,
            List<int?> QtyHour,
            List<string> Lot,
            List<string> Remark,
            List<int?> Overtime,
            List<int?> NoOfDirectWorker,
            List<int?> NoOfDirectWorkerOvertime,
            string Comment,
            DateTime TargetDate)
        {
            int planId = 0;
            CurrentDate = TargetDate != DateTime.MinValue ? TargetDate : DateTime.Now.Date;

            string filterMachine = Request.Form["FilterMachineCode"];
            if (!string.IsNullOrEmpty(filterMachine)) FilterMachineCode = filterMachine;

            bool hasInvalidRows = false;
            int savedRowsCount = 0;

            try
            {
                using (var connection = new SqlConnection(this.connectionString))
                {
                    connection.Open();

                    string querySelectPlanId = @"SELECT TOP 1 Id FROM ProductionPlan WHERE CurrentDate = @CurrentDate;";
                    using (SqlCommand commandSelectId = new SqlCommand(querySelectPlanId, connection))
                    {
                        commandSelectId.Parameters.AddWithValue("@CurrentDate", CurrentDate);
                        var res = commandSelectId.ExecuteScalar();
                        if (res != null) planId = (int)res;
                        else
                        {
                            string qInsPlan = @"INSERT INTO ProductionPlan (CurrentDate) VALUES (@CurrentDate); SELECT SCOPE_IDENTITY();";
                            using (SqlCommand cIns = new SqlCommand(qInsPlan, connection))
                            {
                                cIns.Parameters.AddWithValue("@CurrentDate", CurrentDate);
                                planId = Convert.ToInt32(cIns.ExecuteScalar());
                            }
                        }
                    }

                    if (!string.IsNullOrEmpty(Comment) && planId > 0)
                    {
                        string targetColumn = (FilterMachineCode == "MCH1-02") ? "Comment_CS" : "Comment_CU";
                        string queryUpdate = $"UPDATE ProductionPlan SET {targetColumn} = @Comment WHERE Id = @Id;";
                        using (SqlCommand cmd = new SqlCommand(queryUpdate, connection))
                        {
                            cmd.Parameters.AddWithValue("@Id", planId);
                            cmd.Parameters.AddWithValue("@Comment", Comment);
                            cmd.ExecuteNonQuery();
                        }
                    }

                    // ── TAMBAHAN: Kumpulkan Id existing dari form, lalu hapus yang tidak ada ──
                    var submittedIds = new List<int>();
                    for (int k = 0; k < ModelName.Count; k++)
                    {
                        string rawIdK = Request.Form[$"IdModel[{k}]"];
                        if (int.TryParse(rawIdK, out int pid) && pid > 0)
                            submittedIds.Add(pid);
                    }

                    if (submittedIds.Count > 0)
                    {
                        string inClause = string.Join(",", submittedIds);
                        string queryDelOld = $@"DELETE FROM ProductionRecords 
                                                WHERE PlanId = @PlanId 
                                                AND MachineCode = @Mc2 
                                                AND Id NOT IN ({inClause})";
                        using (SqlCommand cmdDel = new SqlCommand(queryDelOld, connection))
                        {
                            cmdDel.Parameters.AddWithValue("@PlanId", planId);
                            cmdDel.Parameters.AddWithValue("@Mc2", FilterMachineCode ?? "MCH1-01");
                            cmdDel.ExecuteNonQuery();
                        }
                    }
                    // ── AKHIR TAMBAHAN ──

                    for (int i = 0; i < ModelName.Count; i++)
                    {
                        string safeModelName = (ModelName != null && ModelName.Count > i) ? ModelName[i] : "";
                        int? safeQty = (Quantity != null && Quantity.Count > i) ? Quantity[i] : null;
                        int? safeWorker = (NoOfDirectWorker != null && NoOfDirectWorker.Count > i) ? NoOfDirectWorker[i] : null;
                        int? safeQtyHour = (QtyHour != null && QtyHour.Count > i) ? QtyHour[i] : null;
                        string safeLot = (Lot != null && Lot.Count > i) ? Lot[i] : null;
                        string safeRemark = (Remark != null && Remark.Count > i) ? Remark[i] : null;
                        int? safeOvertime = (Overtime != null && Overtime.Count > i) ? Overtime[i] : null;
                        int? safeWorkerOvt = (NoOfDirectWorkerOvertime != null && NoOfDirectWorkerOvertime.Count > i) ? NoOfDirectWorkerOvertime[i] : null;

                        bool isRowEmpty = string.IsNullOrEmpty(safeModelName) &&
                                          (!safeQty.HasValue || safeQty == 0) &&
                                          (!safeWorker.HasValue);
                        if (isRowEmpty) continue;

                        bool isRowValid = !string.IsNullOrEmpty(safeModelName) &&
                                          (safeQty.HasValue && safeQty > 0) &&
                                          safeWorker.HasValue;
                        if (!isRowValid)
                        {
                            hasInvalidRows = true;
                            continue;
                        }

                        string shiftValue = "NS";
                        string shiftKey = $"Shift[{i}]";
                        if (Request.Form.ContainsKey(shiftKey))
                        {
                            shiftValue = string.Join(",", Request.Form[shiftKey]);
                        }
                        if (string.IsNullOrEmpty(shiftValue)) shiftValue = "NS";

                        if (safeQtyHour.HasValue && !string.IsNullOrEmpty(safeModelName))
                        {
                            string qUpdMaster = @"UPDATE MasterData SET QtyHour = @QtyHour WHERE ProductName = @ProductName;";
                            using (SqlCommand cmd = new SqlCommand(qUpdMaster, connection))
                            {
                                cmd.Parameters.AddWithValue("@QtyHour", safeQtyHour);
                                cmd.Parameters.AddWithValue("@ProductName", safeModelName);
                                cmd.ExecuteNonQuery();
                            }
                        }

                        string rawId = Request.Form[$"IdModel[{i}]"];
                        int? safeId = int.TryParse(rawId, out int parsedId) && parsedId > 0 ? parsedId : (int?)null;
                        string querySQL = "";

                        if (safeId.HasValue && safeId > 0)
                        {
                            // UPDATE: hanya update kolom Change Plan, TIDAK menyentuh SapPlan
                            querySQL = @"UPDATE ProductionRecords 
                                 SET ProductName=@Pn, Quantity=@Qty, Lot=@Lot, Remark=@Rem, 
                                     Overtime=@Ovt, NoDirectOfWorker=@WNorm, NoDirectOfWorkerOvertime=@WOvt, Shift=@Sh
                                 WHERE Id=@Id";
                        }
                        else
                        {
                            string queryFindExisting = @"SELECT TOP 1 Id FROM ProductionRecords 
                                                          WHERE PlanId = @Pid AND ProductName = @Pn 
                                                          AND MachineCode = @Mc";
                            using (SqlCommand cmdFind = new SqlCommand(queryFindExisting, connection))
                            {
                                cmdFind.Parameters.AddWithValue("@Pid", planId);
                                cmdFind.Parameters.AddWithValue("@Pn", safeModelName);
                                cmdFind.Parameters.AddWithValue("@Mc", FilterMachineCode ?? "MCH1-01");
                                var existingId = cmdFind.ExecuteScalar();

                                if (existingId != null)
                                {
                                    safeId = (int)existingId;
                                    querySQL = @"UPDATE ProductionRecords 
                                                 SET ProductName=@Pn, Quantity=@Qty, Lot=@Lot, Remark=@Rem, 
                                                     Overtime=@Ovt, NoDirectOfWorker=@WNorm, 
                                                     NoDirectOfWorkerOvertime=@WOvt, Shift=@Sh
                                                 WHERE Id=@Id";
                                }
                                else
                                {
                                    querySQL = @"INSERT INTO ProductionRecords 
                                        (PlanID, ProductName, MachineCode, Quantity, Lot, Remark, Overtime, NoDirectOfWorker, NoDirectOfWorkerOvertime, Shift) 
                                        VALUES (@Pid, @Pn, @Mc, @Qty, @Lot, @Rem, @Ovt, @WNorm, @WOvt, @Sh);";
                                }
                            }
                        }

                        using (SqlCommand cmd = new SqlCommand(querySQL, connection))
                        {
                            cmd.Parameters.AddWithValue("@Pn", safeModelName);
                            cmd.Parameters.AddWithValue("@Qty", safeQty);
                            cmd.Parameters.AddWithValue("@WNorm", safeWorker);
                            cmd.Parameters.AddWithValue("@Sh", shiftValue);
                            cmd.Parameters.AddWithValue("@Ovt", (object)safeOvertime ?? DBNull.Value);
                            cmd.Parameters.AddWithValue("@WOvt", (object)safeWorkerOvt ?? DBNull.Value);
                            cmd.Parameters.AddWithValue("@Lot", (object)safeLot ?? DBNull.Value);
                            cmd.Parameters.AddWithValue("@Rem", (object)safeRemark ?? DBNull.Value);

                            if (safeId.HasValue && safeId > 0)
                            {
                                cmd.Parameters.AddWithValue("@Id", safeId);
                            }
                            else
                            {
                                cmd.Parameters.AddWithValue("@Pid", planId);
                                string mCode = FilterMachineCode ?? "MCH1-01";
                                string qM = "SELECT TOP 1 MachineCode FROM Product WHERE ProductName = @Pn";
                                using (SqlCommand cM = new SqlCommand(qM, connection))
                                {
                                    cM.Parameters.AddWithValue("@Pn", safeModelName);
                                    var resM = cM.ExecuteScalar();
                                    if (resM != null) mCode = resM.ToString();
                                }
                                cmd.Parameters.AddWithValue("@Mc", mCode);
                            }

                            cmd.ExecuteNonQuery();
                            savedRowsCount++;
                        }
                    }

                    if (savedRowsCount > 0)
                    {
                        TempData["StatusMessage"] = hasInvalidRows ? "warning" : "success";
                        TempData["Message"] = hasInvalidRows
                            ? "Data Saved, but some rows were SKIPPED because Product Name, Quantity, or Normal Worker were empty."
                            : "All Production Plan saved successfully!";
                    }
                    else
                    {
                        TempData["StatusMessage"] = hasInvalidRows ? "error" : "info";
                        TempData["Message"] = hasInvalidRows
                            ? "Action Failed! Please fill in Product Name, Quantity, and Worker (Normal) for at least one row."
                            : "No data to save.";
                    }

                    return RedirectToPage(new { FilterDate = CurrentDate.ToString("yyyy-MM-dd"), FilterMachineCode = FilterMachineCode });
                }
            }
            catch (Exception ex)
            {
                TempData["StatusMessage"] = "error";
                TempData["Message"] = "Error: " + ex.Message;
                return RedirectToPage(new { FilterDate = CurrentDate.ToString("yyyy-MM-dd"), FilterMachineCode = FilterMachineCode });
            }
        }

        public async Task<IActionResult> OnPostDeleteRecordAsync()
        {
            string recordId = Request.Form["RecordId"];
            try
            {
                using (var connection = new SqlConnection(this.connectionString))
                {
                    connection.Open();
                    string queryDelete = @"DELETE FROM ProductionRecords WHERE Id = @RecordId;";
                    using (SqlCommand commandDelete = new SqlCommand(queryDelete, connection))
                    {
                        commandDelete.Parameters.AddWithValue("@RecordId", recordId);
                        int rowsAffected = await commandDelete.ExecuteNonQueryAsync();
                        TempData["StatusMessage"] = rowsAffected > 0 ? "success" : "error";
                        TempData["Message"] = rowsAffected > 0 ? "Data deleted successfully" : "Data not found";
                        return RedirectToPage();
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Exception: " + ex.ToString());
                TempData["StatusMessage"] = "error";
                TempData["Message"] = "Error deleting records: " + ex.Message;
                return RedirectToPage(new { FilterDate = CurrentDate.ToString("yyyy-MM-dd"), FilterMachineCode = FilterMachineCode });
            }
        }

        public async Task<IActionResult> OnPostDeleteAllRecord()
        {
            int planId = 0;
            CurrentDate = DateTime.Now.Date;
            try
            {
                using (var connection = new SqlConnection(this.connectionString))
                {
                    connection.Open();
                    string queryGetId = "SELECT Id FROM ProductionPlan WHERE CurrentDate = @CurrentDate;";
                    using (SqlCommand commandGetId = new SqlCommand(queryGetId, connection))
                    {
                        commandGetId.Parameters.AddWithValue("@CurrentDate", CurrentDate);
                        using (SqlDataReader dataReader = commandGetId.ExecuteReader())
                        {
                            while (dataReader.Read()) { planId = dataReader.GetInt32(0); }
                        }
                    }

                    // Hanya delete ProductionRecords (Change Plan), TIDAK hapus SapPlan
                    string queryDelete = "DELETE FROM ProductionRecords WHERE PlanId = @PlanId;";
                    using (SqlCommand commandDelete = new SqlCommand(queryDelete, connection))
                    {
                        commandDelete.Parameters.AddWithValue("@PlanId", planId);
                        int rowsAffected = await commandDelete.ExecuteNonQueryAsync();
                        TempData["StatusMessage"] = rowsAffected > 0 ? "success" : "error";
                        TempData["Message"] = rowsAffected > 0 ? "Data deleted successfully" : "Data not found";
                        return RedirectToPage();
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Exception: " + ex.ToString());
                TempData["StatusMessage"] = "error";
                TempData["Message"] = "Error deleting data: " + ex.Message;
                return Page();
            }
        }

        public IActionResult OnPostUpdateProduct()
        {
            string id = Request.Form["Id"];
            string ProductName = Request.Form["ProductName"];
            string Quantity = Request.Form["Quantity"];
            string QtyHour = Request.Form["QtyHour"];
            string Lot = Request.Form["Lot"];
            string Remark = Request.Form["Remark"];
            string Overtime = Request.Form["Overtime"];
            string NoOfDirectWorker = Request.Form["NoOfDirectWorker"];
            string NoOfDirectWorkerOvertime = Request.Form["NoOfDirectWorkerOvertime"];

            string targetDateString = Request.Form["TargetDate"];
            DateTime targetDate = DateTime.Now.Date;
            if (DateTime.TryParse(targetDateString, out DateTime parsedDate)) targetDate = parsedDate;

            string shiftValue = "";
            if (Request.Form.ContainsKey("Shift"))
            {
                shiftValue = string.Join(",", Request.Form["Shift"]);
            }

            try
            {
                using (var connection = new SqlConnection(this.connectionString))
                {
                    connection.Open();

                    if (!string.IsNullOrEmpty(QtyHour))
                    {
                        string queryUpdate = @"UPDATE MasterData SET QtyHour = @QtyHour WHERE ProductName = @ProductName;";
                        using (SqlCommand commandUpdate = new SqlCommand(queryUpdate, connection))
                        {
                            commandUpdate.Parameters.AddWithValue("@QtyHour", QtyHour);
                            commandUpdate.Parameters.AddWithValue("@ProductName", ProductName);
                            commandUpdate.ExecuteNonQuery();
                        }
                    }

                    // Update hanya ProductionRecords (Change Plan), TIDAK menyentuh SapPlan
                    string query = @"UPDATE ProductionRecords 
                             SET ProductName = @ProductName, 
                                 Quantity = @Quantity,
                                 Overtime = @Overtime,
                                 NoDirectOfWorker = @WNorm,
                                 NoDirectOfWorkerOvertime = @WOvt,
                                 Shift = @Shift
                             WHERE Id = @Id";

                    using (SqlCommand command = new SqlCommand(query, connection))
                    {
                        command.Parameters.AddWithValue("@Id", id);
                        command.Parameters.AddWithValue("@ProductName", ProductName ?? (object)DBNull.Value);
                        command.Parameters.AddWithValue("@Quantity", Quantity ?? (object)DBNull.Value);
                        command.Parameters.AddWithValue("@Overtime", Overtime ?? (object)DBNull.Value);
                        command.Parameters.AddWithValue("@WNorm", NoOfDirectWorker ?? (object)DBNull.Value);
                        command.Parameters.AddWithValue("@WOvt", NoOfDirectWorkerOvertime ?? (object)DBNull.Value);
                        command.Parameters.AddWithValue("@Shift", shiftValue);
                        command.ExecuteNonQuery();
                    }
                }
                TempData["StatusMessage"] = "success";
                TempData["Message"] = "Data successfully updated!";
                return RedirectToPage(new { FilterDate = targetDate.ToString("yyyy-MM-dd"), FilterMachineCode = FilterMachineCode });
            }
            catch (Exception ex)
            {
                TempData["StatusMessage"] = "error";
                TempData["Message"] = "Error updating data: " + ex.Message;
                return RedirectToPage(new { FilterDate = targetDate.ToString("yyyy-MM-dd"), FilterMachineCode = FilterMachineCode });
            }
        }

        [HttpPost]
        public async Task<IActionResult> OnPostSubmitCounter([FromBody] SubmitCount submitCount)
        {
            if (submitCount == null) return BadRequest();

            try
            {
                using (var connection = new SqlConnection(this.connectionString))
                {
                    connection.Open();
                    string queryInsert = @"INSERT INTO SubmitCounts (SubmitCount, Timestamp) VALUES (1, GETDATE());";
                    using (SqlCommand commandInsert = new SqlCommand(queryInsert, connection))
                    {
                        await commandInsert.ExecuteNonQueryAsync();
                    }
                }

                int updatedCount = 0;
                using (var connection = new SqlConnection(this.connectionString))
                {
                    connection.Open();
                    string queryCount = @"SELECT COUNT(*) FROM SubmitCounts WHERE CAST(Timestamp AS DATE) = @CurrentDate;";
                    using (SqlCommand commandCount = new SqlCommand(queryCount, connection))
                    {
                        commandCount.Parameters.AddWithValue("@CurrentDate", DateTime.Now.Date);
                        updatedCount = (int)commandCount.ExecuteScalar();
                    }
                }

                return new JsonResult(new { success = true, count = updatedCount });
            }
            catch (Exception ex)
            {
                Console.WriteLine("Exception: " + ex.ToString());
                return new JsonResult(new { success = false, message = "Internal server error" });
            }
        }

        [HttpGet]
        [Route("/OnGetGetSubmitCounter")]
        public async Task<IActionResult> OnGetGetSubmitCounter()
        {
            int submitCount = 0;
            CurrentDate = DateTime.Now.Date;

            try
            {
                using (var connection = new SqlConnection(this.connectionString))
                {
                    connection.Open();
                    string query = @"SELECT COUNT(*) FROM SubmitCounts WHERE CAST(Timestamp AS DATE) = @CurrentDate;";
                    using (SqlCommand command = new SqlCommand(query, connection))
                    {
                        command.Parameters.AddWithValue("@CurrentDate", CurrentDate);
                        submitCount = (int)command.ExecuteScalar();
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Exception: " + ex.ToString());
                return new JsonResult(new { success = false, message = "Error fetching submit count" });
            }

            return new JsonResult(new { success = true, count = submitCount });
        }

        // ── UPLOAD EXCEL → INSERT KE SapPlan (BUKAN ProductionRecords) ─────────
        public async Task<IActionResult> OnPostUploadAsync(IFormFile UploadedFile, string TargetMachine, int TargetMonth, int TargetYear)
        {
            if (UploadedFile == null || UploadedFile.Length == 0)
            {
                TempData["StatusMessage"] = "error";
                TempData["Message"] = "File Excel tidak ditemukan.";
                return RedirectToPage();
            }

            ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
            int totalSaved = 0;
            int daysInMonth = DateTime.DaysInMonth(TargetYear, TargetMonth);

            try
            {
                using (var stream = new MemoryStream())
                {
                    await UploadedFile.CopyToAsync(stream);
                    using (var package = new ExcelPackage(stream))
                    {
                        ExcelWorksheet worksheet = package.Workbook.Worksheets[0];
                        int rowCount = worksheet.Dimension.Rows;

                        using (var connection = new SqlConnection(this.connectionString))
                        {
                            connection.Open();
                            using (SqlTransaction transaction = connection.BeginTransaction())
                            {
                                try
                                {
                                    for (int row = 3; row <= rowCount; row++)
                                    {
                                        string modelName = worksheet.Cells[row, 2].Value?.ToString()?.Trim();
                                        if (string.IsNullOrEmpty(modelName)) continue;

                                        // ← TAMBAHAN BARU
                                        bool isCuMachine = TargetMachine == "MCH1-01";
                                        bool isCsMachine = TargetMachine == "MCH1-02";
                                        if (isCuMachine && modelName.StartsWith("CS-", StringComparison.OrdinalIgnoreCase)) continue;
                                        if (isCsMachine && modelName.StartsWith("CU-", StringComparison.OrdinalIgnoreCase)) continue;

                                        string queryDeleteModel = @"
        DELETE SP FROM SapPlan SP
        INNER JOIN ProductionPlan PP ON SP.PlanId = PP.Id
        WHERE SP.MachineCode = @MachineCode
        AND SP.ProductName = @ProductName
        AND MONTH(PP.CurrentDate) = @Month
        AND YEAR(PP.CurrentDate) = @Year;";

                                        using (SqlCommand cmdDel = new SqlCommand(queryDeleteModel, connection, transaction))
                                        {
                                            cmdDel.Parameters.AddWithValue("@MachineCode", TargetMachine);
                                            cmdDel.Parameters.AddWithValue("@ProductName", modelName);
                                            cmdDel.Parameters.AddWithValue("@Month", TargetMonth);
                                            cmdDel.Parameters.AddWithValue("@Year", TargetYear);
                                            cmdDel.ExecuteNonQuery();
                                        }

                                        for (int day = 1; day <= daysInMonth; day++)
                                        {
                                            int colNormal = 3 + ((day - 1) * 2);
                                            int colOvertime = colNormal + 1;

                                            var valNormal = worksheet.Cells[row, colNormal].Value;
                                            var valOvertime = worksheet.Cells[row, colOvertime].Value;

                                            int qtyNormal = 0;
                                            int qtyOvertime = 0;

                                            if (valNormal != null) int.TryParse(valNormal.ToString(), out qtyNormal);
                                            if (valOvertime != null) int.TryParse(valOvertime.ToString(), out qtyOvertime);

                                            if (qtyNormal > 0 || qtyOvertime > 0)
                                            {
                                                DateTime currentDate = new DateTime(TargetYear, TargetMonth, day);
                                                int planId = GetOrCreatePlanId(connection, transaction, currentDate);
                                                InsertSapPlanFromExcel(connection, transaction, planId, modelName, TargetMachine, qtyNormal, qtyOvertime);
                                                totalSaved++;
                                            }
                                        }
                                    }

                                    transaction.Commit();
                                    TempData["StatusMessage"] = "success";
                                    TempData["Message"] = $"Upload Berhasil! {totalSaved} SAP Plan record berhasil disimpan.";
                                }
                                catch (Exception ex)
                                {
                                    transaction.Rollback();
                                    throw ex;
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Upload Error: " + ex.ToString());
                TempData["StatusMessage"] = "error";
                TempData["Message"] = "Gagal memproses file: " + ex.Message;
            }

            DateTime redirectDate = new DateTime(TargetYear, TargetMonth, 1);
            return RedirectToPage(new { FilterDate = redirectDate.ToString("yyyy-MM-dd"), FilterMachineCode = TargetMachine });
        }

        private int GetOrCreatePlanId(SqlConnection conn, SqlTransaction trans, DateTime date)
        {
            string queryCheck = "SELECT Id FROM ProductionPlan WHERE CurrentDate = @Date";
            using (SqlCommand cmd = new SqlCommand(queryCheck, conn, trans))
            {
                cmd.Parameters.AddWithValue("@Date", date);
                var res = cmd.ExecuteScalar();
                if (res != null) return (int)res;
            }

            string queryInsert = "INSERT INTO ProductionPlan (CurrentDate) VALUES (@Date); SELECT SCOPE_IDENTITY();";
            using (SqlCommand cmd = new SqlCommand(queryInsert, conn, trans))
            {
                cmd.Parameters.AddWithValue("@Date", date);
                return Convert.ToInt32(cmd.ExecuteScalar());
            }
        }

        // ── INSERT KE SapPlan (dari Excel) ─────────────────────────────────────
        private void InsertSapPlanFromExcel(SqlConnection conn, SqlTransaction trans, int planId, string modelName, string machineCode, int normalQty, int overtimeQty)
        {
            string query = @"
                IF EXISTS (SELECT 1 FROM SapPlan WHERE PlanId = @PlanId AND ProductName = @Pn AND MachineCode = @Mc)
                BEGIN
                    UPDATE SapPlan 
                    SET SapPlanNormal = @Normal, SapPlanOvertime = @Overtime
                    WHERE PlanId = @PlanId AND ProductName = @Pn AND MachineCode = @Mc;
                END
                ELSE
                BEGIN
                    INSERT INTO SapPlan (PlanId, ProductName, MachineCode, SapPlanNormal, SapPlanOvertime)
                    VALUES (@PlanId, @Pn, @Mc, @Normal, @Overtime);
                END";

            using (SqlCommand cmd = new SqlCommand(query, conn, trans))
            {
                cmd.Parameters.AddWithValue("@PlanId", planId);
                cmd.Parameters.AddWithValue("@Pn", modelName);
                cmd.Parameters.AddWithValue("@Mc", machineCode);
                cmd.Parameters.AddWithValue("@Normal", normalQty);
                cmd.Parameters.AddWithValue("@Overtime", overtimeQty > 0 ? (object)overtimeQty : DBNull.Value);
                cmd.ExecuteNonQuery();
            }
        }
        
        public IActionResult OnGetDownloadTemplate()
        {
            var filePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "data", "productionplan", "ProductionPlan_Template.xlsx");
            if (!System.IO.File.Exists(filePath)) return NotFound("File template tidak ditemukan di server.");
            var bytes = System.IO.File.ReadAllBytes(filePath);
            return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "ProductionPlan_Template.xlsx");
        }

        // ── CLASSES ─────────────────────────────────────────────────────────────
        public class ProductName
        {
            public string? Name { get; set; }
        }

        public class ProductionRecord
        {
            public int Id { get; set; }
            public string? ModelName { get; set; }
            public int? Quantity { get; set; }
            public int? Overtime { get; set; }
            public int? NoDirectOfWorker { get; set; }
            public int? NoDirectOfWorkerOvertime { get; set; }
            public string? Shift { get; set; }
            public int? QtyHour { get; set; }
            public double? Hour { get; set; }
            public string? Lot { get; set; }
            public string? Remark { get; set; }
            // ← SAP Plan reference (diisi dari join saat load)
            public int? SapPlanNormal { get; set; }
            public int? SapPlanOvertime { get; set; }
        }

        // ← BARU: Class untuk SAP Plan
        public class SapPlanRecord
        {
            public int Id { get; set; }
            public string? ModelName { get; set; }
            public int SapPlanNormal { get; set; }
            public int SapPlanOvertime { get; set; }
        }

        public class SubmitCount
        {
            public int Id { get; set; }
            public int SubmitCounter { get; set; }
            public DateTime Timestamp { get; set; }
        }
    }
}