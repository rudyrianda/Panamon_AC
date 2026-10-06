using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using MonitoringSystem.Models;

namespace MonitoringSystem.Pages.CAC.Shared
{
    public class SutModel : PageModel
    {
        private readonly ScaffoldedDbContext _context;

        public SutModel(ScaffoldedDbContext context)
        {
            _context = context;
        }

        [BindProperty(SupportsGet = true)]
        public string? FilterMachineCode { get; set; } = "CAC";

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
            string? OriginalProductName,
            string? OriginalMachineCode,
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

                await using var transaction = await _context.Database.BeginTransactionAsync();
                int affectedRows;

                if (!string.IsNullOrWhiteSpace(ProductId))
                {
                    affectedRows = await _context.Database.ExecuteSqlInterpolatedAsync($@"
                        UPDATE MasterData SET
                            ProductName    = {ProductName},
                            MachineCode    = {MachineCode},
                            Description    = {Description},
                            ProdPlan       = {ProdPlan},
                            SUT            = {SUT},
                            NoOfOperator   = {NoOfOperator},
                            QtyHour        = {QtyHour},
                            ProdHeadHour   = {ProdHeadHour},
                            CycleTimeVacum = {CycleTimeVacum},
                            WorkHour       = {WorkHour}
                        WHERE Product_Id = {ProductId}");
                }
                else
                {
                    if (string.IsNullOrWhiteSpace(OriginalProductName))
                    {
                        await transaction.RollbackAsync();
                        TempData["StatusMessage"] = "error";
                        TempData["Message"] = "Data tidak memiliki identitas yang valid dan tidak dapat diupdate.";
                        return RedirectToPage(new { FilterMachineCode });
                    }

                    // Data lama tanpa Product_Id diidentifikasi memakai nilai asli yang
                    // tidak ikut berubah saat user mengedit modal.
                    affectedRows = await _context.Database.ExecuteSqlInterpolatedAsync($@"
                        UPDATE MasterData SET
                            ProductName    = {ProductName},
                            MachineCode    = {MachineCode},
                            Description    = {Description},
                            ProdPlan       = {ProdPlan},
                            SUT            = {SUT},
                            NoOfOperator   = {NoOfOperator},
                            QtyHour        = {QtyHour},
                            ProdHeadHour   = {ProdHeadHour},
                            CycleTimeVacum = {CycleTimeVacum},
                            WorkHour       = {WorkHour}
                        WHERE (Product_Id IS NULL OR LTRIM(RTRIM(Product_Id)) = '')
                          AND ProductName = {OriginalProductName}
                          AND ((MachineCode = {OriginalMachineCode})
                               OR (MachineCode IS NULL AND {OriginalMachineCode} IS NULL))");
                }

                if (affectedRows != 1)
                {
                    await transaction.RollbackAsync();
                    TempData["StatusMessage"] = "error";
                    TempData["Message"] = affectedRows == 0
                        ? "Data tidak ditemukan. Tidak ada perubahan yang disimpan."
                        : "Ditemukan lebih dari satu data yang sama. Perubahan dibatalkan untuk menjaga data SUT.";
                    return RedirectToPage(new { FilterMachineCode });
                }

                await transaction.CommitAsync();

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
