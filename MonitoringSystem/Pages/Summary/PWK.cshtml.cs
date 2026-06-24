using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using MonitoringSystem.Models;
using System.Text.Json;

namespace MonitoringSystem.Pages.Summary
{
    public class PWKModel : PageModel
    {
        private readonly ScaffoldedDbContext _context;
        private readonly ILogger<PWKModel> _logger;

        public PWKModel(ScaffoldedDbContext context, ILogger<PWKModel> logger)
        {
            _context = context;
            _logger = logger;
        }

        [BindProperty(SupportsGet = true)]
        public string FilterDate { get; set; }

        [BindProperty(SupportsGet = true)]
        public string FilterMachineLine { get; set; }

        public List<PwkData> listData { get; set; } = new();

        // ─── GET ───────────────────────────────────────────────
        public async Task OnGetAsync()
        {
            listData = new List<PwkData>();

            try
            {
                var conn = _context.Database.GetDbConnection();
                await conn.OpenAsync();

                using var cmd = conn.CreateCommand();

                var machineFilter = string.IsNullOrEmpty(FilterMachineLine)
                    ? "" : "AND o.MachineCode = @filterMachine";

                cmd.CommandText = $@"
                    SELECT 
                        o.Product_Id AS Data_Id,
                        ISNULL(m.ProductName, o.Product_Id) AS Model,
                        (MAX(o.GoodUnit) - MIN(o.GoodUnit) + 1) AS Actual,
                        o.MachineCode,
                        MIN(o.SN_GOOD) AS SerialFirst,
                        MAX(o.SN_GOOD) AS SerialLast
                    FROM OEESN o
                    LEFT JOIN MasterData m ON m.Product_Id = o.Product_Id
                    WHERE CAST(o.Date AS DATE) = @filterDate
                    {machineFilter}
                    GROUP BY o.Product_Id, m.ProductName, o.MachineCode
                    ORDER BY o.MachineCode, m.ProductName";

                var pDate = cmd.CreateParameter();
                pDate.ParameterName = "@filterDate";
                pDate.Value = string.IsNullOrEmpty(FilterDate)
                    ? DateTime.Now.Date
                    : DateTime.Parse(FilterDate).Date;
                cmd.Parameters.Add(pDate);

                if (!string.IsNullOrEmpty(FilterMachineLine))
                {
                    var pMachine = cmd.CreateParameter();
                    pMachine.ParameterName = "@filterMachine";
                    pMachine.Value = FilterMachineLine;
                    cmd.Parameters.Add(pMachine);
                }

                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    listData.Add(new PwkData
                    {
                        Data_Id = reader["Data_Id"]?.ToString(),
                        Model = reader["Model"]?.ToString(),
                        Actual = reader["Actual"] == DBNull.Value ? null : Convert.ToDecimal(reader["Actual"]),
                        MachineLine = reader["MachineCode"]?.ToString(),
                        SerialFirst = reader["SerialFirst"]?.ToString(),
                        SerialLast = reader["SerialLast"]?.ToString()
                    });
                }

                await conn.CloseAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error loading PWK data: {ex.Message}");
                TempData["StatusMessage"] = "error";
                TempData["Message"] = $"Error loading data: {ex.Message}";
            }
        }

