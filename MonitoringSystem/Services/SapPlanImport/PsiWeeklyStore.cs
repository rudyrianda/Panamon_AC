using System.Data;
using Microsoft.Data.SqlClient;

namespace MonitoringSystem.Services.SapPlanImport
{
    /// <summary>
    /// Tabel dbo.PsiWeeklyPlan (dibuat dengan database_psi_weekly_plan.sql): list mingguan per mesin.
    /// Satu import mengganti seluruh minggu dalam satu bulan + satu mesin dalam satu transaksi.
    /// </summary>
    public static class PsiWeeklyStore
    {
        public const string Table = "PsiWeeklyPlan";

        public static bool IsMissingTable(SqlException ex) => ex.Number == 208;

        /// <summary>Nama model di Master Data Produk PLC ROHIB (dbo.MasterProduct). Hanya model ini yang boleh masuk list.</summary>
        public static async Task<List<string>> LoadMasterProductModelsAsync(string connStr, CancellationToken ct)
        {
            var list = new List<string>();
            await using var conn = new SqlConnection(connStr);
            await conn.OpenAsync(ct);
            await using var cmd = new SqlCommand("SELECT DISTINCT LTRIM(RTRIM([Model])) FROM dbo.MasterProduct WHERE [Model] IS NOT NULL AND LTRIM(RTRIM([Model])) <> '';", conn);
            try
            {
                await using var rd = await cmd.ExecuteReaderAsync(ct);
                while (await rd.ReadAsync(ct)) list.Add(rd.GetString(0));
            }
            catch (SqlException ex) when (IsMissingTable(ex))
            {
                throw new InvalidOperationException("Tabel dbo.MasterProduct (Master Data Produk PLC ROHIB) tidak ada di database.");
            }
            if (list.Count == 0) throw new InvalidOperationException("Master Data Produk (dbo.MasterProduct) kosong; list mingguan tidak dibuat.");
            return list;
        }

        public static async Task<(int Rows, int Qty)> CountMonthAsync(string connStr, DateTime month, string machine, CancellationToken ct)
        {
            await using var conn = new SqlConnection(connStr);
            await conn.OpenAsync(ct);
            await using var cmd = new SqlCommand(
                $"SELECT COUNT(*), ISNULL(SUM(Qty), 0) FROM dbo.{Table} WHERE MachineCode = @mc AND WeekStart >= @from AND WeekStart < @to;", conn);
            AddParams(cmd, month, machine);
            await using var rd = await cmd.ExecuteReaderAsync(ct);
            await rd.ReadAsync(ct);
            return (rd.GetInt32(0), rd.GetInt32(1));
        }

        public static async Task ReplaceMonthAsync(string connStr, DateTime month, string machine, List<PsiWeeklyRow> rows, string sourceHash, CancellationToken ct)
        {
            await using var conn = new SqlConnection(connStr);
            await conn.OpenAsync(ct);
            await using var tx = (SqlTransaction)await conn.BeginTransactionAsync(IsolationLevel.Serializable, ct);
            try
            {
                await using (var del = new SqlCommand($"DELETE FROM dbo.{Table} WHERE MachineCode = @mc AND WeekStart >= @from AND WeekStart < @to;", conn, tx))
                {
                    AddParams(del, month, machine);
                    await del.ExecuteNonQueryAsync(ct);
                }

                foreach (var r in rows)
                {
                    await using var ins = new SqlCommand($@"
                        INSERT INTO dbo.{Table} (MachineCode, WeekStart, WeekEnd, PlanDate, SeqInWeek, SeqInDay, ProductName, Qty,
                                                 SourceDates, IsMerged, MergeReason, PriorityCategory, PriorityRank, SourceFileHash)
                        VALUES (@mc, @ws, @we, @pd, @sw, @sd, @product, @qty, @src, @merged, @reason, @cat, @rank, @hash);", conn, tx);
                    ins.Parameters.Add("@mc", SqlDbType.NVarChar, 20).Value = machine;
                    ins.Parameters.Add("@ws", SqlDbType.Date).Value = r.WeekStart;
                    ins.Parameters.Add("@we", SqlDbType.Date).Value = r.WeekEnd;
                    ins.Parameters.Add("@pd", SqlDbType.Date).Value = r.PlanDate;
                    ins.Parameters.Add("@sw", SqlDbType.Int).Value = r.SeqInWeek;
                    ins.Parameters.Add("@sd", SqlDbType.Int).Value = r.SeqInDay;
                    ins.Parameters.Add("@product", SqlDbType.NVarChar, 100).Value = r.Model;
                    ins.Parameters.Add("@qty", SqlDbType.Int).Value = r.Qty;
                    ins.Parameters.Add("@src", SqlDbType.NVarChar, 400).Value = string.Join(",", r.SourceDates.Select(d => d.ToString("yyyy-MM-dd")));
                    ins.Parameters.Add("@merged", SqlDbType.Bit).Value = r.IsMerged;
                    ins.Parameters.Add("@reason", SqlDbType.NVarChar, 50).Value = (object?)r.MergeReason ?? DBNull.Value;
                    ins.Parameters.Add("@cat", SqlDbType.NVarChar, 20).Value = r.Category;
                    ins.Parameters.Add("@rank", SqlDbType.Int).Value = r.PriorityRank;
                    ins.Parameters.Add("@hash", SqlDbType.Char, 64).Value = sourceHash;
                    await ins.ExecuteNonQueryAsync(ct);
                }

                // Verifikasi isi database = hasil hitung sebelum commit
                await using (var chk = new SqlCommand(
                    $"SELECT COUNT(*), ISNULL(SUM(Qty), 0) FROM dbo.{Table} WHERE MachineCode = @mc AND WeekStart >= @from AND WeekStart < @to;", conn, tx))
                {
                    AddParams(chk, month, machine);
                    await using var rd = await chk.ExecuteReaderAsync(ct);
                    await rd.ReadAsync(ct);
                    if (rd.GetInt32(0) != rows.Count || rd.GetInt32(1) != rows.Sum(r => r.Qty))
                        throw new InvalidOperationException($"Verifikasi list mingguan gagal: database {rd.GetInt32(0)} baris/{rd.GetInt32(1)}, hasil hitung {rows.Count}/{rows.Sum(r => r.Qty)}.");
                }

                await tx.CommitAsync(ct);
            }
            catch
            {
                await tx.RollbackAsync(CancellationToken.None);
                throw;
            }
        }

        private static void AddParams(SqlCommand cmd, DateTime month, string machine)
        {
            cmd.Parameters.Add("@from", SqlDbType.Date).Value = month;
            cmd.Parameters.Add("@to", SqlDbType.Date).Value = month.AddMonths(1);
            cmd.Parameters.Add("@mc", SqlDbType.NVarChar, 20).Value = machine;
        }
    }
}
