using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ClosedXML.Excel;

namespace SubPanamon.Pages.PWKActual;

/// <summary>
/// Membuat satu lembar Excel PWK dari data yang sama dengan tampilan/PDF.
/// Workbook sengaja tidak membaca database sendiri agar hasil ekspor selalu
/// mengikuti state IndexModel yang sudah memuat snapshot PWK atau data sumber.
/// </summary>
internal static class PwKExcelGenerator
{
    private const string HeaderGreen = "#D9F2D9";
    private const string InputYellow = "#FFFBE6";
    private const string White = "#FFFFFF";
    private const string Black = "#000000";

    public static byte[] Create(IndexModel model)
    {
        ArgumentNullException.ThrowIfNull(model);

        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add("PWK Actual");
        sheet.ShowGridLines = false;

        var orderedReasons = IndexModel.KategoriList
            .SelectMany(model.ReasonsOf)
            .ToList();
        var totalColumns = Math.Max(40, orderedReasons.Count + 2);

        for (var column = 1; column <= totalColumns; column++)
            sheet.Column(column).Width = 3.5;

        BuildHeader(sheet, model, totalColumns);
        BuildContext(sheet, model, totalColumns, 5);
        BuildGivenTime(sheet, model, totalColumns, 8);
        BuildLossTime(sheet, model, orderedReasons, totalColumns, 18);

        var productionStart = 37;
        var productionEnd = BuildProduction(sheet, model, totalColumns, productionStart);
        var notesEnd = BuildNotes(sheet, model, totalColumns, productionEnd + 2);
        var summaryEnd = BuildSummary(sheet, model, totalColumns, notesEnd + 2);

        var used = sheet.Range(1, 1, summaryEnd, totalColumns);
        used.Style.Font.FontName = "Arial";
        used.Style.Font.FontSize = 8;
        used.Style.Font.FontColor = XLColor.FromHtml(Black);
        used.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;

        sheet.SheetView.FreezeRows(6);
        sheet.PageSetup.PageOrientation = XLPageOrientation.Portrait;
        sheet.PageSetup.PaperSize = XLPaperSize.A4Paper;
        sheet.PageSetup.PagesWide = 1;
        sheet.PageSetup.PagesTall = 1;
        sheet.PageSetup.Margins.Top = 0.12;
        sheet.PageSetup.Margins.Bottom = 0.12;
        sheet.PageSetup.Margins.Left = 0.12;
        sheet.PageSetup.Margins.Right = 0.12;
        sheet.PageSetup.PrintAreas.Add(1, 1, summaryEnd, totalColumns);

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    private static void BuildHeader(IXLWorksheet sheet, IndexModel model, int totalColumns)
    {
        var titleEnd = Math.Min(6, totalColumns);
        MergeValue(sheet, 1, 1, 3, titleEnd,
            "PENGAWASAN WAKTU KERJA\n(TIME CONTROL)\nFOR LINE",
            header: true);

        var scheduleStart = titleEnd + 1;
        var scheduleEnd = Math.Min(24, totalColumns);
        var schedule = EqualSpans(scheduleStart, scheduleEnd, 6);
        var labels = new[] { "HARI", "SEN-KAM", "JUMAT", "SHIFT 1", "SHIFT 2", "SHIFT 3" };
        var values = new[]
        {
            "A : WORKING TIME",
            model.HdrNonShift.ToString(),
            model.HdrNonShiftJumat.ToString(),
            model.HdrShift1.ToString(),
            model.HdrShift2.ToString(),
            model.HdrShift3.ToString()
        };

        for (var i = 0; i < schedule.Count; i++)
        {
            MergeValue(sheet, 1, schedule[i].Start, 1, schedule[i].End, labels[i], header: true);
            MergeValue(sheet, 2, schedule[i].Start, 3, schedule[i].End, values[i]);
        }

        if (scheduleEnd < totalColumns)
        {
            var approval = EqualSpans(scheduleEnd + 1, totalColumns, 4);
            var approvalLabels = new[] { "MANAGER", "S-CHIEF", "G.CHIEF", "LEADER" };
            for (var i = 0; i < approval.Count; i++)
            {
                MergeValue(sheet, 1, approval[i].Start, 1, approval[i].End, approvalLabels[i], header: true);
                MergeValue(sheet, 2, approval[i].Start, 3, approval[i].End, "");
            }
        }

        sheet.Rows(1, 3).Height = 17;
        sheet.Row(2).Height = 20;
        sheet.Row(3).Height = 20;
    }

    private static void BuildContext(IXLWorksheet sheet, IndexModel model, int totalColumns, int row)
    {
        var spans = EqualSpans(1, totalColumns, 7);
        var labels = new[] { "TANGGAL", "LINE", "GROUP", "MACHINE CODE", "SHIFT", "JUMLAH OPR", "WAKTU KERJA (A)" };
        var values = new object[]
        {
            model.Tanggal,
            IndexModel.LineName,
            model.GroupName ?? "",
            model.MachineCode,
            model.Shift ?? "",
            model.JumlahOpr,
            model.WorkingTime
        };

        for (var i = 0; i < spans.Count; i++)
        {
            MergeValue(sheet, row, spans[i].Start, row, spans[i].End, labels[i], header: true);
            var valueCell = MergeValue(sheet, row + 1, spans[i].Start, row + 1, spans[i].End, values[i]);
            if (i == 0) valueCell.Style.DateFormat.Format = "dd-mm-yyyy";
            if (i is 5 or 6) valueCell.Style.NumberFormat.Format = "#,##0";
        }
    }

    private static void BuildGivenTime(IXLWorksheet sheet, IndexModel model, int totalColumns, int titleRow)
    {
        SectionTitle(sheet, titleRow, totalColumns, "A. GIVEN TIME");
        var spans = EqualSpans(1, totalColumns, 6);
        var headers = new[] { "ITEM", "TERDAFTAR", "ABSENSI", "WAKTU KERJA", "LEMBUR", "TOTAL" };
        for (var i = 0; i < spans.Count; i++)
            MergeValue(sheet, titleRow + 1, spans[i].Start, titleRow + 1, spans[i].End, headers[i], header: true);

        var firstDataRow = titleRow + 2;
        var lastDataRow = firstDataRow + model.Given.Count - 1;
        var workingTimeCell = MergeValue(sheet, firstDataRow, spans[3].Start, lastDataRow, spans[3].End, model.WorkingTime);
        workingTimeCell.Style.NumberFormat.Format = "#,##0";
        var overtimeCell = MergeValue(sheet, firstDataRow, spans[4].Start, lastDataRow, spans[4].End, model.SharedLembur, editable: true);
        overtimeCell.Style.NumberFormat.Format = "#,##0";
        for (var index = 0; index < model.Given.Count; index++)
        {
            var row = firstDataRow + index;
            var item = model.Given[index];
            MergeValue(sheet, row, spans[0].Start, row, spans[0].End, item.Item ?? "", alignLeft: true);
            MergeValue(sheet, row, spans[1].Start, row, spans[1].End, item.Terdaftar, editable: true);
            MergeValue(sheet, row, spans[2].Start, row, spans[2].End, item.Absensi, editable: true);
        }

        var totalRow = firstDataRow + model.Given.Count;
        MergeValue(sheet, totalRow, spans[0].Start, totalRow, spans[0].End, "TOTAL", header: true);
        var workerTerms = Enumerable.Range(0, model.Given.Count)
            .Select(offset =>
                $"MAX(0,{Address(sheet, firstDataRow + offset, spans[1].Start)})")
            .ToArray();
        var workerMultiplierFormula = $"MAX(1,SUM({string.Join(",", workerTerms)}))";
        for (var columnIndex = 1; columnIndex < spans.Count - 1; columnIndex++)
        {
            var totalCell = MergeValue(sheet, totalRow, spans[columnIndex].Start, totalRow, spans[columnIndex].End, 0, header: true);
            totalCell.Style.NumberFormat.Format = "#,##0";
            if (columnIndex is 3 or 4)
            {
                totalCell.FormulaA1 =
                    $"={Address(sheet, firstDataRow, spans[columnIndex].Start)}*{workerMultiplierFormula}";
            }
            else
            {
                var addresses = Enumerable.Range(0, model.Given.Count)
                    .Select(offset => Address(sheet, firstDataRow + offset, spans[columnIndex].Start));
                totalCell.FormulaA1 = $"=SUM({string.Join(",", addresses)})";
            }
        }

        var grandTotalCell = MergeValue(
            sheet,
            firstDataRow,
            spans[5].Start,
            totalRow,
            spans[5].End,
            0,
            bold: true);
        grandTotalCell.Style.NumberFormat.Format = "#,##0";
        grandTotalCell.FormulaA1 =
            $"={Address(sheet, totalRow, spans[3].Start)}+{Address(sheet, totalRow, spans[4].Start)}";
    }

    private static void BuildLossTime(
        IXLWorksheet sheet,
        IndexModel model,
        IReadOnlyList<IndexModel.ReasonItem> reasons,
        int totalColumns,
        int titleRow)
    {
        var firstReasonColumn = 2;
        var lastReasonColumn = firstReasonColumn + reasons.Count - 1;
        var totalStartColumn = lastReasonColumn + 1;
        var lastColumn = Math.Max(totalColumns, totalStartColumn);
        SectionTitle(sheet, titleRow, lastColumn, "B. LOSE TIME");

        var groupRow = titleRow + 1;
        var reasonRow = titleRow + 2;
        var idRow = titleRow + 3;
        MergeValue(sheet, groupRow, 1, reasonRow, 1, "ITEM TIME LOSS", header: true);
        MergeValue(sheet, groupRow, totalStartColumn, reasonRow, lastColumn, "TOTAL", header: true);

        var reasonColumn = 2;
        foreach (var category in IndexModel.KategoriList)
        {
            var categoryReasons = reasons.Where(r => r.Kategori == category).ToList();
            if (categoryReasons.Count == 0) continue;

            MergeValue(
                sheet,
                groupRow,
                reasonColumn,
                groupRow,
                reasonColumn + categoryReasons.Count - 1,
                category,
                header: true);

            foreach (var reason in categoryReasons)
            {
                var reasonCell = MergeValue(sheet, reasonRow, reasonColumn, reasonRow, reasonColumn, reason.Name ?? "", header: true);
                reasonCell.Style.Alignment.TextRotation = 90;
                MergeValue(sheet, idRow, reasonColumn, idRow, reasonColumn, reason.Id, header: true);
                sheet.Column(reasonColumn).Width = 3.4;
                reasonColumn++;
            }
        }

        MergeValue(sheet, idRow, 1, idRow, 1, "JAM", header: true);
        MergeValue(sheet, idRow, totalStartColumn, idRow, lastColumn, "", header: true);
        sheet.Column(1).Width = 18;
        sheet.Row(reasonRow).Height = 105;

        var dataRow = idRow + 1;
        var jamRows = model.JamRows();
        foreach (var jam in jamRows)
        {
            MergeValue(sheet, dataRow, 1, dataRow, 1, string.IsNullOrEmpty(jam) ? "s/d" : jam);
            for (var i = 0; i < reasons.Count; i++)
                MergeValue(sheet, dataRow, i + 2, dataRow, i + 2, model.N(model.Cell(jam, reasons[i].Id)));

            var rowTotal = MergeValue(sheet, dataRow, totalStartColumn, dataRow, lastColumn, 0);
            if (reasons.Count > 0)
                rowTotal.FormulaA1 = $"=SUM({Address(sheet, dataRow, firstReasonColumn)}:{Address(sheet, dataRow, lastReasonColumn)})";
            dataRow++;
        }

        MergeValue(sheet, dataRow, 1, dataRow, 1, "TOTAL", header: true);
        for (var i = 0; i < reasons.Count; i++)
            MergeValue(sheet, dataRow, i + 2, dataRow, i + 2, model.N(model.ColTotal(reasons[i].Id)), header: true);

        var grandTotal = MergeValue(sheet, dataRow, totalStartColumn, dataRow, lastColumn, model.TotalLoss, header: true);
        if (reasons.Count > 0)
            grandTotal.FormulaA1 = $"=SUM({Address(sheet, dataRow, firstReasonColumn)}:{Address(sheet, dataRow, lastReasonColumn)})";
    }

    private static int BuildProduction(IXLWorksheet sheet, IndexModel model, int totalColumns, int titleRow)
    {
        SectionTitle(sheet, titleRow, totalColumns, "C. HASIL PRODUKSI");
        var spans = EqualSpans(1, totalColumns, 12);
        var firstHeaderRow = titleRow + 1;
        var secondHeaderRow = titleRow + 2;

        var singleHeaders = new Dictionary<int, string>
        {
            [0] = "MODEL", [1] = "TIME", [2] = "NO. SERI AWAL", [3] = "NO. SERI AKHIR",
            [10] = "DEFECT QTY", [11] = "KETERANGAN"
        };
        foreach (var (index, label) in singleHeaders)
            MergeValue(sheet, firstHeaderRow, spans[index].Start, secondHeaderRow, spans[index].End, label, header: true);

        MergeValue(sheet, firstHeaderRow, spans[4].Start, firstHeaderRow, spans[6].End, "DAILY", header: true);
        MergeValue(sheet, firstHeaderRow, spans[7].Start, firstHeaderRow, spans[9].End, "ACCM", header: true);
        var subHeaders = new[] { "PLAN", "ACTUAL", "+/-", "PLAN", "ACTUAL", "+/-" };
        for (var i = 0; i < subHeaders.Length; i++)
        {
            var spanIndex = i + 4;
            MergeValue(sheet, secondHeaderRow, spans[spanIndex].Start, secondHeaderRow, spans[spanIndex].End, subHeaders[i], header: true);
        }

        var firstDataRow = secondHeaderRow + 1;
        var row = firstDataRow;
        if (model.Prods.Count == 0)
        {
            MergeValue(sheet, row, 1, row, totalColumns, "Belum ada data produksi untuk tanggal, line, dan shift ini.");
            row++;
        }
        else
        {
            foreach (var production in model.Prods)
            {
                MergeValue(sheet, row, spans[0].Start, row, spans[0].End, production.Model ?? "", alignLeft: true);
                MergeValue(sheet, row, spans[1].Start, row, spans[1].End, production.Time);

                var serialStart = MergeValue(sheet, row, spans[2].Start, row, spans[2].End, production.SeriAwal ?? "", editable: true);
                var serialEnd = MergeValue(sheet, row, spans[3].Start, row, spans[3].End, production.SeriAkhir ?? "", editable: true);
                serialStart.Style.NumberFormat.Format = "@";
                serialEnd.Style.NumberFormat.Format = "@";

                MergeValue(sheet, row, spans[4].Start, row, spans[4].End, production.DailyPlan, editable: true);
                MergeValue(sheet, row, spans[5].Start, row, spans[5].End, production.DailyActual, editable: true);
                var dailyDiff = MergeValue(sheet, row, spans[6].Start, row, spans[6].End, 0);
                dailyDiff.FormulaA1 = $"={Address(sheet, row, spans[5].Start)}-{Address(sheet, row, spans[4].Start)}";

                MergeValue(sheet, row, spans[7].Start, row, spans[7].End, production.AccmPlan);
                MergeValue(sheet, row, spans[8].Start, row, spans[8].End, production.AccmActual);
                var accmDiff = MergeValue(sheet, row, spans[9].Start, row, spans[9].End, 0);
                accmDiff.FormulaA1 = $"={Address(sheet, row, spans[8].Start)}-{Address(sheet, row, spans[7].Start)}";

                MergeValue(sheet, row, spans[10].Start, row, spans[10].End, production.Defect, editable: true);
                MergeValue(sheet, row, spans[11].Start, row, spans[11].End, production.Keterangan ?? "", editable: true, alignLeft: true);
                row++;
            }
        }

        MergeValue(sheet, row, spans[0].Start, row, spans[3].End, "TOTAL", header: true);
        if (model.Prods.Count > 0)
        {
            SetColumnTotal(sheet, row, spans[4], firstDataRow, row - 1);
            SetColumnTotal(sheet, row, spans[5], firstDataRow, row - 1);
            var diff = MergeValue(sheet, row, spans[6].Start, row, spans[6].End, 0, header: true);
            diff.FormulaA1 = $"={Address(sheet, row, spans[5].Start)}-{Address(sheet, row, spans[4].Start)}";
            MergeValue(sheet, row, spans[7].Start, row, spans[9].End, "", header: true);
            SetColumnTotal(sheet, row, spans[10], firstDataRow, row - 1);
        }
        else
        {
            for (var i = 4; i <= 10; i++)
                MergeValue(sheet, row, spans[i].Start, row, spans[i].End, i is 7 or 8 or 9 ? "" : 0, header: true);
        }
        MergeValue(sheet, row, spans[11].Start, row, spans[11].End, "", header: true);
        return row;
    }

    private static int BuildNotes(IXLWorksheet sheet, IndexModel model, int totalColumns, int titleRow)
    {
        SectionTitle(sheet, titleRow, totalColumns, "D. CATATAN MASALAH");
        var spans = EqualSpans(1, totalColumns, 4);
        var headers = new[] { "HAMBATAN", "ANALISA MASALAH", "TINDAKAN", "DURASI" };
        for (var i = 0; i < spans.Count; i++)
            MergeValue(sheet, titleRow + 1, spans[i].Start, titleRow + 1, spans[i].End, headers[i], header: true);

        var notes = model.Catatan.Count > 0
            ? model.Catatan
            : new List<IndexModel.CatatanRow> { new() };
        var row = titleRow + 2;
        foreach (var note in notes)
        {
            MergeValue(sheet, row, spans[0].Start, row, spans[0].End, note.Hambatan ?? "", editable: true, alignLeft: true);
            MergeValue(sheet, row, spans[1].Start, row, spans[1].End, note.Analisa ?? "", editable: true, alignLeft: true);
            MergeValue(sheet, row, spans[2].Start, row, spans[2].End, note.Tindakan ?? "", editable: true, alignLeft: true);
            MergeValue(sheet, row, spans[3].Start, row, spans[3].End, note.Pic ?? "", editable: true);
            row++;
        }
        return row - 1;
    }

    private static int BuildSummary(IXLWorksheet sheet, IndexModel model, int totalColumns, int titleRow)
    {
        var spans = EqualSpans(1, totalColumns, 3);
        MergeValue(sheet, titleRow, spans[0].Start, titleRow + 2, spans[0].End,
            $"( J ) HASIL PROD\n{model.HasilProd:#,##0} pcs", alignLeft: true);
        MergeValue(sheet, titleRow, spans[1].Start, titleRow + 2, spans[1].End,
            $"( K ) AVAILABLE WORKING TIME\n( A + Lembur ) x Jumlah Operator\n({model.WorkingTime:#,##0} + {model.SharedLembur:#,##0}) x {model.JumlahOpr:#,##0} = {model.AvailableWT:#,##0}", alignLeft: true);
        MergeValue(sheet, titleRow, spans[2].Start, titleRow + 2, spans[2].End,
            $"PROD / HEAD / HOUR\n( J x 60 ) / ( K )\n({model.HasilProd:#,##0} x 60) / {model.AvailableWT:#,##0} = {model.ProdHeadHour:#,##0.##}", alignLeft: true);
        sheet.Rows(titleRow, titleRow + 2).Height = 16;
        return titleRow + 2;
    }

    private static void SetColumnTotal(IXLWorksheet sheet, int totalRow, Span span, int firstDataRow, int lastDataRow)
    {
        var total = MergeValue(sheet, totalRow, span.Start, totalRow, span.End, 0, header: true);
        total.FormulaA1 = $"=SUM({Address(sheet, firstDataRow, span.Start)}:{Address(sheet, lastDataRow, span.Start)})";
    }

    private static IXLCell SectionTitle(IXLWorksheet sheet, int row, int lastColumn, string text) =>
        MergeValue(sheet, row, 1, row, lastColumn, text, alignLeft: true, bold: true, withBorder: false);

    private static IXLCell MergeValue(
        IXLWorksheet sheet,
        int firstRow,
        int firstColumn,
        int lastRow,
        int lastColumn,
        object? value,
        bool header = false,
        bool editable = false,
        bool alignLeft = false,
        bool bold = false,
        bool withBorder = true)
    {
        var range = sheet.Range(firstRow, firstColumn, lastRow, lastColumn);
        if (firstRow != lastRow || firstColumn != lastColumn) range.Merge();
        var cell = sheet.Cell(firstRow, firstColumn);
        cell.Value = XLCellValue.FromObject(value ?? "");

        range.Style.Alignment.Horizontal = alignLeft
            ? XLAlignmentHorizontalValues.Left
            : XLAlignmentHorizontalValues.Center;
        range.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
        range.Style.Alignment.WrapText = true;
        range.Style.Font.Bold = header || bold;
        range.Style.Fill.BackgroundColor = XLColor.FromHtml(header ? HeaderGreen : editable ? InputYellow : White);

        if (withBorder)
        {
            range.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
            range.Style.Border.InsideBorder = XLBorderStyleValues.Thin;
            range.Style.Border.OutsideBorderColor = XLColor.FromHtml(Black);
            range.Style.Border.InsideBorderColor = XLColor.FromHtml(Black);
        }

        return cell;
    }

    private static string Address(IXLWorksheet sheet, int row, int column) =>
        sheet.Cell(row, column).Address.ToStringRelative();

    private static List<Span> EqualSpans(int startColumn, int endColumn, int count)
    {
        var result = new List<Span>(count);
        var width = endColumn - startColumn + 1;
        var baseSize = width / count;
        var remainder = width % count;
        var cursor = startColumn;
        for (var i = 0; i < count; i++)
        {
            var size = baseSize + (i < remainder ? 1 : 0);
            var end = cursor + size - 1;
            result.Add(new Span(cursor, end));
            cursor = end + 1;
        }
        return result;
    }

    private readonly record struct Span(int Start, int End);
}
