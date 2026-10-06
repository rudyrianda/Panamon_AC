using System.Globalization;
using System.Text;

namespace MonitoringSystem.Pages.SubPanamon.PWKActual;

/// <summary>
/// Membuat satu halaman PDF A4 portrait tanpa executable/native dependency tambahan.
/// Layout sengaja mengikuti susunan lembar PWK: Given Time, Lose Time, produksi,
/// catatan masalah, dan rekap dalam satu halaman A4 portrait.
/// </summary>
internal static class PwKPdfGenerator
{
    private const double PageWidth = 595.28;  // A4 portrait, point
    private const double PageHeight = 841.89;
    private const double Margin = 10;
    private const double DesignWidth = 1600;

    public static byte[] Create(IndexModel model)
    {
        var lossRows = model.JamRows();
        var productionRows = model.Prods ?? new List<IndexModel.ProdRow>();
        var noteRows = model.Catatan?.Count > 0
            ? model.Catatan
            : new List<IndexModel.CatatanRow> { new() };

        // Tinggi virtual dihitung dari jumlah baris aktual. Sumbu horizontal dan
        // vertikal diatur terpisah supaya lembar memakai seluruh area A4 portrait,
        // tanpa menyisakan setengah halaman kosong.
        var designHeight = 78d + 34d
            + (18d + 24d + Math.Max(6, model.Given?.Count ?? 0) * 20d + 20d)
            + (18d + 24d + 96d + 18d + lossRows.Count * 22d + 22d)
            + (18d + 44d + Math.Max(1, productionRows.Count) * 21d + 22d)
            + (18d + 22d + Math.Max(1, noteRows.Count) * 28d)
            + 66d;

        var horizontalScale = (PageWidth - 2 * Margin) / DesignWidth;
        var verticalScale = (PageHeight - 2 * Margin) / designHeight;
        var canvas = new PdfCanvas(PageWidth, PageHeight, Margin, horizontalScale, verticalScale);
        var y = 0d;

        DrawDocumentHeader(canvas, model, ref y);
        DrawContext(canvas, model, ref y);
        DrawGiven(canvas, model, ref y);
        DrawLoss(canvas, model, lossRows, ref y);
        DrawProduction(canvas, model, productionRows, ref y);
        DrawNotes(canvas, noteRows, ref y);
        DrawSummary(canvas, model, ref y);

        return MinimalPdf.Write(canvas.Content, PageWidth, PageHeight);
    }

    private static void DrawDocumentHeader(PdfCanvas c, IndexModel m, ref double y)
    {
        c.Cell(0, y, 330, 78, "PENGAWASAN WAKTU KERJA\n(TIME CONTROL)\nFOR LINE", true, true, 16);

        const double middleX = 330;
        const double middleW = 710;
        var middleCols = new[] { 180d, 106d, 106d, 106d, 106d, 106d };
        var labels = new[] { "HARI", "SEN-KAM", "JUMAT", "SHIFT 1", "SHIFT 2", "SHIFT 3" };
        var values = new[]
        {
            "A : WORKING TIME", m.HdrNonShift.ToString(), m.HdrNonShiftJumat.ToString(),
            m.HdrShift1.ToString(), m.HdrShift2.ToString(), m.HdrShift3.ToString()
        };
        var x = middleX;
        for (var i = 0; i < middleCols.Length; i++)
        {
            c.Cell(x, y, middleCols[i], 30, labels[i], true, true, 9);
            c.Cell(x, y + 30, middleCols[i], 48, values[i], false, i == 0, 9);
            x += middleCols[i];
        }

        var signX = middleX + middleW;
        var signW = (DesignWidth - signX) / 4d;
        var signs = new[] { "MANAGER", "S-CHIEF", "G.CHIEF", "LEADER" };
        for (var i = 0; i < signs.Length; i++)
        {
            c.Cell(signX + i * signW, y, signW, 30, signs[i], true, true, 9);
            c.Cell(signX + i * signW, y + 30, signW, 48, "", false, false, 9);
        }
        y += 78;
    }

