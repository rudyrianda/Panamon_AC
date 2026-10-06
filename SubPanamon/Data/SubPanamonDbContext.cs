using Microsoft.EntityFrameworkCore;

namespace SubPanamon.Data;

// Hanya dipakai untuk koneksi SQL mentah (Database.GetDbConnection) di halaman PWK.
public class SubPanamonDbContext : DbContext
{
    public SubPanamonDbContext(DbContextOptions<SubPanamonDbContext> options) : base(options)
    {
    }
}
