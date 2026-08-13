using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Data.SqlClient;
using System.ComponentModel.DataAnnotations;

namespace MonitoringSystem.Pages.InputOEESN
{
    public class IndexModel : PageModel
    {
        private readonly IConfiguration _configuration;
        
        // Menggunakan koneksi default (sama seperti fitur Inventory2)
        private string ConnectionString => _configuration.GetConnectionString("DefaultConnection");

        public IndexModel(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        [BindProperty, Required] public long SnStart { get; set; }
        [BindProperty, Required] public long SnEnd { get; set; }
        [BindProperty] public string? ShiftMode { get; set; }
        [BindProperty] public double PTarget { get; set; }
        [BindProperty] public int TotalUnit { get; set; }
        [BindProperty] public double Performance { get; set; }
        [BindProperty] public string? ProductId { get; set; }
        [BindProperty] public int GoodUnit { get; set; }
        [BindProperty] public int TargetUnit { get; set; }
        [BindProperty] public string? MachineCode { get; set; }
        [BindProperty] public int CycleTime { get; set; }
        [BindProperty] public DateTime InputDate { get; set; } = DateTime.Now;

        public void OnGet()
        {
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (SnStart > SnEnd)
            {
                TempData["ErrorMessage"] = "Gagal: SN_GOOD Start tidak boleh lebih besar dari SN_GOOD End.";
                return Page();
            }

            try
            {
                using var connection = new SqlConnection(ConnectionString);
                await connection.OpenAsync();
                
                // Gunakan transaksi agar jika terjadi error di tengah, data tidak masuk setengah
                using var transaction = connection.BeginTransaction();
                string sql = @"
                    INSERT INTO PROMOSYS.dbo.OEESN 
                    (SDate, Date, ShiftMode, P_Target, TotalUnit, Performance, Product_Id, GoodUnit, TargetUnit, MachineCode, CycleTime, SN_GOOD) 
                    VALUES (@date, @date, @shiftMode, @pTarget, @totalUnit, @performance, @productId, @goodUnit, @targetUnit, @machineCode, @cycleTime, @snGood)";
                
                using var command = new SqlCommand(sql, connection, transaction);
                
                // Memasukkan Parameter Statis
                command.Parameters.AddWithValue("@date", InputDate);
                command.Parameters.AddWithValue("@shiftMode", string.IsNullOrEmpty(ShiftMode) ? DBNull.Value : ShiftMode);
                command.Parameters.AddWithValue("@pTarget", PTarget);
                command.Parameters.AddWithValue("@totalUnit", TotalUnit);
                command.Parameters.AddWithValue("@performance", Performance);
                command.Parameters.AddWithValue("@productId", string.IsNullOrEmpty(ProductId) ? DBNull.Value : ProductId);
                command.Parameters.AddWithValue("@goodUnit", GoodUnit);
                command.Parameters.AddWithValue("@targetUnit", TargetUnit);
                command.Parameters.AddWithValue("@machineCode", string.IsNullOrEmpty(MachineCode) ? DBNull.Value : MachineCode);
                command.Parameters.AddWithValue("@cycleTime", CycleTime);
                
                // Parameter Dinamis untuk SN
                var snParam = command.Parameters.Add("@snGood", System.Data.SqlDbType.BigInt);

                int count = 0;
                // Looping dari Serial Awal ke Serial Akhir
                for (long sn = SnStart; sn <= SnEnd; sn++)
                {
                    snParam.Value = sn;
                    await command.ExecuteNonQueryAsync();
                    count++;
                }
                
                transaction.Commit();
                TempData["SuccessMessage"] = $"Berhasil! {count} data berhasil dimasukkan ke tabel OEESN (SN: {SnStart} s/d {SnEnd}).";
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"Database Error: {ex.Message}";
            }

            return Page();
        }
    }
}