    private static void DrawContext(PdfCanvas c, IndexModel m, ref double y)
    {
        var labels = new[] { "TANGGAL", "LINE", "GROUP", "MACHINE CODE", "SHIFT", "JUMLAH OPR", "WAKTU KERJA (A)" };
        var values = new[]
        {
            m.Tanggal.ToString("dd-MM-yyyy"), IndexModel.LineName, m.GroupName ?? "-", m.MachineCode,
            m.Shift ?? "-", m.JumlahOpr.ToString(CultureInfo.InvariantCulture), $"{m.WorkingTime} menit"
        };
        var w = DesignWidth / labels.Length;
        for (var i = 0; i < labels.Length; i++)
        {
            c.Cell(i * w, y, w, 15, labels[i], true, true, 8);
            c.Cell(i * w, y + 15, w, 19, values[i], false, false, 9);
        }
        y += 34;
    }

    private static void DrawGiven(PdfCanvas c, IndexModel m, ref double y)
    {
        c.SectionTitle(y, "A. GIVEN TIME");
        y += 18;
        var widths = new[] { 500d, 220d, 220d, 220d, 220d, 220d };
        var headers = new[] { "ITEM", "TERDAFTAR", "ABSENSI", "WAKTU KERJA", "LEMBUR", "TOTAL" };
        DrawRow(c, y, 24, widths, headers, true, 9);
        y += 24;

        var rows = m.Given ?? new List<IndexModel.GivenRow>();
        var rowCount = Math.Max(6, rows.Count);
        var bodyY = y;
        var xItem = 0d;
        var xTerdaftar = widths[0];
        var xAbsensi = xTerdaftar + widths[1];
        var xWaktu = xAbsensi + widths[2];
        var xLembur = xWaktu + widths[3];
        var xTotal = xLembur + widths[4];

        c.Cell(xWaktu, bodyY, widths[3], rowCount * 20, m.Fmt(m.WorkingTime), false, false, 8);
        c.Cell(xLembur, bodyY, widths[4], rowCount * 20, m.Fmt(m.SharedLembur), false, false, 8);
        c.Cell(xTotal, bodyY, widths[5], rowCount * 20 + 20, m.Fmt(m.TotalGiven), false, true, 9);

        for (var i = 0; i < rowCount; i++)
        {
            var row = rows.ElementAtOrDefault(i);
            c.Cell(xItem, y, widths[0], 20, row?.Item ?? "", false, false, 8, alignLeft: true);
            c.Cell(xTerdaftar, y, widths[1], 20, row?.Terdaftar.ToString() ?? "", false, false, 8);
            c.Cell(xAbsensi, y, widths[2], 20, row?.Absensi.ToString() ?? "", false, false, 8);
            y += 20;
        }

        var totals = new[]
        {
            "TOTAL", m.Fmt(rows.Sum(v => v.Terdaftar)), m.Fmt(rows.Sum(v => v.Absensi)),
            m.Fmt(m.TotalWorkingTime), m.Fmt(m.TotalOvertime)
        };
        DrawRow(c, y, 20, widths.Take(5).ToArray(), totals, true, 9);
        y += 20;
    }

