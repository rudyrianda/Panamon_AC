using System.Globalization;
using System.IO.Compression;
using System.Xml;
using System.Xml.Linq;

namespace MonitoringSystem.Services.SapPlanImport
{
    /// <summary>Satu quantity produksi harian dari baris model (bukan baris kumulatif).</summary>
    public record PlanSourceCell(DateTime Date, string Section, string MachineCode, int Order, int ExcelRow, string ExcelCol, string Model, int Qty);

    public class ParsedPlanSheet
    {
        public string SheetName { get; init; } = string.Empty;
        public DateTime MonthStart { get; init; }
        public bool SheetFound { get; set; }
        public List<PlanSourceCell> Cells { get; } = new();
        /// <summary>Error per mesin (key MachineCode) atau "*" untuk error struktur yang berlaku untuk semua mesin.</summary>
        public Dictionary<string, List<string>> Errors { get; } = new();

        public void AddError(string machine, string message)
        {
            if (!Errors.TryGetValue(machine, out var list)) Errors[machine] = list = new List<string>();
            list.Add(message);
        }

        public List<string> ErrorsFor(string machine)
        {
            var all = new List<string>();
            if (Errors.TryGetValue("*", out var g)) all.AddRange(g);
            if (Errors.TryGetValue(machine, out var m)) all.AddRange(m);
            return all;
        }
    }

    /// <summary>
    /// Pembaca "Daily prod plan" (.xlsx) read-only, tanpa library Excel: membaca nilai tersimpan (cached value) tiap sel.
    /// Header tabel & kolom tanggal dideteksi dinamis. Hanya baris model bernomor yang dibaca; baris kumulatif diabaikan.
    /// </summary>
    public static class DailyPlanWorkbookReader
    {
        private static readonly XNamespace Ns = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
        private static readonly XNamespace RelNs = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
        private static readonly XNamespace PkgRelNs = "http://schemas.openxmlformats.org/package/2006/relationships";
        private static readonly string[] DayNames = { "Mon", "Tue", "Wed", "Thu", "Fri", "Sat", "Sun" };

        private readonly record struct XCell(string? Type, string? Value, bool HasFormula);

        public static string SheetNameFor(DateTime month) =>
            month.ToString("MMM", CultureInfo.InvariantCulture) + "_" + month.ToString("yy", CultureInfo.InvariantCulture);

        public static ParsedPlanSheet Parse(string xlsxPath, DateTime monthStart, string cuMachine, string csMachine)
        {
            var result = new ParsedPlanSheet { SheetName = SheetNameFor(monthStart), MonthStart = monthStart };
            using var zip = ZipFile.OpenRead(xlsxPath);

            var target = FindSheetEntry(zip, result.SheetName);
            if (target == null)
            {
                result.AddError("*", $"Sheet '{result.SheetName}' tidak ada di file.");
                return result;
            }
            result.SheetFound = true;

            var shared = ReadSharedStrings(zip);
            var cells = ReadCells(zip.GetEntry(target)!, shared);
            ParseTable(result, cells, cuMachine, csMachine);
            return result;
        }

        private static string? FindSheetEntry(ZipArchive zip, string sheetName)
        {
            var wb = XDocument.Load(zip.GetEntry("xl/workbook.xml")!.Open());
            var sheet = wb.Descendants(Ns + "sheet").FirstOrDefault(s => (string?)s.Attribute("name") == sheetName);
            if (sheet == null) return null;
            var rid = (string?)sheet.Attribute(RelNs + "id");
            var rels = XDocument.Load(zip.GetEntry("xl/_rels/workbook.xml.rels")!.Open());
            var rel = rels.Descendants(PkgRelNs + "Relationship").FirstOrDefault(r => (string?)r.Attribute("Id") == rid);
            var t = (string?)rel?.Attribute("Target");
            if (t == null) return null;
            t = t.TrimStart('/');
            return t.StartsWith("xl/", StringComparison.OrdinalIgnoreCase) ? t : "xl/" + t;
        }

