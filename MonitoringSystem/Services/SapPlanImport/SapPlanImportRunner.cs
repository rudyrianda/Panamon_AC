using System.Data;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;

namespace MonitoringSystem.Services.SapPlanImport
{
    public class SapPlanUnitResult
    {
        public string Month { get; set; } = string.Empty;          // yyyy-MM
        public string Sheet { get; set; } = string.Empty;
        public string MachineCode { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;         // DryRun | Committed | Skipped | Failed
        public string? Message { get; set; }
        public int Records { get; set; }
        public int QtySource { get; set; }
        public int Normal { get; set; }
        public int Overtime { get; set; }
        public int DbRecordsBefore { get; set; }
        public int DbQtyBefore { get; set; }
        public int DbNormalBefore { get; set; }
        public int DbOvertimeBefore { get; set; }
        public int RowsSame { get; set; }
        public int RowsChanged { get; set; }
        public int RowsNew { get; set; }
        public int RowsRemoved { get; set; }
        public List<string> AliasesUsed { get; set; } = new();
        public List<string> Errors { get; set; } = new();
        public List<SapPlanDaySummary> Days { get; set; } = new();
    }

    public class SapPlanCycleResult
    {
        public DateTime StartedAt { get; set; }
        public DateTime FinishedAt { get; set; }
        public bool DryRun { get; set; }
        public string SourcePath { get; set; } = string.Empty;
        public string? FileHash { get; set; }
        public DateTime? FileLastWriteTime { get; set; }
        public long FileSize { get; set; }
        public string? CycleError { get; set; }
        public List<SapPlanUnitResult> Units { get; set; } = new();
    }

    internal class SapPlanUnitState
    {
        public string Hash { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public DateTime At { get; set; }
        public int Records { get; set; }
        public int QtySource { get; set; }
    }

    /// <summary>
    /// Satu siklus import SAP Plan: snapshot file (read-only) -> baca sheet bulan horizon -> hitung Normal/OVT ->
    /// validasi -> dry-run (file saja) atau commit per bulan+mesin dalam satu transaksi SQL.
    /// Hanya SapPlan Shift NS bulan & mesin yang diproses yang diganti. ProductionPlan tidak pernah dihapus.
    /// </summary>
    public class SapPlanImportRunner
    {
        private readonly IOptionsMonitor<SapPlanImportOptions> _options;
        private readonly IConfiguration _configuration;
        private readonly IHostEnvironment _env;
        private readonly ILogger<SapPlanImportRunner> _logger;
        private readonly SemaphoreSlim _gate = new(1, 1);

        private static readonly JsonSerializerOptions JsonOpts = new() { WriteIndented = true };
        private static readonly JsonSerializerOptions JsonLine = new() { WriteIndented = false };

        public SapPlanImportRunner(IOptionsMonitor<SapPlanImportOptions> options, IConfiguration configuration,
            IHostEnvironment env, ILogger<SapPlanImportRunner> logger)
        {
            _options = options;
            _configuration = configuration;
            _env = env;
            _logger = logger;
        }

        public static TimeZoneInfo ResolveTimeZone(string id)
        {
            try { return TimeZoneInfo.FindSystemTimeZoneById(id); }
            catch (TimeZoneNotFoundException) { return TimeZoneInfo.FindSystemTimeZoneById("SE Asia Standard Time"); }
        }

        public async Task<SapPlanCycleResult> RunOnceAsync(bool? dryRunOverride, CancellationToken ct)
        {
            if (!await _gate.WaitAsync(0, ct))
                return new SapPlanCycleResult { StartedAt = DateTime.Now, FinishedAt = DateTime.Now, CycleError = "Siklus sebelumnya masih berjalan." };
            try { return await RunCoreAsync(dryRunOverride, ct); }
            finally { _gate.Release(); }
        }

        private async Task<SapPlanCycleResult> RunCoreAsync(bool? dryRunOverride, CancellationToken ct)
        {
            var opt = _options.CurrentValue;
            var tz = ResolveTimeZone(opt.TimeZone);
            var nowLocal = TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, tz).DateTime;
            var cycle = new SapPlanCycleResult { StartedAt = nowLocal, DryRun = dryRunOverride ?? opt.DryRun, SourcePath = opt.SourcePath };
            var stateFolder = Path.Combine(_env.ContentRootPath, opt.StateFolder);
            string? snapshot = null;