    private static void DrawLoss(PdfCanvas c, IndexModel m, IReadOnlyList<string> lossRows, ref double y)
    {
        c.SectionTitle(y, "B. LOSE TIME");
        y += 18;

        var reasons = m.Reasons.ToList();
        const double jamW = 165;
        const double totalW = 60;
        var reasonW = reasons.Count == 0 ? DesignWidth - jamW - totalW : (DesignWidth - jamW - totalW) / reasons.Count;

        c.Cell(0, y, jamW, 138, "ITEM TIME LOSS", true, true, 10);
        var x = jamW;
        foreach (var category in IndexModel.KategoriList)
        {
            var count = reasons.Count(r => r.Kategori == category);
            if (count == 0) continue;
            c.Cell(x, y, reasonW * count, 24, category, true, true, 8);
            x += reasonW * count;
        }
        c.Cell(DesignWidth - totalW, y, totalW, 138, "TOTAL", true, true, 9);

        x = jamW;
        foreach (var reason in reasons)
        {
            c.Cell(x, y + 24, reasonW, 96, reason.Name ?? "", true, false, 7, rotate: true);
            c.Cell(x, y + 120, reasonW, 18, reason.Id.ToString(), true, true, 7);
            x += reasonW;
        }
        c.Cell(0, y + 120, jamW, 18, "JAM", true, true, 8);
        y += 138;

        foreach (var jam in lossRows)
        {
            c.Cell(0, y, jamW, 22, string.IsNullOrEmpty(jam) ? "s/d" : jam, false, false, 7);
            x = jamW;
            foreach (var reason in reasons)
            {
                c.Cell(x, y, reasonW, 22, m.N(m.Cell(jam, reason.Id)), false, false, 7, alignRight: true);
                x += reasonW;
            }
            c.Cell(DesignWidth - totalW, y, totalW, 22, m.N(m.RowTotal(jam)), false, false, 8, alignRight: true);
            y += 22;
        }

        c.Cell(0, y, jamW, 22, "TOTAL", true, true, 9);
        x = jamW;
        foreach (var reason in reasons)
        {
            c.Cell(x, y, reasonW, 22, m.N(m.ColTotal(reason.Id)), true, true, 7, alignRight: true);
            x += reasonW;
        }
        c.Cell(DesignWidth - totalW, y, totalW, 22, m.TotalLoss.ToString(), true, true, 9, alignRight: true);
        y += 22;
    }

    private static void DrawProduction(PdfCanvas c, IndexModel m, IReadOnlyList<IndexModel.ProdRow> rows, ref double y)
    {
        c.SectionTitle(y, "C. HASIL PRODUKSI");
        y += 18;
        var w = new[] { 140d, 110d, 235d, 235d, 80d, 80d, 60d, 80d, 80d, 60d, 85d, 355d };
        var top = new[] { "MODEL", "TIME", "NO. SERI AWAL", "NO. SERI AKHIR", "DAILY", "", "", "ACCM", "", "", "DEFECT QTY", "KETERANGAN" };
        DrawRow(c, y, 22, w, top, true, 8);
        var second = new[] { "", "", "", "", "PLAN", "ACTUAL", "+/-", "PLAN", "ACTUAL", "+/-", "", "" };
        DrawRow(c, y + 22, 22, w, second, true, 8);
        y += 44;

        if (rows.Count == 0)
        {
            c.Cell(0, y, DesignWidth, 21, "Belum ada data produksi untuk tanggal, line, dan shift ini.", false, false, 8);
            y += 21;
        }
        else
        {
            foreach (var p in rows)
            {
                var values = new[]
                {
                    p.Model ?? "", p.Time, p.SeriAwal ?? "", p.SeriAkhir ?? "",
                    p.DailyPlan.ToString(), p.DailyActual.ToString(), (p.DailyActual - p.DailyPlan).ToString(),
                    p.AccmPlan.ToString(), p.AccmActual.ToString(), (p.AccmActual - p.AccmPlan).ToString(),
                    p.Defect.ToString(), p.Keterangan ?? ""
                };
                DrawRow(c, y, 21, w, values, false, 7, firstLeft: true, lastLeft: true);
                y += 21;
            }
        }

        var totals = new[]
        {
            "TOTAL", "", "", "", m.TotalPlan.ToString(), m.HasilProd.ToString(),
            (m.HasilProd - m.TotalPlan).ToString(), "", "", "", m.TotalDefect.ToString(), ""
        };
        DrawRow(c, y, 22, w, totals, true, 8);
        y += 22;
    }

    private static void DrawNotes(PdfCanvas c, IReadOnlyList<IndexModel.CatatanRow> rows, ref double y)
    {
        c.SectionTitle(y, "D. CATATAN MASALAH");
        y += 18;
        var w = new[] { 320d, 520d, 520d, 240d };
        DrawRow(c, y, 22, w, new[] { "HAMBATAN", "ANALISA MASALAH", "TINDAKAN", "DURASI" }, true, 9);
        y += 22;
        foreach (var row in rows)
        {
            DrawRow(c, y, 28, w, new[] { row.Hambatan ?? "", row.Analisa ?? "", row.Tindakan ?? "", row.Pic ?? "" }, false, 8,
                firstLeft: true, lastLeft: false, allLeft: true);
            y += 28;
        }
    }

