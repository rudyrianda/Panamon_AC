namespace MonitoringSystem.Services.SapPlanImport
{
    public record SapPlanAlias(string ExcelName, string MachineCode, string MasterDataName, string? Note);

    /// <summary>Satu baris hasil: siap menjadi 1 record SapPlan (Shift NS).</summary>
    public record SapPlanRow(DateTime Date, string MachineCode, int Order, int ExcelRow, string Model, int Qty, int Sut,
        long Seconds, int Normal, int Overtime, string Mapping);

    public record SapPlanDaySummary(DateTime Date, string MachineCode, int Models, int Qty, int Normal, int Overtime,
        long Seconds, int CapacitySeconds, long OvertimeSeconds);

    /// <summary>
    /// Cari SUT (MasterData persis / alias eksplisit, tanpa fuzzy match) lalu alokasikan Normal/OVT per tanggal & mesin:
    /// total detik &lt;= kapasitas -> semua Normal; lebih -> OVT diambil dari model terakhir (urutan Excel) ke atas,
    /// model yang terpotong: OVT = ceil(sisa detik / SUT), Normal = Qty - OVT.
    /// </summary>
    public class SapPlanCalculator
    {
        private readonly Dictionary<string, int> _sut;              // "NAMA|MESIN" -> SUT (-1 = ambigu)
        private readonly Dictionary<string, SapPlanAlias> _aliases; // "NAMA EXCEL|MESIN" -> alias

        public SapPlanCalculator(Dictionary<string, int> sutByNameAndMachine, IEnumerable<SapPlanAlias> aliases)
        {
            _sut = sutByNameAndMachine;
            _aliases = new Dictionary<string, SapPlanAlias>(StringComparer.OrdinalIgnoreCase);
            foreach (var a in aliases) _aliases[Key(a.ExcelName, a.MachineCode)] = a;
        }

        public static string Key(string name, string machine) => name.Trim().ToUpperInvariant() + "|" + machine.Trim().ToUpperInvariant();

        /// <summary>SUT untuk model di mesin tertentu. Null + pesan bila tidak ada / ambigu.</summary>
        public (int? sut, string mapping, string? error) ResolveSut(string model, string machine)
        {
            if (_sut.TryGetValue(Key(model, machine), out var s))
                return s > 0 ? (s, "MasterData", null) : (null, "", $"SUT '{model}' ({machine}) di MasterData ambigu/tidak valid.");

            if (_aliases.TryGetValue(Key(model, machine), out var alias))
            {
                if (_sut.TryGetValue(Key(alias.MasterDataName, machine), out var sa) && sa > 0)
                    return (sa, $"alias -> {alias.MasterDataName}", null);
                return (null, "", $"Alias '{model}' -> '{alias.MasterDataName}' ({machine}) tidak punya SUT valid di MasterData.");
            }
            return (null, "", $"Model '{model}' ({machine}) tidak ada di MasterData dan belum punya alias.");
        }

        public (List<SapPlanRow> rows, List<SapPlanDaySummary> days, List<string> errors) Calculate(
            IEnumerable<PlanSourceCell> cells, string machine, int capacitySeconds)
        {
            var rows = new List<SapPlanRow>();
            var days = new List<SapPlanDaySummary>();
            var errors = new List<string>();

            foreach (var day in cells.Where(c => c.MachineCode == machine).GroupBy(c => c.Date).OrderBy(g => g.Key))
            {
                var items = day.OrderBy(c => c.Order).ToList();
                var work = new List<(PlanSourceCell cell, int sut, string mapping)>();
                foreach (var c in items)
                {
                    var (sut, mapping, err) = ResolveSut(c.Model, machine);
                    if (err != null) { errors.Add($"{c.Date:yyyy-MM-dd} baris {c.ExcelRow}: {err}"); continue; }
                    work.Add((c, sut!.Value, mapping));
                }
                if (work.Count != items.Count) continue; // tanggal ini tidak bisa dihitung -> error sudah dicatat

                var seconds = work.Select(w => (long)w.cell.Qty * w.sut).ToArray();
                var total = seconds.Sum();
                var ovtQty = new int[work.Count];
                var remain = Math.Max(0, total - capacitySeconds);
                for (var i = work.Count - 1; i >= 0 && remain > 0; i--)
                {
                    if (seconds[i] <= remain) { ovtQty[i] = work[i].cell.Qty; remain -= seconds[i]; }
                    else { ovtQty[i] = (int)Math.Ceiling(remain / (double)work[i].sut); remain = 0; }
                }

                for (var i = 0; i < work.Count; i++)
                {
                    var (c, sut, mapping) = work[i];
                    var normal = c.Qty - ovtQty[i];
                    if (normal < 0 || normal + ovtQty[i] != c.Qty)
                        errors.Add($"{c.Date:yyyy-MM-dd} {c.Model}: Normal+OVT tidak sama dengan Qty sumber.");
                    rows.Add(new SapPlanRow(c.Date, machine, c.Order, c.ExcelRow, c.Model, c.Qty, sut, seconds[i], normal, ovtQty[i], mapping));
                }
                days.Add(new SapPlanDaySummary(day.Key, machine, work.Count, work.Sum(w => w.cell.Qty),
                    work.Sum(w => w.cell.Qty) - ovtQty.Sum(), ovtQty.Sum(), total, capacitySeconds, Math.Max(0, total - capacitySeconds)));
            }
            return (rows, days, errors);
        }
    }
}