            try
            {
                Directory.CreateDirectory(stateFolder);
                if (string.IsNullOrWhiteSpace(opt.SourcePath)) throw new InvalidOperationException("SapPlanImport:SourcePath belum diisi.");
                if (!opt.SourcePath.StartsWith(@"\\"))
                    _logger.LogWarning("SapPlanImport: SourcePath bukan UNC path ({Path}). Drive mapped biasanya tidak terlihat oleh akun service/IIS.", opt.SourcePath);

                // 1. File harus stabil (ukuran & waktu modifikasi sama pada dua pengecekan)
                var fi = new FileInfo(opt.SourcePath);
                if (!fi.Exists) throw new FileNotFoundException("File sumber tidak ditemukan / tidak bisa diakses.", opt.SourcePath);
                var (len1, mod1) = (fi.Length, fi.LastWriteTimeUtc);
                await Task.Delay(TimeSpan.FromSeconds(Math.Max(0, opt.StableCheckSeconds)), ct);
                fi.Refresh();
                if (fi.Length != len1 || fi.LastWriteTimeUtc != mod1) throw new IOException("File masih berubah (sedang disimpan). Dicoba lagi pada jadwal berikutnya.");

                // 2. Snapshot lokal (sumber hanya dibaca, boleh sedang dibuka orang lain)
                var tempDir = Path.Combine(Path.GetTempPath(), "panamon-sapplan");
                Directory.CreateDirectory(tempDir);
                snapshot = Path.Combine(tempDir, Guid.NewGuid().ToString("N") + ".xlsx");
                await using (var src = new FileStream(opt.SourcePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
                await using (var dst = new FileStream(snapshot, FileMode.CreateNew, FileAccess.Write))
                    await src.CopyToAsync(dst, ct);
                fi.Refresh();
                if (fi.Length != len1 || fi.LastWriteTimeUtc != mod1) throw new IOException("File berubah selama disalin. Dicoba lagi pada jadwal berikutnya.");
                cycle.FileSize = len1;
                cycle.FileLastWriteTime = TimeZoneInfo.ConvertTimeFromUtc(mod1, tz);
                await using (var hs = File.OpenRead(snapshot))
                    cycle.FileHash = Convert.ToHexString(await SHA256.HashDataAsync(hs, ct));

                // 3. Horizon bulan: bulan berjalan + MonthsAhead (+ ExtraMonths eksplisit)
                var thisMonth = new DateTime(nowLocal.Year, nowLocal.Month, 1);
                var months = Enumerable.Range(0, Math.Max(0, opt.MonthsAhead) + 1).Select(i => thisMonth.AddMonths(i)).ToList();
                foreach (var m in opt.ExtraMonths)
                    if (DateTime.TryParseExact(m, "yyyy-MM", CultureInfo.InvariantCulture, DateTimeStyles.None, out var em) && !months.Contains(em))
                        months.Add(em);
                months.Sort();

                // 4. Alias & SUT MasterData
                var aliases = LoadAliases(Path.Combine(_env.ContentRootPath, opt.AliasFile));
                var connStr = _configuration.GetConnectionString("DefaultConnection")
                    ?? throw new InvalidOperationException("ConnectionStrings:DefaultConnection tidak ada.");
                var sut = await LoadMasterDataSutAsync(connStr, new[] { opt.CuMachineCode, opt.CsMachineCode }, ct);
                var calculator = new SapPlanCalculator(sut, aliases);
                var state = LoadState(stateFolder);

                foreach (var month in months)
                {
                    var parsed = DailyPlanWorkbookReader.Parse(snapshot, month, opt.CuMachineCode, opt.CsMachineCode);
                    foreach (var machine in new[] { opt.CuMachineCode, opt.CsMachineCode })
                    {
                        ct.ThrowIfCancellationRequested();
                        var unit = await ProcessUnitAsync(opt, connStr, calculator, parsed, month, machine, cycle.DryRun, cycle.FileHash!, state, stateFolder, ct);
                        cycle.Units.Add(unit);
                        AppendAudit(stateFolder, cycle, unit);
                    }
                }
                SaveState(stateFolder, state);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
            catch (Exception ex)
            {
                cycle.CycleError = ex.Message;
                _logger.LogError(ex, "SapPlanImport: siklus gagal - tidak ada data yang diubah untuk bagian yang belum diproses.");
                AppendAudit(stateFolder, cycle, null);
            }
            finally
            {
                if (snapshot != null) { try { File.Delete(snapshot); } catch { /* snapshot sementara */ } }
                cycle.FinishedAt = TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, tz).DateTime;
                try { File.WriteAllText(Path.Combine(stateFolder, "last-cycle.json"), JsonSerializer.Serialize(cycle, JsonOpts)); } catch { }
            }

            foreach (var u in cycle.Units)
                _logger.LogInformation("SapPlanImport {Month} {Machine}: {Status} - {Records} record, Qty {Qty} = Normal {Normal} + OVT {Ovt}{Err}",
                    u.Month, u.MachineCode, u.Status, u.Records, u.QtySource, u.Normal, u.Overtime,
                    u.Errors.Count > 0 ? $" | {u.Errors.Count} error, contoh: {u.Errors[0]}" : "");
            return cycle;
        }

        private async Task<SapPlanUnitResult> ProcessUnitAsync(SapPlanImportOptions opt, string connStr, SapPlanCalculator calculator,
            ParsedPlanSheet parsed, DateTime month, string machine, bool dryRun, string hash,
            Dictionary<string, SapPlanUnitState> state, string stateFolder, CancellationToken ct)
        {
            var unit = new SapPlanUnitResult { Month = month.ToString("yyyy-MM"), Sheet = parsed.SheetName, MachineCode = machine };
            var stateKey = $"{unit.Month}|{machine}";
            try
            {
                unit.Errors.AddRange(parsed.ErrorsFor(machine));
                var cells = parsed.Cells.Where(c => c.MachineCode == machine).ToList();

                if (!dryRun && state.TryGetValue(stateKey, out var st) && st.Status == "Committed" && st.Hash == hash)
                {
                    unit.Status = "Skipped";
                    unit.Message = "File tidak berubah sejak commit terakhir.";
                    return unit;
                }

                var (rows, days, calcErrors) = calculator.Calculate(cells, machine, opt.CapacitySeconds);
                unit.Errors.AddRange(calcErrors);
                unit.Days = days;
                unit.Records = rows.Count;
                unit.QtySource = rows.Sum(r => r.Qty);
                unit.Normal = rows.Sum(r => r.Normal);
                unit.Overtime = rows.Sum(r => r.Overtime);
                unit.AliasesUsed = rows.Where(r => r.Mapping.StartsWith("alias")).Select(r => $"{r.Model} {r.Mapping}").Distinct().OrderBy(x => x).ToList();

                // Validasi staging
                if (unit.Errors.Count == 0 && cells.Sum(c => c.Qty) != unit.QtySource) unit.Errors.Add("Total Qty hasil hitung tidak sama dengan total Qty sumber.");
                if (unit.Normal + unit.Overtime != unit.QtySource) unit.Errors.Add("Total Normal + OVT tidak sama dengan total Qty sumber.");
                foreach (var dup in rows.GroupBy(r => (r.Date, Name: r.Model.ToUpperInvariant())).Where(g => g.Count() > 1))
                    unit.Errors.Add($"Duplikat key {dup.Key.Date:yyyy-MM-dd} {dup.Key.Name}.");
                foreach (var r in rows.Where(r => r.Date.Year != month.Year || r.Date.Month != month.Month))
                    unit.Errors.Add($"Tanggal {r.Date:yyyy-MM-dd} di luar bulan {unit.Month}.");

                // Data SAP Plan sekarang (hanya baca) untuk perbandingan
                var before = await LoadExistingAsync(connStr, month, machine, opt.Shift, ct);
                unit.DbRecordsBefore = before.Rows.Count;
                unit.DbQtyBefore = before.Rows.Sum(r => r.Normal + r.Overtime);
                unit.DbNormalBefore = before.Rows.Sum(r => r.Normal);
                unit.DbOvertimeBefore = before.Rows.Sum(r => r.Overtime);
                if (before.NullShiftRows > 0)
                    unit.Errors.Add($"Ada {before.NullShiftRows} record SapPlan dengan Shift kosong (NULL) di bulan/mesin ini. Rapikan dulu, worker tidak menghapus/menebak shift-nya.");
                if (before.DuplicatePlanDates.Count > 0)
                    unit.Errors.Add("ProductionPlan punya tanggal dobel: " + string.Join(", ", before.DuplicatePlanDates) + ". Rapikan dulu sebelum import.");
                CompareWithExisting(unit, rows, before.Rows);

                if (!parsed.SheetFound || cells.Count == 0)
                {
                    if (unit.Errors.Count == 0 && before.Rows.Count == 0) { unit.Status = "Skipped"; unit.Message = "Tidak ada rencana di file maupun di database."; return unit; }
                    if (cells.Count == 0 && parsed.SheetFound) unit.Errors.Add("File tidak berisi quantity untuk mesin ini, sedangkan database berisi data. Tidak dihapus demi keamanan.");
                }

                WriteDetailCsv(stateFolder, unit, rows, dryRun);

                if (unit.Errors.Count > 0)
                {
                    unit.Status = "Failed";
                    unit.Message = "Tidak ada perubahan database untuk bulan/mesin ini.";
                    state[stateKey] = new SapPlanUnitState { Hash = hash, Status = "Failed", At = DateTime.Now, Records = unit.Records, QtySource = unit.QtySource };
                    return unit;
                }

                if (dryRun)
                {
                    unit.Status = "DryRun";
                    unit.Message = "Dry-run: database tidak diubah.";
                    return unit;
                }

                await CommitAsync(connStr, month, machine, opt.Shift, rows, ct);
                unit.Status = "Committed";
                unit.Message = $"SAP Plan {unit.Month} {machine} Shift {opt.Shift} diganti ({unit.Records} record).";
                state[stateKey] = new SapPlanUnitState { Hash = hash, Status = "Committed", At = DateTime.Now, Records = unit.Records, QtySource = unit.QtySource };
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
            catch (Exception ex)
            {
                unit.Status = "Failed";
                unit.Errors.Add(ex.Message);
                unit.Message = "Transaksi dibatalkan (rollback). Tidak ada perubahan database untuk bulan/mesin ini.";
                _logger.LogError(ex, "SapPlanImport {Month} {Machine} gagal", unit.Month, machine);
            }
            return unit;
        }

        private static void CompareWithExisting(SapPlanUnitResult unit, List<SapPlanRow> rows, List<ExistingRow> existing)
        {
            var now = rows.ToDictionary(r => (r.Date, r.Model.Trim().ToUpperInvariant()), r => (r.Normal, r.Overtime));
            var old = new Dictionary<(DateTime, string), (int, int)>();
            foreach (var e in existing)
            {
                var k = (e.Date, e.Product.Trim().ToUpperInvariant());
                old[k] = old.TryGetValue(k, out var v) ? (v.Item1 + e.Normal, v.Item2 + e.Overtime) : (e.Normal, e.Overtime);
            }
            foreach (var (k, v) in now)
            {
                if (!old.TryGetValue(k, out var o)) unit.RowsNew++;
                else if (o == v) unit.RowsSame++;
                else unit.RowsChanged++;
            }
            unit.RowsRemoved = old.Keys.Count(k => !now.ContainsKey(k));
        }

        // ── Database ────────────────────────────────────────────────────────────

        private static async Task<Dictionary<string, int>> LoadMasterDataSutAsync(string connStr, string[] machines, CancellationToken ct)
        {
            var map = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            await using var conn = new SqlConnection(connStr);
            await conn.OpenAsync(ct);
            await using var cmd = new SqlCommand(
                "SELECT LTRIM(RTRIM(ProductName)), LTRIM(RTRIM(MachineCode)), SUT FROM MasterData WHERE LTRIM(RTRIM(MachineCode)) IN (@m1, @m2) AND ProductName IS NOT NULL", conn);
            cmd.Parameters.Add("@m1", SqlDbType.VarChar, 50).Value = machines[0];
            cmd.Parameters.Add("@m2", SqlDbType.VarChar, 50).Value = machines[1];
            await using var rd = await cmd.ExecuteReaderAsync(ct);
            while (await rd.ReadAsync(ct))
            {
                var key = SapPlanCalculator.Key(rd.GetString(0), rd.GetString(1));
                var sut = rd.IsDBNull(2) ? 0 : rd.GetInt32(2);
                // Duplikat dengan SUT sama = aman; SUT berbeda = ambigu (-1)
                map[key] = map.TryGetValue(key, out var prev) && prev != sut ? -1 : sut;
            }
            return map;
        }

        private record ExistingRow(DateTime Date, string Product, int Normal, int Overtime);
        private record ExistingData(List<ExistingRow> Rows, int NullShiftRows, List<string> DuplicatePlanDates);

        private static async Task<ExistingData> LoadExistingAsync(string connStr, DateTime month, string machine, string shift, CancellationToken ct)
        {
            var rows = new List<ExistingRow>();
            var nullShift = 0;
            var dupDates = new List<string>();
            await using var conn = new SqlConnection(connStr);
            await conn.OpenAsync(ct);
            await using (var cmd = new SqlCommand(@"
                SELECT PP.CurrentDate, SP.ProductName, SP.SapPlanNormal, ISNULL(SP.SapPlanOvertime, 0), SP.Shift
                FROM SapPlan SP INNER JOIN ProductionPlan PP ON SP.PlanId = PP.Id
                WHERE PP.CurrentDate >= @from AND PP.CurrentDate < @to AND SP.MachineCode = @mc
                  AND (SP.Shift = @shift OR SP.Shift IS NULL);", conn))
            {
                AddMonthParams(cmd, month, machine, shift);
                await using var rd = await cmd.ExecuteReaderAsync(ct);
                while (await rd.ReadAsync(ct))
                {
                    if (rd.IsDBNull(4)) { nullShift++; continue; }
                    rows.Add(new ExistingRow(rd.GetDateTime(0).Date, rd.IsDBNull(1) ? "" : rd.GetString(1), rd.GetInt32(2), rd.GetInt32(3)));
                }
            }
            await using (var dup = new SqlCommand(@"
                SELECT CurrentDate FROM ProductionPlan WHERE CurrentDate >= @from AND CurrentDate < @to
                GROUP BY CurrentDate HAVING COUNT(*) > 1;", conn))
            {
                AddMonthParams(dup, month, machine, shift);
                await using var rd = await dup.ExecuteReaderAsync(ct);
                while (await rd.ReadAsync(ct)) dupDates.Add(rd.GetDateTime(0).ToString("yyyy-MM-dd"));
            }
            return new ExistingData(rows, nullShift, dupDates);
        }

        private static void AddMonthParams(SqlCommand cmd, DateTime month, string machine, string shift)
        {
            cmd.Parameters.Add("@from", SqlDbType.Date).Value = month;
            cmd.Parameters.Add("@to", SqlDbType.Date).Value = month.AddMonths(1);
            cmd.Parameters.Add("@mc", SqlDbType.NVarChar, 100).Value = machine;
            cmd.Parameters.Add("@shift", SqlDbType.NVarChar, 20).Value = shift;
        }

        /// <summary>Ganti SapPlan Shift NS satu bulan + satu mesin dalam satu transaksi. Gagal di langkah mana pun = rollback.</summary>
        private static async Task CommitAsync(string connStr, DateTime month, string machine, string shift, List<SapPlanRow> rows, CancellationToken ct)
        {
            await using var conn = new SqlConnection(connStr);
            await conn.OpenAsync(ct);
            await using var tx = (SqlTransaction)await conn.BeginTransactionAsync(IsolationLevel.Serializable, ct);
            try
            {
                // 1. ProductionPlan per tanggal. Tanggal dobel = berhenti (tidak memilih record secara acak).
                var planIds = new Dictionary<DateTime, int>();
                var duplicates = new List<string>();
                await using (var cmd = new SqlCommand(
                    "SELECT Id, CurrentDate FROM ProductionPlan WITH (UPDLOCK, HOLDLOCK) WHERE CurrentDate >= @from AND CurrentDate < @to;", conn, tx))
                {
                    AddMonthParams(cmd, month, machine, shift);
                    await using var rd = await cmd.ExecuteReaderAsync(ct);
                    while (await rd.ReadAsync(ct))
                    {
                        var d = rd.GetDateTime(1).Date;
                        if (planIds.ContainsKey(d)) duplicates.Add(d.ToString("yyyy-MM-dd"));
                        else planIds[d] = rd.GetInt32(0);
                    }
                }
                if (duplicates.Count > 0)
                    throw new InvalidOperationException("ProductionPlan punya tanggal dobel: " + string.Join(", ", duplicates.Distinct()) + ". Rapikan dulu sebelum import.");

                foreach (var date in rows.Select(r => r.Date).Distinct().Where(d => !planIds.ContainsKey(d)))
                {
                    await using var ins = new SqlCommand("INSERT INTO ProductionPlan (CurrentDate) OUTPUT INSERTED.Id VALUES (@d);", conn, tx);
                    ins.Parameters.Add("@d", SqlDbType.Date).Value = date;
                    planIds[date] = (int)(await ins.ExecuteScalarAsync(ct))!;
                }

                // 2. Hapus hanya SapPlan Shift NS untuk bulan & mesin ini
                await using (var del = new SqlCommand(@"
                    DELETE SP FROM SapPlan SP INNER JOIN ProductionPlan PP ON SP.PlanId = PP.Id
                    WHERE PP.CurrentDate >= @from AND PP.CurrentDate < @to AND SP.MachineCode = @mc AND SP.Shift = @shift;", conn, tx))
                {
                    AddMonthParams(del, month, machine, shift);
                    await del.ExecuteNonQueryAsync(ct);
                }

                // 3. Insert hasil hitung
                foreach (var r in rows)
                {
                    await using var ins = new SqlCommand(@"
                        INSERT INTO SapPlan (PlanId, MachineCode, SapPlanNormal, SapPlanOvertime, CreatedAt, ProductName, Shift)
                        VALUES (@plan, @mc, @normal, @ovt, GETDATE(), @product, @shift);", conn, tx);
                    ins.Parameters.Add("@plan", SqlDbType.Int).Value = planIds[r.Date];
                    ins.Parameters.Add("@mc", SqlDbType.NVarChar, 100).Value = machine;
                    ins.Parameters.Add("@normal", SqlDbType.Int).Value = r.Normal;
                    ins.Parameters.Add("@ovt", SqlDbType.Int).Value = r.Overtime;
                    ins.Parameters.Add("@product", SqlDbType.NVarChar, 400).Value = r.Model;
                    ins.Parameters.Add("@shift", SqlDbType.NVarChar, 20).Value = shift;
                    await ins.ExecuteNonQueryAsync(ct);
                }

                // 4. Verifikasi isi database = hasil hitung sebelum commit
                await using (var chk = new SqlCommand(@"
                    SELECT COUNT(*), ISNULL(SUM(SP.SapPlanNormal), 0), ISNULL(SUM(ISNULL(SP.SapPlanOvertime, 0)), 0)
                    FROM SapPlan SP INNER JOIN ProductionPlan PP ON SP.PlanId = PP.Id
                    WHERE PP.CurrentDate >= @from AND PP.CurrentDate < @to AND SP.MachineCode = @mc AND SP.Shift = @shift;", conn, tx))
                {
                    AddMonthParams(chk, month, machine, shift);
                    await using var rd = await chk.ExecuteReaderAsync(ct);
                    await rd.ReadAsync(ct);
                    var (count, normal, ovt) = (rd.GetInt32(0), rd.GetInt32(1), rd.GetInt32(2));
                    if (count != rows.Count || normal != rows.Sum(r => r.Normal) || ovt != rows.Sum(r => r.Overtime))
                        throw new InvalidOperationException($"Verifikasi gagal: database {count} record/{normal}/{ovt}, hasil hitung {rows.Count}/{rows.Sum(r => r.Normal)}/{rows.Sum(r => r.Overtime)}.");
                }

                await tx.CommitAsync(ct);
            }
            catch
            {
                await tx.RollbackAsync(CancellationToken.None);
                throw;
            }
        }

        // ── File: alias, state, audit, hasil dry-run ─────────────────────────────

        private static List<SapPlanAlias> LoadAliases(string path)
        {
            if (!File.Exists(path)) throw new FileNotFoundException("File alias SAP Plan tidak ditemukan.", path);
            using var doc = JsonDocument.Parse(File.ReadAllText(path));
            var list = new List<SapPlanAlias>();
            foreach (var a in doc.RootElement.GetProperty("Aliases").EnumerateArray())
                list.Add(new SapPlanAlias(a.GetProperty("ExcelName").GetString()!, a.GetProperty("MachineCode").GetString()!,
                    a.GetProperty("MasterDataName").GetString()!, a.TryGetProperty("Note", out var n) ? n.GetString() : null));
            return list;
        }

        private static Dictionary<string, SapPlanUnitState> LoadState(string folder)
        {
            var path = Path.Combine(folder, "state.json");
            if (!File.Exists(path)) return new();
            try { return JsonSerializer.Deserialize<Dictionary<string, SapPlanUnitState>>(File.ReadAllText(path)) ?? new(); }
            catch { return new(); }
        }

        private static void SaveState(string folder, Dictionary<string, SapPlanUnitState> state) =>
            File.WriteAllText(Path.Combine(folder, "state.json"), JsonSerializer.Serialize(state, JsonOpts));

        private void AppendAudit(string folder, SapPlanCycleResult cycle, SapPlanUnitResult? unit)
        {
            try
            {
                var entry = new
                {
                    At = DateTime.Now,
                    cycle.DryRun,
                    File = Path.GetFileName(cycle.SourcePath),
                    cycle.FileHash,
                    cycle.FileLastWriteTime,
                    cycle.CycleError,
                    Unit = unit == null ? null : new
                    {
                        unit.Month, unit.Sheet, unit.MachineCode, unit.Status, unit.Message, unit.Records, unit.QtySource, unit.Normal, unit.Overtime,
                        unit.DbRecordsBefore, unit.DbQtyBefore, unit.RowsSame, unit.RowsChanged, unit.RowsNew, unit.RowsRemoved,
                        unit.AliasesUsed, unit.Errors
                    }
                };
                File.AppendAllText(Path.Combine(folder, $"audit-{DateTime.Now:yyyy-MM}.log"), JsonSerializer.Serialize(entry, JsonLine) + Environment.NewLine);
            }
            catch (Exception ex) { _logger.LogWarning(ex, "SapPlanImport: gagal menulis audit log."); }
        }

        private static void WriteDetailCsv(string folder, SapPlanUnitResult unit, List<SapPlanRow> rows, bool dryRun)
        {
            var dir = Path.Combine(folder, dryRun ? "dryrun" : "import");
            Directory.CreateDirectory(dir);
            var sb = new StringBuilder("Tanggal,Mesin,UrutanExcel,BarisExcel,Model,QtySumber,SUT,Detik,Normal,OVT,Mapping\n");
            foreach (var r in rows.OrderBy(r => r.Date).ThenBy(r => r.Order))
                sb.Append($"{r.Date:yyyy-MM-dd},{r.MachineCode},{r.Order},{r.ExcelRow},\"{r.Model}\",{r.Qty},{r.Sut},{r.Seconds},{r.Normal},{r.Overtime},\"{r.Mapping}\"\n");
            File.WriteAllText(Path.Combine(dir, $"{unit.Month}_{unit.MachineCode}.csv"), sb.ToString(), Encoding.UTF8);
        }
    }
}