    private static void DrawSummary(PdfCanvas c, IndexModel m, ref double y)
    {
        var gap = 12d;
        var w = (DesignWidth - gap * 2) / 3d;
        var available = m.AvailableWT;
        c.Cell(0, y, w, 66, $"( J ) HASIL PROD\n{m.Fmt(m.HasilProd)} pcs", false, true, 10);
        c.Cell(w + gap, y, w, 66,
            $"( K ) AVAILABLE WORKING TIME\n( A + Lembur ) x Jumlah Operator\n({m.Fmt(m.WorkingTime)} + {m.Fmt(m.SharedLembur)}) x {m.Fmt(m.JumlahOpr)} = {m.Fmt(available)}", false, true, 9);
        c.Cell((w + gap) * 2, y, w, 66,
            $"PROD / HEAD / HOUR\n( J x 60 ) / ( K )\n({m.Fmt(m.HasilProd)} x 60) / {m.Fmt(available)} = {m.ProdHeadHour}", false, true, 9);
        y += 66;
    }

    private static void DrawRow(PdfCanvas c, double y, double h, IReadOnlyList<double> widths,
        IReadOnlyList<string> values, bool header, double fontSize, bool firstLeft = false,
        bool lastLeft = false, bool allLeft = false)
    {
        var x = 0d;
        for (var i = 0; i < widths.Count; i++)
        {
            c.Cell(x, y, widths[i], h, i < values.Count ? values[i] : "", header, header, fontSize,
                alignLeft: allLeft || (firstLeft && i == 0) || (lastLeft && i == widths.Count - 1));
            x += widths[i];
        }
    }

    private sealed class PdfCanvas
    {
        private readonly StringBuilder _ops = new();
        private readonly double _pageHeight;
        private readonly double _horizontalMargin;
        private readonly double _verticalMargin;
        private readonly double _horizontalScale;
        private readonly double _verticalScale;
        private readonly double _fontScale;

        public PdfCanvas(double pageWidth, double pageHeight, double margin,
            double horizontalScale, double verticalScale)
        {
            _pageHeight = pageHeight;
            _horizontalMargin = margin + (pageWidth - 2 * margin - DesignWidth * horizontalScale) / 2d;
            _verticalMargin = margin;
            _horizontalScale = horizontalScale;
            _verticalScale = verticalScale;
            _fontScale = Math.Min(horizontalScale, verticalScale);
            _ops.Append("0 G 0 g 0.45 w\n");
        }

        public string Content => _ops.ToString();

        public void SectionTitle(double y, string text) => Text(0, y, DesignWidth, 18, text, true, 10, true);

        public void Cell(double x, double y, double w, double h, string text, bool green, bool bold,
            double fontSize, bool rotate = false, bool alignRight = false, bool alignLeft = false)
        {
            var px = X(x);
            var py = Bottom(y, h);
            var pw = w * _horizontalScale;
            var ph = h * _verticalScale;
            if (green)
            {
                _ops.Append("0.84 0.94 0.84 rg ")
                    .Append(F(px)).Append(' ').Append(F(py)).Append(' ').Append(F(pw)).Append(' ').Append(F(ph)).Append(" re f\n0 g\n");
            }
            _ops.Append(F(px)).Append(' ').Append(F(py)).Append(' ').Append(F(pw)).Append(' ').Append(F(ph)).Append(" re S\n");
            Text(x, y, w, h, text, bold, fontSize, false, rotate, alignRight, alignLeft);
        }