        private static List<string> ReadSharedStrings(ZipArchive zip)
        {
            var list = new List<string>();
            var entry = zip.GetEntry("xl/sharedStrings.xml");
            if (entry == null) return list;
            using var reader = XmlReader.Create(entry.Open());
            reader.MoveToContent();
            while (!reader.EOF)
            {
                if (reader.NodeType == XmlNodeType.Element && reader.LocalName == "si")
                {
                    var si = (XElement)XNode.ReadFrom(reader);
                    // Teks biasa (<t>) atau rich text (<r><t>); abaikan teks fonetik (<rPh>)
                    list.Add(string.Concat(si.Elements(Ns + "t").Select(x => x.Value)
                        .Concat(si.Elements(Ns + "r").Elements(Ns + "t").Select(x => x.Value))));
                    continue;
                }
                reader.Read();
            }
            return list;
        }

        private static Dictionary<int, Dictionary<int, XCell>> ReadCells(ZipArchiveEntry entry, List<string> shared)
        {
            var cells = new Dictionary<int, Dictionary<int, XCell>>();
            using var reader = XmlReader.Create(entry.Open());
            reader.MoveToContent();
            while (!reader.EOF)
            {
                if (reader.NodeType == XmlNodeType.Element && reader.LocalName == "c")
                {
                    // XNode.ReadFrom sudah memajukan reader ke node berikutnya -> jangan Read() lagi
                    var c = (XElement)XNode.ReadFrom(reader);
                    var r = (string?)c.Attribute("r");
                    if (r == null) continue;
                    var (row, col) = SplitRef(r);
                    var type = (string?)c.Attribute("t");
                    string? value = c.Element(Ns + "v")?.Value;
                    if (type == "s" && value != null && int.TryParse(value, out var idx) && idx >= 0 && idx < shared.Count) value = shared[idx];
                    else if (type == "inlineStr") value = string.Concat(c.Descendants(Ns + "t").Select(x => x.Value));
                    if (!cells.TryGetValue(row, out var rowCells)) cells[row] = rowCells = new Dictionary<int, XCell>();
                    rowCells[col] = new XCell(type, value, c.Element(Ns + "f") != null);
                    continue;
                }
                reader.Read();
            }
            return cells;
        }

        private static (int row, int col) SplitRef(string cellRef)
        {
            int col = 0, i = 0;
            for (; i < cellRef.Length && char.IsLetter(cellRef[i]); i++) col = col * 26 + (char.ToUpperInvariant(cellRef[i]) - 'A' + 1);
            return (int.Parse(cellRef.AsSpan(i), CultureInfo.InvariantCulture), col);
        }

        public static string ColName(int col)
        {
            var s = string.Empty;
            while (col > 0) { var m = (col - 1) % 26; s = (char)('A' + m) + s; col = (col - 1) / 26; }
            return s;
        }

        private static string Text(Dictionary<int, Dictionary<int, XCell>> cells, int row, int col) =>
            cells.TryGetValue(row, out var r) && r.TryGetValue(col, out var c) ? (c.Value ?? string.Empty).Trim() : string.Empty;

