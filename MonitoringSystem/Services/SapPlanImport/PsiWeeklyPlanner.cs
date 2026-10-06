using System.Text.Json;

namespace MonitoringSystem.Services.SapPlanImport
{
    /// <summary>Prioritas satu model di list mingguan.</summary>
    public record PsiPriority(string Category, int Rank, string Via);

    /// <summary>Satu baris list mingguan (Senin-Minggu, dipotong di batas bulan).</summary>
    public record PsiWeeklyRow(DateTime WeekStart, DateTime WeekEnd, DateTime PlanDate, int SeqInWeek, int SeqInDay,
        string Model, int Qty, List<DateTime> SourceDates, bool IsMerged, string? MergeReason, string Category, int PriorityRank, string Via);

    /// <summary>
    /// Daftar prioritas dari psi-weekly-priority.json: kategori berurutan, di dalamnya Models lalu Similar.
    /// Model hanya dimasukkan jika namanya persis ada di daftar ini DAN di Master Data Produk PLC ROHIB (dbo.MasterProduct).
    /// Tidak ada pencocokan varian (-P / -E) atau nama mirip otomatis.
    /// </summary>
    public class PsiPriorityList
    {
        private readonly Dictionary<string, PsiPriority> _map = new(StringComparer.OrdinalIgnoreCase);
        private HashSet<string>? _masterModels;

        public int SmallQtyThreshold { get; private set; } = 100;

        /// <summary>Model yang terdaftar di Master Data Produk. Model di luar ini tidak dimasukkan ke list.</summary>
        public void RestrictTo(IEnumerable<string> masterModels) =>
            _masterModels = new HashSet<string>(masterModels.Select(m => m.Trim()), StringComparer.OrdinalIgnoreCase);

        /// <summary>Alasan model tidak dimasukkan, atau null jika dimasukkan.</summary>
        public string? RejectReason(string model)
        {
            model = model.Trim();
            if (!_map.ContainsKey(model)) return "tidak ada di daftar prioritas";
            if (_masterModels != null && !_masterModels.Contains(model)) return "tidak ada di Master Data Produk";
            return null;
        }

        public static PsiPriorityList Load(string path)
        {
            if (!File.Exists(path)) throw new FileNotFoundException("File prioritas list mingguan tidak ditemukan.", path);
            using var doc = JsonDocument.Parse(File.ReadAllText(path));
            var list = new PsiPriorityList();
            if (doc.RootElement.TryGetProperty("SmallQtyThreshold", out var t)) list.SmallQtyThreshold = t.GetInt32();

            var catIndex = 0;
            foreach (var cat in doc.RootElement.GetProperty("Categories").EnumerateArray())
            {
                catIndex++;
                var name = cat.GetProperty("Category").GetString()!;
                var pos = 0;
                foreach (var (prop, via) in new[] { ("Models", "daftar"), ("Similar", "mirip") })
                {
                    if (!cat.TryGetProperty(prop, out var arr)) continue;
                    foreach (var m in arr.EnumerateArray())
                    {
                        var model = m.GetString()!.Trim();
                        pos++;
                        // Rank: kategori * 100000 + posisi * 10
                        if (!list._map.TryAdd(model, new PsiPriority(name, catIndex * 100000 + pos * 10, via)))
                            throw new InvalidOperationException($"Model '{model}' muncul lebih dari sekali di file prioritas.");
                    }
                }
            }
            return list;
        }

        /// <summary>null = model tidak dimasukkan (tidak di daftar, atau tidak di Master Data Produk).</summary>
        public PsiPriority? Resolve(string model) =>
            RejectReason(model) == null ? _map[model.Trim()] : null;
    }

    /// <summary>
    /// List mingguan dari quantity harian "Daily prod plan":
    /// - Minggu = Senin s/d Minggu, dipotong di awal/akhir bulan (tiap bulan dari sheet-nya sendiri).
    /// - Model sama di hari berurutan dalam minggu yang sama digabung ke hari pertama.
    /// - Qty (gabungan sejauh ini) di bawah SmallQtyThreshold: qty berikutnya model itu dalam minggu yang sama ikut digabung.
    /// - Urutan per hari: model biasa menurut prioritas, lalu model hasil gabungan (juga menurut prioritas) paling akhir.
    /// </summary>
    public static class PsiWeeklyPlanner
    {
        public const string MergeConsecutive = "HariBerurutan";
        public const string MergeSmallQty = "QtyKecil";

