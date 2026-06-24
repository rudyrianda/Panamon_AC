using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using MonitoringSystem.Models;

namespace MonitoringSystem.Pages.Shared
{
    public class SutModel : PageModel
    {
        private readonly ScaffoldedDbContext _context;

        public SutModel(ScaffoldedDbContext context)
        {
            _context = context;
        }

        [BindProperty(SupportsGet = true)]
        public string? FilterMachineCode { get; set; }

        public List<ProductSut> listProducts { get; set; } = new();

        // ─── GET ───────────────────────────────────────────────
        public async Task OnGetAsync()
        {
            listProducts = new List<ProductSut>();

            var conn = _context.Database.GetDbConnection();
            try
            {
                await conn.OpenAsync();
                using var cmd = conn.CreateCommand();

                if (string.IsNullOrEmpty(FilterMachineCode))
                    cmd.CommandText = "SELECT Product_Id, ProductName, MachineCode, Description, ProdPlan, SUT, NoOfOperator, QtyHour, ProdHeadHour, CycleTimeVacum, WorkHour FROM MasterData ORDER BY ProductName";
                else
                {
                    cmd.CommandText = "SELECT Product_Id, ProductName, MachineCode, Description, ProdPlan, SUT, NoOfOperator, QtyHour, ProdHeadHour, CycleTimeVacum, WorkHour FROM MasterData WHERE MachineCode = @mc ORDER BY ProductName";
                    var p = cmd.CreateParameter();
                    p.ParameterName = "@mc";
                    p.Value = FilterMachineCode;
                    cmd.Parameters.Add(p);
                }

                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    listProducts.Add(new ProductSut
                    {
                        Product_Id = reader["Product_Id"]?.ToString(),
                        ProductName = reader["ProductName"]?.ToString(),
                        MachineCode = reader["MachineCode"]?.ToString(),
                        Description = reader["Description"]?.ToString(),
                        ProdPlan = reader["ProdPlan"] == DBNull.Value ? null : Convert.ToInt32(reader["ProdPlan"]),
                        SUT = reader["SUT"] == DBNull.Value ? null : Convert.ToInt32(reader["SUT"]),
                        NoOfOperator = reader["NoOfOperator"] == DBNull.Value ? null : Convert.ToInt32(reader["NoOfOperator"]),
                        QtyHour = reader["QtyHour"] == DBNull.Value ? null : Convert.ToInt32(reader["QtyHour"]),
                        ProdHeadHour = reader["ProdHeadHour"] == DBNull.Value ? null : Convert.ToInt32(reader["ProdHeadHour"]),
                        CycleTimeVacum = reader["CycleTimeVacum"] == DBNull.Value ? null : Convert.ToInt32(reader["CycleTimeVacum"]),
                        WorkHour = reader["WorkHour"] == DBNull.Value ? null : Convert.ToInt32(reader["WorkHour"]),
                    });
                }
            }
            finally
            {
                await conn.CloseAsync();
            }
        }

        // ─── INSERT ────────────────────────────────────────────
        public async Task<IActionResult> OnPostInsertAsync(
            string? FilterMachineCode,
            string? ProductName, string? MachineCode, string? Description,
            int? ProdPlan, int? SUT, int? NoOfOperator, int? QtyHour,
            int? ProdHeadHour, int? CycleTimeVacum, int? WorkHour)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(ProductName))
                {
                    TempData["StatusMessage"] = "error";
                    TempData["Message"] = "Product Name wajib diisi.";
                    return RedirectToPage(new { FilterMachineCode });
                }

                await _context.Database.ExecuteSqlRawAsync(@"
                    INSERT INTO MasterData (ProductName, MachineCode, Description, ProdPlan, SUT, NoOfOperator, QtyHour, ProdHeadHour, CycleTimeVacum, WorkHour)
                    VALUES ({0}, {1}, {2}, {3}, {4}, {5}, {6}, {7}, {8}, {9})",
                    ProductName, MachineCode ?? (object)DBNull.Value,
                    Description ?? (object)DBNull.Value,
                    ProdPlan ?? (object)DBNull.Value,
                    SUT ?? (object)DBNull.Value,
                    NoOfOperator ?? (object)DBNull.Value,
                    QtyHour ?? (object)DBNull.Value,
                    ProdHeadHour ?? (object)DBNull.Value,
                    CycleTimeVacum ?? (object)DBNull.Value,
                    WorkHour ?? (object)DBNull.Value);

                TempData["StatusMessage"] = "success";
                TempData["Message"] = $"Product '{ProductName}' berhasil ditambahkan.";
            }
            catch (Exception ex)
            {
                TempData["StatusMessage"] = "error";
                TempData["Message"] = $"Gagal menambahkan: {ex.Message}";
            }
            return RedirectToPage(new { FilterMachineCode });
        }

        // ─── UPDATE ────────────────────────────────────────────
        public async Task<IActionResult> OnPostUpdateAsync(
            string? FilterMachineCode,
            string? ProductId,
            string? ProductName, string? MachineCode, string? Description,
            int? ProdPlan, int? SUT, int? NoOfOperator, int? QtyHour,
            int? ProdHeadHour, int? CycleTimeVacum, int? WorkHour)
        {
            try
            {
                await _context.Database.ExecuteSqlRawAsync(@"
                    UPDATE MasterData SET
                        ProductName    = {0},
                        MachineCode    = {1},
                        Description    = {2},
                        ProdPlan       = {3},
                        SUT            = {4},
                        NoOfOperator   = {5},
                        QtyHour        = {6},
                        ProdHeadHour   = {7},
                        CycleTimeVacum = {8},
                        WorkHour       = {9}
                    WHERE Product_Id   = {10}",
                    ProductName ?? (object)DBNull.Value,
                    MachineCode ?? (object)DBNull.Value,
                    Description ?? (object)DBNull.Value,
                    ProdPlan ?? (object)DBNull.Value,
                    SUT ?? (object)DBNull.Value,
                    NoOfOperator ?? (object)DBNull.Value,
                    QtyHour ?? (object)DBNull.Value,
                    ProdHeadHour ?? (object)DBNull.Value,
                    CycleTimeVacum ?? (object)DBNull.Value,
                    WorkHour ?? (object)DBNull.Value,
                    ProductId);

                TempData["StatusMessage"] = "success";
                TempData["Message"] = $"Product '{ProductName}' berhasil diupdate.";
            }
            catch (Exception ex)
            {
                TempData["StatusMessage"] = "error";
                TempData["Message"] = $"Gagal update: {ex.Message}";
            }
            return RedirectToPage(new { FilterMachineCode });
        }

        // ─── DELETE ────────────────────────────────────────────
        public async Task<IActionResult> OnPostDeleteAsync(
            string? FilterMachineCode,
            string? ProductId)
        {
            try
            {
                await _context.Database.ExecuteSqlRawAsync(
                    "DELETE FROM MasterData WHERE Product_Id = {0}", ProductId);

                TempData["StatusMessage"] = "success";
                TempData["Message"] = "Product berhasil dihapus.";
            }
            catch (Exception ex)
            {
                TempData["StatusMessage"] = "error";
                TempData["Message"] = $"Gagal hapus: {ex.Message}";
            }
            return RedirectToPage(new { FilterMachineCode });
        }
    }
}