using System.Text.Json;
using Microsoft.Data.SqlClient;

namespace MonitoringSystem.Pages.ACOEE
{
    // Kategori loss time AC OEE = nama 18 kotak reason layar GOT LOSS TIME Expander Kyoshin 6.35 (R410..R580).
    // Daftarnya mengikuti PLC: diambil dari salinan reason terakhir yang disimpan Plclogger di dbo.PlcKyoshinLossEvent
    // (kolom ReasonsAtStart, diperbarui setiap ada loss). Dipakai bersama Detail Loss Time dan Trend Loss Time.
    //   - Kategori utama  : nama kotak saat ini, urut sesuai GOT; warna mengikuti nomor kotak.
    //   - Nama lama       : reason yang sudah diganti di GOT tetap jadi kategori tambahan (abu-abu), hanya kalau ada datanya.
    //   - OTHER           : reason kosong / kategori lama yang padanannya sudah tidak ada di PLC.
    public static class AcOeeLossCategories
    {
        public const string MachineTrouble = "MACHINE TROUBLE";
        public const string Other = "OTHER";

        public sealed record Slot(int No, string Name);

        // Dipakai kalau belum ada salinan reason dari PLC (nama yang terbaca 04/10/2026)
        public static readonly IReadOnlyList<string> Names = new[]
        {
            "MODEL CHANGE", "MANDREL CHANGE", "GAWSE EXTERNAL", "MATERIAL SHORT.EXT", "MATERIAL SHORT.INT",
            "MATERIAL SHORT.INH", "MAN POWER AJUS", "QUALITY TROUBLE", "MACHINE TROUBLE", "SET REPAIRING",
            "REWORK", "GENERAL ASSEMBLY", "LOSS AWAL HARI", "MORNING ASSEMBLY", "TROLLY HABIS",
            "BILLET COUNTER", "GANTI TROLLY", "OTHER"
        };

        // Warna per nomor kotak 1..18 (kotak yang sama tetap berwarna sama walau namanya diganti)
        private static readonly string[] SlotColors =
        {
            "#FF6384", "#FF8FAB", "#36A2EB", "#1A6EBF", "#4BC0C0", "#9966FF", "#FFCE56", "#FF9F40", "#C9CBCF",
            "#A0A0A0", "#FF9F80", "#198754", "#20C997", "#0D6EFD", "#E83E8C", "#FD7E14", "#6F42C1", "#77DD77"
        };
        private static readonly string[] OldNameColors = { "#8A8F98", "#6C757D", "#ADB5BD", "#5A6268", "#9AA0A6" };
        private const string OtherColor = "#77DD77";

