using System.Text.RegularExpressions;

namespace MonitoringSystem.Pages.CAC.Quality
{
    /// <summary>
    /// Pengelompokan data NG_RPTS untuk halaman Quality.
    /// NG_RPTS tidak punya kolom kategori, jadi Station / Kategori / Disposisi
    /// diturunkan dari teks Station, Cause, Detail dan ActionDefect.
    /// Ubah keyword di file ini bila pengelompokan perlu disesuaikan.
    /// </summary>
    public static class QualityDefectCatalog
    {
        public const string OthersKey = "OTHERS";
        public const string OthersLabel = "Others";

        public record StationGroup(string Key, string Label, string[] Keywords);
        public record DefectCategory(string Key, string Label, string[] Keywords);

        public class LineDefinition
        {
            public string Code { get; init; } = "";
            public string MachineCode { get; init; } = "";
            public List<StationGroup> Stations { get; init; } = new();
            // Dicek berurutan; kategori dengan Keywords kosong = default.
            public List<DefectCategory> Categories { get; init; } = new();
        }

        // CAC: line masih frontend saja (belum ada data). Struktur station/kategori sementara mengikuti CS.
        private static LineDefinition CacLine(string code, string machineCode) => new()
        {
            Code = code,
            MachineCode = machineCode,
            Stations = new()
            {
                new("ASSY_RUN", "Assy-Running", new[] { "prepar", "inner", "running", "runing", "starting", "gas", "assy", "assembly", "chassis" }),
                new("FINAL", "Final Inspection", new[] { "final", "detail" }),
            },
            Categories = new()
            {
                new("SUPPLIER", "Supplier Parts", Array.Empty<string>()),
                new("PROCESS", "Process Assy", new[] {
                    "process", "proses", "operator", "pasang", "salah", "terbalik", "lupa",
                    "nyentuh", "screw", "instal", "nempel", "keluar jalur" }),
                new("INHOUSE", "InHouse Parts", new[] {
                    "in-house", "inhouse", "evaporator", " eva ", "chasys", "chassys", "chassis",
                    "press/he", "condensor", "kondensor", "las bocor" }),
            }
        };

        public static readonly LineDefinition CAC = CacLine("CAC", "CAC");
        public static readonly LineDefinition SKD = CacLine("SKD", "SKD");
        public static readonly LineDefinition StandingFloor = CacLine("Standing Floor", "STANDING FLOOR");

        public static readonly IReadOnlyList<LineDefinition> Lines = new[] { CAC, SKD, StandingFloor };

        // Urutan pengecekan kategori: yang lebih spesifik dulu, default terakhir.
        private static readonly Dictionary<string, string[]> CategoryCheckOrder = new()
        {
            ["CAC"] = new[] { "PROCESS", "INHOUSE" },
            ["SKD"] = new[] { "PROCESS", "INHOUSE" },
            ["Standing Floor"] = new[] { "PROCESS", "INHOUSE" },
        };

        public static LineDefinition ResolveLine(string? lineOrMachineCode)
        {
            var value = (lineOrMachineCode ?? "").Trim();
            return Lines.FirstOrDefault(l =>
                       l.Code.Equals(value, StringComparison.OrdinalIgnoreCase) ||
                       l.MachineCode.Equals(value, StringComparison.OrdinalIgnoreCase))
                   ?? CAC;
        }

        public static StationGroup ResolveStation(LineDefinition line, string? rawStation)
        {
            var text = Normalize(rawStation);
            if (text.Length > 0)
            {
                foreach (var station in line.Stations)
                {
                    if (station.Keywords.Any(k => text.Contains(k)))
                    {
                        return station;
                    }
                }
            }
            return new StationGroup(OthersKey, OthersLabel, Array.Empty<string>());
        }

        public static DefectCategory ResolveCategory(LineDefinition line, string? cause, string? detail)
        {
            var text = " " + Normalize(cause + " " + detail) + " ";
            foreach (var key in CategoryCheckOrder[line.Code])
            {
                var category = line.Categories.First(c => c.Key == key);
                if (category.Keywords.Any(k => text.Contains(k)))
                {
                    return category;
                }
            }
            return line.Categories.First(c => c.Keywords.Length == 0);
        }

        /// <summary>Change Product / scrap / return = Dispose, aksi lain yang terisi = Repair.</summary>
        public static bool IsDispose(string? actionDefect)
        {
            var text = Normalize(actionDefect);
            return text.Contains("change") || text.Contains("dispose") || text.Contains("scrap") || text.Contains("return");
        }

        public static bool IsRepair(string? actionDefect)
        {
            return !string.IsNullOrWhiteSpace(actionDefect) && !IsDispose(actionDefect);
        }

        public static string ItemLabel(string? detail, string? cause)
        {
            var value = Regex.Replace((detail ?? "").Trim(), @"\s+", " ");
            if (value.Length == 0)
            {
                value = Regex.Replace((cause ?? "").Trim(), @"\s+", " ");
            }
            return value.Length == 0 ? "(Tanpa Detail)" : value;
        }

        /// <summary>Tahun fiskal Panasonic: FY2026 = April 2026 s/d Maret 2027.</summary>
        public static IEnumerable<DateTime> FiscalMonths(int fiscalYear)
        {
            var start = new DateTime(fiscalYear, 4, 1);
            return Enumerable.Range(0, 12).Select(i => start.AddMonths(i));
        }

        public static int FiscalYearOf(DateTime date) => date.Month >= 4 ? date.Year : date.Year - 1;

        private static string Normalize(string? value)
        {
            return Regex.Replace((value ?? "").Trim().ToLowerInvariant(), @"\s+", " ");
        }
    }
}