        private static void ParseTable(ParsedPlanSheet result, Dictionary<int, Dictionary<int, XCell>> cells, string cuMachine, string csMachine)
        {
            var rows = cells.Keys.OrderBy(k => k).ToList();

            // 1. Header: sel "MODEL"
            int hdrRow = 0, modelCol = 0;
            foreach (var r in rows)
            {
                var hit = cells[r].FirstOrDefault(kv => string.Equals((kv.Value.Value ?? "").Trim(), "MODEL", StringComparison.OrdinalIgnoreCase));
                if (hit.Key > 0) { hdrRow = r; modelCol = hit.Key; break; }
            }
            if (hdrRow == 0) { result.AddError("*", "Header 'MODEL' tidak ditemukan."); return; }

            int noCol = 1;
            for (var r = hdrRow; r <= hdrRow + 2; r++)
                if (cells.TryGetValue(r, out var rc))
                    foreach (var kv in rc)
                        if (string.Equals((kv.Value.Value ?? "").Trim(), "NO", StringComparison.OrdinalIgnoreCase)) noCol = kv.Key;

            // 2. Kolom tanggal: nama hari di baris header, tanggal awal (serial Excel) di bawah kolom pertama
            var dayCols = cells[hdrRow].Where(kv => DayNames.Contains((kv.Value.Value ?? "").Trim())).Select(kv => kv.Key).OrderBy(c => c).ToList();
            if (dayCols.Count == 0) { result.AddError("*", "Header nama hari (Mon..Sun) tidak ditemukan."); return; }
            var firstDateCol = dayCols[0];
            DateTime? start = null;
            for (var r = hdrRow + 1; r <= hdrRow + 2; r++)
                if (double.TryParse(Text(cells, r, firstDateCol), NumberStyles.Float, CultureInfo.InvariantCulture, out var serial) && serial > 30000)
                    start = DateTime.FromOADate(serial).Date;
            if (start == null) { result.AddError("*", $"Tanggal awal di bawah kolom {ColName(firstDateCol)} tidak ditemukan."); return; }
            if (start.Value != result.MonthStart)
            {
                result.AddError("*", $"Tanggal awal tabel {start:yyyy-MM-dd} tidak sama dengan bulan sheet {result.MonthStart:yyyy-MM}.");
                return;
            }
            var daysInMonth = DateTime.DaysInMonth(start.Value.Year, start.Value.Month);
            var dateCols = new Dictionary<int, DateTime>();
            for (var i = 0; i < daysInMonth; i++)
            {
                var col = firstDateCol + i;
                var date = start.Value.AddDays(i);
                var expected = date.ToString("ddd", CultureInfo.InvariantCulture);
                var hdr = Text(cells, hdrRow, col);
                if (!string.Equals(hdr, expected, StringComparison.OrdinalIgnoreCase))
                {
                    result.AddError("*", $"Header hari kolom {ColName(col)} = '{hdr}', seharusnya '{expected}' ({date:yyyy-MM-dd}).");
                    return;
                }
                dateCols[col] = date;
            }

            // 3. Baris model bernomor. Bagian CU sampai model "CS-"/"EP-" pertama; KIOS ikut bagian tempatnya.
            var section = "CU";
            var order = 0;
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var r in rows.Where(r => r > hdrRow))
            {
                var model = Text(cells, r, modelCol);
                if (string.Equals(model, "TOTAL", StringComparison.OrdinalIgnoreCase)) break;
                var no = Text(cells, r, noCol);
                if (model.Length == 0 || !int.TryParse(no, NumberStyles.Integer, CultureInfo.InvariantCulture, out _)) continue;

                if (section == "CU" && (model.StartsWith("CS-", StringComparison.OrdinalIgnoreCase) || model.StartsWith("EP-", StringComparison.OrdinalIgnoreCase)))
                    section = "CS";
                var machine = section == "CU" ? cuMachine : csMachine;
                if (section == "CS" && (model.StartsWith("CU-", StringComparison.OrdinalIgnoreCase) || model.StartsWith("EU-", StringComparison.OrdinalIgnoreCase)))
                {
                    result.AddError("*", $"Baris {r}: model '{model}' berada di bagian CS, bagian tidak jelas - perlu ditinjau.");
                    continue;
                }
                order++;
                if (!seen.Add(section + "|" + model))
                {
                    result.AddError(machine, $"Baris {r}: model '{model}' muncul lebih dari sekali di bagian {section}.");
                    continue;
                }

                foreach (var (col, date) in dateCols.OrderBy(kv => kv.Key))
                {
                    if (!cells[r].TryGetValue(col, out var cell)) continue;
                    var where = $"Baris {r} kolom {ColName(col)} ({date:yyyy-MM-dd}) {model}";
                    var raw = (cell.Value ?? string.Empty).Trim();
                    if (cell.HasFormula && cell.Value == null) { result.AddError(machine, $"{where}: rumus tanpa nilai hasil."); continue; }
                    if (raw.Length == 0) continue;
                    if (cell.Type == "e") { result.AddError(machine, $"{where}: error Excel '{raw}'."); continue; }
                    if (cell.Type is "s" or "str" or "inlineStr" or "b") { result.AddError(machine, $"{where}: bukan angka ('{raw}')."); continue; }
                    if (!double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var num)) { result.AddError(machine, $"{where}: bukan angka ('{raw}')."); continue; }
                    if (num == 0) continue;
                    if (num < 0 || num != Math.Floor(num) || num > int.MaxValue) { result.AddError(machine, $"{where}: nilai tidak valid {num.ToString(CultureInfo.InvariantCulture)}."); continue; }
                    result.Cells.Add(new PlanSourceCell(date, section, machine, order, r, ColName(col), model, (int)num));
                }
            }
        }
    }
}