        // Nama kategori lama AC OEE -> reason GOT yang setara (nama per 04/10/2026)
        public static readonly IReadOnlyDictionary<string, string> FromLegacy = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "Model Change Loss",          "MODEL CHANGE" },
            { "Mold Change Loss",           "MANDREL CHANGE" },
            { "Gawse - External Bodies",    "GAWSE EXTERNAL" },
            { "Material Shortage External", "MATERIAL SHORT.EXT" },
            { "Material Shortage Internal", "MATERIAL SHORT.INT" },
            { "Material Shortage Inhouse",  "MATERIAL SHORT.INH" },
            { "Man Power Adjustment",       "MAN POWER AJUS" },
            { "Quality Trouble",            "QUALITY TROUBLE" },
            { "Machine & Tools Trouble",    "MACHINE TROUBLE" },
            { "Set Repairing Loss",         "SET REPAIRING" },
            { "Rework",                     "REWORK" },
            { "General Assembly",           "GENERAL ASSEMBLY" },
            { "Loss Awal Hari",             "LOSS AWAL HARI" },
            { "Morning Assembly",           "MORNING ASSEMBLY" },
            { "Other",                      "OTHER" }
        };

        // Nama 18 kotak saat ini dari salinan reason terakhir di PlcKyoshinLossEvent; kotak tanpa nama dilewati
        public static List<Slot> LoadCurrentSlots(string connectionString)
        {
            try
            {
                using var conn = new SqlConnection(connectionString);
                conn.Open();
                using var cmd = new SqlCommand(@"
                    SELECT TOP 1 ReasonsAtStart FROM dbo.PlcKyoshinLossEvent
                    WHERE MachineCode = 'MCH1-01' AND ReasonsAtStart IS NOT NULL
                    ORDER BY StartAt DESC, Id DESC", conn);
                if (cmd.ExecuteScalar() is string json)
                {
                    var slots = new List<Slot>();
                    foreach (var e in JsonDocument.Parse(json).RootElement.EnumerateArray())
                    {
                        var name = (e.GetProperty("Name").GetString() ?? "").Trim();
                        if (name.Length > 0 && !slots.Any(s => s.Name.Equals(name, StringComparison.OrdinalIgnoreCase)))
                            slots.Add(new Slot(e.GetProperty("Slot").GetInt32(), name));
                    }
                    if (slots.Count > 0) return slots.OrderBy(s => s.No).ToList();
                }
            }
            catch (Exception ex) { Console.WriteLine($"AcOeeLossCategories.LoadCurrentSlots: {ex.Message}"); }
            return Names.Select((n, i) => new Slot(i + 1, n)).ToList();
        }

        // Nama untuk TAMPILAN saja: "MODEL CHANGE" -> "Model Change". Pencocokan, warna, filter & data tetap memakai nama asli PLC.
        public static string DisplayName(string? name)
        {
            var trimmed = (name ?? "").Trim();
            return trimmed.Length == 0 ? trimmed
                : System.Globalization.CultureInfo.GetCultureInfo("en-US").TextInfo.ToTitleCase(trimmed.ToLowerInvariant());
        }

        // Nama persis (huruf besar/kecil diabaikan) di antara nama yang diberikan; null kalau tidak ada
        public static string? Match(string? name, IEnumerable<string> names) =>
            names.FirstOrDefault(n => string.Equals(n, (name ?? "").Trim(), StringComparison.OrdinalIgnoreCase));

        public static string? Match(string? name) => Match(name, Names);

        // Kategori kejadian loss PLC: nama kotak saat ini, nama lama apa adanya (kategori tambahan), kosong = OTHER
        public static string ForPlcReason(string? reason, IReadOnlyList<Slot> slots)
        {
            var trimmed = (reason ?? "").Trim();
            if (trimmed.Length == 0) return Other;
            return Match(trimmed, slots.Select(s => s.Name)) ?? trimmed;
        }

        // Daftar kategori: kotak saat ini (urut GOT), lalu nama lama yang ada datanya; OTHER ditambahkan di akhir kalau tidak ada di GOT
        public static List<string> BuildCategoryList(IReadOnlyList<Slot> slots, IEnumerable<string> categoriesWithData)
        {
            var list = slots.Select(s => s.Name).ToList();
            foreach (var cat in categoriesWithData.Where(c => !string.IsNullOrWhiteSpace(c)).Distinct(StringComparer.OrdinalIgnoreCase))
                if (Match(cat, list) == null && !cat.Equals(Other, StringComparison.OrdinalIgnoreCase)) list.Add(cat);
            if (Match(Other, list) == null) list.Add(Other);
            return list;
        }

        public static Dictionary<string, string> BuildColors(IReadOnlyList<Slot> slots, IEnumerable<string> categories)
        {
            var colors = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var s in slots) colors[s.Name] = SlotColors[(s.No - 1 + SlotColors.Length) % SlotColors.Length];
            int old = 0;
            foreach (var cat in categories)
                if (!colors.ContainsKey(cat))
                    colors[cat] = cat.Equals(Other, StringComparison.OrdinalIgnoreCase) ? OtherColor : OldNameColors[old++ % OldNameColors.Length];
            return colors;
        }
    }
}