        // ─── GET MISSING SERIALS (AJAX) ────────────────────────
        public async Task<IActionResult> OnGetMissingSerialsAsync(
            string productId,
            string machineCode,
            string filterDate)
        {
            try
            {
                var conn = _context.Database.GetDbConnection();
                await conn.OpenAsync();

                using var cmd = conn.CreateCommand();
                cmd.CommandText = @"
                    SELECT SN_GOOD
                    FROM OEESN
                    WHERE CAST(Date AS DATE) = @filterDate
                      AND Product_Id = @productId
                      AND MachineCode = @machineCode
                      AND SN_GOOD IS NOT NULL
                    ORDER BY SN_GOOD";

                var p1 = cmd.CreateParameter(); p1.ParameterName = "@filterDate";
                p1.Value = DateTime.Parse(filterDate).Date;
                cmd.Parameters.Add(p1);

                var p2 = cmd.CreateParameter(); p2.ParameterName = "@productId";
                p2.Value = productId;
                cmd.Parameters.Add(p2);

                var p3 = cmd.CreateParameter(); p3.ParameterName = "@machineCode";
                p3.Value = machineCode;
                cmd.Parameters.Add(p3);

                var existingSerials = new List<long>();
                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    if (long.TryParse(reader["SN_GOOD"]?.ToString(), out long sn))
                        existingSerials.Add(sn);
                }
                await conn.CloseAsync();

                if (!existingSerials.Any())
                    return new JsonResult(new { serials = Array.Empty<long>(), first = "", last = "" });

                var min = existingSerials.Min();
                var max = existingSerials.Max();
                var existingSet = new HashSet<long>(existingSerials);
                var missing = new List<long>();

                for (long s = min; s <= max; s++)
                {
                    if (!existingSet.Contains(s))
                        missing.Add(s);
                }

                return new JsonResult(new
                {
                    serials = missing,
                    first = min.ToString(),
                    last = max.ToString()
                });
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error getting missing serials: {ex.Message}");
                return new JsonResult(new { error = ex.Message });
            }
        }

        // ─── ADD MISSING SERIAL ────────────────────────────────
        public async Task<IActionResult> OnPostAddSerialAsync(
            [FromBody] AddSerialRequest req)
        {
            try
            {
                var conn = _context.Database.GetDbConnection();
                await conn.OpenAsync();

                // Ambil data referensi dari row yang sudah ada (product + machine + date)
                // untuk copy semua kolom kecuali SN_GOOD
                using var cmdRef = conn.CreateCommand();
                cmdRef.CommandText = @"
                    SELECT TOP 1 *
                    FROM OEESN
                    WHERE CAST(Date AS DATE) = @filterDate
                      AND Product_Id = @productId
                      AND MachineCode = @machineCode
                    ORDER BY Date DESC";

                var pr1 = cmdRef.CreateParameter(); pr1.ParameterName = "@filterDate";
                pr1.Value = DateTime.Parse(req.FilterDate).Date;
                cmdRef.Parameters.Add(pr1);

                var pr2 = cmdRef.CreateParameter(); pr2.ParameterName = "@productId";
                pr2.Value = req.ProductId;
                cmdRef.Parameters.Add(pr2);

                var pr3 = cmdRef.CreateParameter(); pr3.ParameterName = "@machineCode";
                pr3.Value = req.MachineCode;
                cmdRef.Parameters.Add(pr3);

                // Baca kolom yang ada di tabel OEESN
                string shiftMode = "";
                DateTime refDate = DateTime.Now;
                object goodUnit = DBNull.Value;

                using var refReader = await cmdRef.ExecuteReaderAsync();
                if (await refReader.ReadAsync())
                {
                    shiftMode = refReader["ShiftMode"]?.ToString() ?? "";
                    refDate = refReader["Date"] != DBNull.Value
                        ? Convert.ToDateTime(refReader["Date"])
                        : DateTime.Now;

                    // Untuk GoodUnit kita pakai nilai serial itu sendiri
                    // (atau ikut logika existing, tapi SN_GOOD yang beda)
                }
                await refReader.CloseAsync();

                // Tentukan datetime untuk row baru
                DateTime insertDate;
                if (shiftMode?.ToUpper() == "NONSHIFT" || string.IsNullOrEmpty(shiftMode))
                {
                    // NonShift: pakai waktu sekarang tapi tanggal filter
                    var filterDateOnly = DateTime.Parse(req.FilterDate).Date;
                    insertDate = filterDateOnly.Add(DateTime.Now.TimeOfDay);
                }
                else
                {
                    // Shift: waktu di antara scan produk tersebut (pakai refDate)
                    insertDate = refDate;
                }

                // Insert row baru
                using var cmdIns = conn.CreateCommand();
                cmdIns.CommandText = @"
                    INSERT INTO OEESN (Product_Id, MachineCode, SN_GOOD, Date, ShiftMode, GoodUnit)
                    SELECT 
                        @productId, @machineCode, @snGood, @insertDate, ShiftMode, GoodUnit
                    FROM (SELECT TOP 1 ShiftMode, GoodUnit 
                          FROM OEESN 
                          WHERE CAST(Date AS DATE) = @filterDate2
                            AND Product_Id = @productId2
                            AND MachineCode = @machineCode2
                          ORDER BY Date DESC) AS ref";

                var pi1 = cmdIns.CreateParameter(); pi1.ParameterName = "@productId"; pi1.Value = req.ProductId;
                var pi2 = cmdIns.CreateParameter(); pi2.ParameterName = "@machineCode"; pi2.Value = req.MachineCode;
                var pi3 = cmdIns.CreateParameter(); pi3.ParameterName = "@snGood"; pi3.Value = req.SerialNumber;
                var pi4 = cmdIns.CreateParameter(); pi4.ParameterName = "@insertDate"; pi4.Value = insertDate;
                var pi5 = cmdIns.CreateParameter(); pi5.ParameterName = "@filterDate2"; pi5.Value = DateTime.Parse(req.FilterDate).Date;
                var pi6 = cmdIns.CreateParameter(); pi6.ParameterName = "@productId2"; pi6.Value = req.ProductId;
                var pi7 = cmdIns.CreateParameter(); pi7.ParameterName = "@machineCode2"; pi7.Value = req.MachineCode;

                cmdIns.Parameters.Add(pi1); cmdIns.Parameters.Add(pi2); cmdIns.Parameters.Add(pi3);
                cmdIns.Parameters.Add(pi4); cmdIns.Parameters.Add(pi5); cmdIns.Parameters.Add(pi6);
                cmdIns.Parameters.Add(pi7);

                await cmdIns.ExecuteNonQueryAsync();
                await conn.CloseAsync();

                return new JsonResult(new { success = true });
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error adding serial: {ex.Message}");
                return new JsonResult(new { success = false, error = ex.Message });
            }
        }

        // ─── SAVE PWK ──────────────────────────────────────────
        public async Task<IActionResult> OnPostSavePwkAsync(
            List<string> Model,
            List<string> Actual,
            List<string> DataId)
        {
            try
            {
                TempData["StatusMessage"] = "success";
                TempData["Message"] = "Data PWK berhasil disimpan!";
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error saving PWK: {ex.Message}");
                TempData["StatusMessage"] = "error";
                TempData["Message"] = $"Error: {ex.Message}";
            }
            return RedirectToPage(new { FilterDate, FilterMachineLine });
        }

        // ─── UPDATE PWK ────────────────────────────────────────
        public async Task<IActionResult> OnPostUpdateAsync(
            string DataId,
            string Model,
            string Actual)
        {
            try
            {
                TempData["StatusMessage"] = "success";
                TempData["Message"] = "Data berhasil diupdate!";
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error updating PWK: {ex.Message}");
                TempData["StatusMessage"] = "error";
                TempData["Message"] = $"Error: {ex.Message}";
            }
            return RedirectToPage(new { FilterDate, FilterMachineLine });
        }
    }

    // ─── DATA MODELS ───────────────────────────────────────────
    public class PwkData
    {
        public string Data_Id { get; set; }
        public string Model { get; set; }
        public decimal? Actual { get; set; }
        public string MachineLine { get; set; }
        public string SerialFirst { get; set; }
        public string SerialLast { get; set; }
    }

    public class AddSerialRequest
    {
        public string ProductId { get; set; }
        public string MachineCode { get; set; }
        public string SerialNumber { get; set; }
        public string FilterDate { get; set; }
    }
}