        private void Text(double x, double y, double w, double h, string text, bool bold, double fontSize,
            bool noBorder, bool rotate = false, bool alignRight = false, bool alignLeft = false)
        {
            if (string.IsNullOrEmpty(text)) return;
            var lines = text.Replace("\r", "").Split('\n');
            var actualFont = Math.Max(2.5, fontSize * _fontScale);
            var lineHeight = actualFont * 1.18;
            var availableTextLength = rotate ? h * _verticalScale : w * _horizontalScale;
            var maxChars = Math.Max(1, (int)(availableTextLength / (actualFont * 0.52)));
            var shown = lines.Select(line => Truncate(Ascii(line), maxChars)).ToArray();

            if (rotate)
            {
                var value = shown.FirstOrDefault() ?? "";
                var tx = X(x) + w * _horizontalScale * 0.58;
                var ty = Bottom(y, h) + 3 * _verticalScale;
                _ops.Append("BT /").Append(bold ? "F2" : "F1").Append(' ').Append(F(actualFont)).Append(" Tf ")
                    .Append("0 1 -1 0 ").Append(F(tx)).Append(' ').Append(F(ty)).Append(" Tm (")
                    .Append(Escape(value)).Append(") Tj ET\n");
                return;
            }

            var cellLeft = X(x);
            var bottom = Bottom(y, h);
            var blockHeight = shown.Length * lineHeight;
            var baseline = bottom + (h * _verticalScale + blockHeight) / 2d - actualFont;
            foreach (var line in shown)
            {
                var approxWidth = line.Length * actualFont * 0.50;
                var tx = alignLeft
                    ? cellLeft + 4 * _horizontalScale
                    : alignRight
                        ? cellLeft + w * _horizontalScale - approxWidth - 4 * _horizontalScale
                        : cellLeft + (w * _horizontalScale - approxWidth) / 2d;
                _ops.Append("BT /").Append(bold ? "F2" : "F1").Append(' ').Append(F(actualFont)).Append(" Tf ")
                    .Append(F(tx)).Append(' ').Append(F(baseline)).Append(" Td (")
                    .Append(Escape(line)).Append(") Tj ET\n");
                baseline -= lineHeight;
            }
        }

        private double X(double x) => _horizontalMargin + x * _horizontalScale;
        private double Bottom(double y, double h) => _pageHeight - _verticalMargin - (y + h) * _verticalScale;
        private static string F(double value) => value.ToString("0.###", CultureInfo.InvariantCulture);
        private static string Truncate(string value, int max) => value.Length <= max
            ? value
            : max <= 3 ? value[..max] : value[..(max - 3)] + "...";
        private static string Ascii(string value) => new(value.Select(ch => ch is >= ' ' and <= '~' ? ch : '?').ToArray());
        private static string Escape(string value) => value.Replace("\\", "\\\\").Replace("(", "\\(").Replace(")", "\\)");
    }

    private static class MinimalPdf
    {
        public static byte[] Write(string content, double width, double height)
        {
            var streamBytes = Encoding.ASCII.GetBytes(content);
            var objects = new[]
            {
                "<< /Type /Catalog /Pages 2 0 R >>",
                "<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
                $"<< /Type /Page /Parent 2 0 R /MediaBox [0 0 {Fmt(width)} {Fmt(height)}] /Resources << /Font << /F1 4 0 R /F2 5 0 R >> >> /Contents 6 0 R >>",
                "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica /Encoding /WinAnsiEncoding >>",
                "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica-Bold /Encoding /WinAnsiEncoding >>",
                $"<< /Length {streamBytes.Length} >>\nstream\n{content}endstream"
            };

            using var output = new MemoryStream();
            WriteAscii(output, "%PDF-1.4\n%PWK\n");
            var offsets = new List<long> { 0 };
            for (var i = 0; i < objects.Length; i++)
            {
                offsets.Add(output.Position);
                WriteAscii(output, $"{i + 1} 0 obj\n{objects[i]}\nendobj\n");
            }

            var xref = output.Position;
            WriteAscii(output, $"xref\n0 {objects.Length + 1}\n0000000000 65535 f \n");
            foreach (var offset in offsets.Skip(1))
                WriteAscii(output, $"{offset:0000000000} 00000 n \n");
            WriteAscii(output, $"trailer\n<< /Size {objects.Length + 1} /Root 1 0 R >>\nstartxref\n{xref}\n%%EOF\n");
            return output.ToArray();
        }

        private static string Fmt(double value) => value.ToString("0.##", CultureInfo.InvariantCulture);
        private static void WriteAscii(Stream stream, string value)
        {
            var bytes = Encoding.ASCII.GetBytes(value);
            stream.Write(bytes, 0, bytes.Length);
        }
    }
}