        public static (List<PsiWeeklyRow> Rows, List<string> Ignored) Build(IEnumerable<PlanSourceCell> cells, DateTime month, PsiPriorityList priority)
        {
            var monthEnd = month.AddMonths(1).AddDays(-1);
            var ignored = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
            var groups = new List<(DateTime WeekStart, DateTime WeekEnd, string Model, PsiPriority Prio, int FirstRow, List<DateTime> Dates, int Qty, SortedSet<string> Reasons)>();

            foreach (var byModel in cells.Where(c => c.Date >= month && c.Date <= monthEnd && c.Qty > 0)
                                         .GroupBy(c => c.Model.Trim(), StringComparer.OrdinalIgnoreCase))
            {
                var prio = priority.Resolve(byModel.Key);
                if (prio == null)
                {
                    ignored.Add($"{byModel.Key} (Qty {byModel.Sum(c => c.Qty)}, {priority.RejectReason(byModel.Key)})");
                    continue;
                }

                foreach (var byWeek in byModel.GroupBy(c => WeekStart(c.Date, month)))
                {
                    var weekEnd = byWeek.Key.AddDays(6 - DaysFromMonday(byWeek.Key));
                    if (weekEnd > monthEnd) weekEnd = monthEnd;

                    (List<DateTime> Dates, int Qty, SortedSet<string> Reasons)? cur = null;
                    foreach (var day in byWeek.GroupBy(c => c.Date).OrderBy(g => g.Key))
                    {
                        var qty = day.Sum(c => c.Qty);
                        if (cur != null)
                        {
                            var g = cur.Value;
                            var consecutive = day.Key == g.Dates[^1].AddDays(1);
                            if (consecutive || g.Qty < priority.SmallQtyThreshold)
                            {
                                g.Dates.Add(day.Key);
                                g.Reasons.Add(consecutive ? MergeConsecutive : MergeSmallQty);
                                cur = (g.Dates, g.Qty + qty, g.Reasons);
                                continue;
                            }
                            groups.Add((byWeek.Key, weekEnd, byModel.Key, prio, byModel.Min(c => c.ExcelRow), g.Dates, g.Qty, g.Reasons));
                        }
                        cur = (new List<DateTime> { day.Key }, qty, new SortedSet<string>());
                    }
                    if (cur != null) groups.Add((byWeek.Key, weekEnd, byModel.Key, prio, byModel.Min(c => c.ExcelRow), cur.Value.Dates, cur.Value.Qty, cur.Value.Reasons));
                }
            }

            var rows = new List<PsiWeeklyRow>();
            foreach (var week in groups.GroupBy(g => (g.WeekStart, g.WeekEnd)).OrderBy(w => w.Key.WeekStart))
            {
                var seqWeek = 0;
                foreach (var day in week.GroupBy(g => g.Dates[0]).OrderBy(d => d.Key))
                {
                    var seqDay = 0;
                    var ordered = day.OrderBy(g => g.Dates.Count > 1 ? 1 : 0).ThenBy(g => g.Prio.Rank).ThenBy(g => g.FirstRow);
                    foreach (var g in ordered)
                    {
                        var merged = g.Dates.Count > 1;
                        rows.Add(new PsiWeeklyRow(week.Key.WeekStart, week.Key.WeekEnd, day.Key, ++seqWeek, ++seqDay, g.Model, g.Qty, g.Dates,
                            merged, merged ? string.Join("+", g.Reasons) : null, g.Prio.Category, g.Prio.Rank, g.Prio.Via));
                    }
                }
            }
            return (rows, ignored.ToList());
        }

        private static int DaysFromMonday(DateTime d) => ((int)d.DayOfWeek + 6) % 7;

        /// <summary>Senin minggu tanggal itu, tetapi tidak sebelum tanggal 1 bulan tersebut.</summary>
        public static DateTime WeekStart(DateTime date, DateTime month)
        {
            var monday = date.AddDays(-DaysFromMonday(date));
            return monday < month ? month : monday;
        }
    }
}
