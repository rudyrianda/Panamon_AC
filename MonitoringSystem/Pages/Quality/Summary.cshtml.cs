using ClosedXML.Excel;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Data.SqlClient;

namespace MonitoringSystem.Pages.Quality
{
    public class SummaryModel : PageModel
    {
        public string connectionString = "Server=10.83.33.103;User Id=sa;Password=sa;Database=PROMOSYS;Trusted_Connection=False;TrustServerCertificate=True;Encrypt=False;MultipleActiveResultSets=True";

        // Sub-kolom per bulan. Qty = semua defect, Quality = disposisi Change Product/Dispose,
        // Report = sudah dikonfirmasi leader, Mfg/Repair = disposisi Repair.
        public static readonly string[] MonthColumns = { "Qty.", "Quality", "Report", "Mfg/ Repair" };

        [BindProperty(SupportsGet = true)]
        public string? Line { get; set; }

        [BindProperty(SupportsGet = true)]
        public int? Fy { get; set; }

        public QualityDefectCatalog.LineDefinition LineDef { get; private set; } = QualityDefectCatalog.CU;
        public int FiscalYear { get; private set; }
        public List<DateTime> Months { get; private set; } = new();
        public List<StationBlock> Stations { get; private set; } = new();
        public MonthCell[] GrandTotal { get; private set; } = Array.Empty<MonthCell>();
        public string errorMessage = "";

        public void OnGet()
        {
            LoadData();
        }

        public IActionResult OnGetExport()
        {
            LoadData();

            using var workbook = new XLWorkbook();
            var ws = workbook.Worksheets.Add($"Defect {LineDef.Code} FY{FiscalYear}");

            int monthStartCol = 5;
            int lastCol = monthStartCol + Months.Count * MonthColumns.Length;

            ws.Cell(1, 1).Value = $"Summary Defect Data Table - {LineDef.Code} Line - FY{FiscalYear}";
            ws.Range(1, 1, 1, lastCol).Merge().Style.Font.SetBold().Font.SetFontSize(14);

            ws.Cell(2, 1).Value = "Station";
            ws.Range(2, 1, 3, 2).Merge();
            ws.Cell(2, 3).Value = "Item Defect";
            ws.Range(2, 3, 3, 3).Merge();
            ws.Cell(2, 4).Value = "1. Repair\n2. Scrap/ Dispose/ Return to Supplier";
            ws.Range(2, 4, 3, 4).Merge();
            for (int m = 0; m < Months.Count; m++)
            {
                int col = monthStartCol + m * MonthColumns.Length;
                ws.Cell(2, col).Value = Months[m].ToString("MMM-yy");
                ws.Range(2, col, 2, col + MonthColumns.Length - 1).Merge();
                for (int c = 0; c < MonthColumns.Length; c++)
                {
                    ws.Cell(3, col + c).Value = MonthColumns[c];
                }
            }
            ws.Cell(2, lastCol).Value = "Total";
            ws.Range(2, lastCol, 3, lastCol).Merge();

            var header = ws.Range(2, 1, 3, lastCol);
            header.Style.Font.SetBold()
                .Fill.SetBackgroundColor(XLColor.FromHtml("#C9B79C"))
                .Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center)
                .Alignment.SetVertical(XLAlignmentVerticalValues.Center)
                .Alignment.SetWrapText(true);

            int row = 4;
            foreach (var station in Stations)
            {
                int stationStart = row;
                foreach (var category in station.Categories)
                {
                    int categoryStart = row;
                    foreach (var item in category.Items)
                    {
                        ws.Cell(row, 3).Value = item.Item;
                        ws.Cell(row, 4).Value = item.Disposition;
                        WriteMonthCells(ws, row, monthStartCol, item.Cells);
                        ws.Cell(row, lastCol).Value = item.Cells.Sum(c => c.Qty);
                        row++;
                    }
                    ws.Cell(categoryStart, 2).Value = category.Label;
                    ws.Range(categoryStart, 2, row - 1, 2).Merge().Style.Alignment.SetTextRotation(90);
                }
                ws.Cell(stationStart, 1).Value = station.Label;
                ws.Range(stationStart, 1, row - 1, 1).Merge().Style.Alignment.SetTextRotation(90);
            }

            ws.Cell(row, 1).Value = "TOTAL";
            ws.Range(row, 1, row, 4).Merge();
            WriteMonthCells(ws, row, monthStartCol, GrandTotal);
            ws.Cell(row, lastCol).Value = GrandTotal.Sum(c => c.Qty);
            ws.Range(row, 1, row, lastCol).Style.Font.SetBold().Fill.SetBackgroundColor(XLColor.FromHtml("#E7E1D6"));

            if (row > 4)
            {
                ws.Range(4, 1, row, 2).Style.Font.SetBold()
                    .Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center)
                    .Alignment.SetVertical(XLAlignmentVerticalValues.Center);
                for (int m = 0; m < Months.Count; m++)
                {
                    int col = monthStartCol + m * MonthColumns.Length;
                    ws.Range(4, col, row - 1, col).Style.Fill.SetBackgroundColor(XLColor.FromHtml("#E8E36A"));
                }
            }

            var table = ws.Range(2, 1, row, lastCol);
            table.Style.Border.SetOutsideBorder(XLBorderStyleValues.Thin)
                .Border.SetInsideBorder(XLBorderStyleValues.Thin);
            ws.Range(4, monthStartCol, row, lastCol).Style.Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);

            ws.Column(1).Width = 6;
            ws.Column(2).Width = 6;
            ws.Column(3).Width = 40;
            ws.Column(4).Width = 16;
            ws.Columns(monthStartCol, lastCol).Width = 8;
            ws.SheetView.Freeze(3, 4);

            using var stream = new MemoryStream();
            workbook.SaveAs(stream);
            return File(stream.ToArray(),
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                $"Summary_Defect_{LineDef.Code}_FY{FiscalYear}.xlsx");
        }

        private static void WriteMonthCells(IXLWorksheet ws, int row, int monthStartCol, MonthCell[] cells)
        {
            for (int m = 0; m < cells.Length; m++)
            {
                int col = monthStartCol + m * MonthColumns.Length;
                var values = cells[m].Values;
                for (int c = 0; c < values.Length; c++)
                {
                    if (values[c] > 0)
                    {
                        ws.Cell(row, col + c).Value = values[c];
                    }
                }
            }
        }

        private void LoadData()
        {
            LineDef = QualityDefectCatalog.ResolveLine(Line);
            Line = LineDef.Code;
            FiscalYear = Fy ?? QualityDefectCatalog.FiscalYearOf(DateTime.Now);
            Months = QualityDefectCatalog.FiscalMonths(FiscalYear).ToList();

            var fromDate = Months.First();
            var toDate = fromDate.AddYears(1);
            var rawRows = new List<RawRow>();

            try
            {
                using (SqlConnection connection = new SqlConnection(connectionString))
                {
                    connection.Open();

                    string getDefects = @"
                    SELECT
                        YEAR(SDate) AS YearNumber,
                        MONTH(SDate) AS MonthNumber,
                        Station,
                        Cause,
                        Detail,
                        ActionDefect,
                        COUNT(*) AS DefectCount,
                        SUM(CASE WHEN LTRIM(RTRIM(ISNULL(ConfirmByLeader, ''))) <> '' THEN 1 ELSE 0 END) AS ConfirmedCount
                    FROM
                        NG_RPTS
                    WHERE
                        MachineCode = @MachineCode
                        AND SDate >= @FromDate
                        AND SDate < @ToDate
                    GROUP BY
                        YEAR(SDate), MONTH(SDate), Station, Cause, Detail, ActionDefect;";

                    using (SqlCommand command = new SqlCommand(getDefects, connection))
                    {
                        command.Parameters.AddWithValue("@MachineCode", LineDef.MachineCode);
                        command.Parameters.AddWithValue("@FromDate", fromDate);
                        command.Parameters.AddWithValue("@ToDate", toDate);
                        using (SqlDataReader reader = command.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                rawRows.Add(new RawRow
                                {
                                    MonthIndex = (reader.GetInt32(0) - fromDate.Year) * 12 + reader.GetInt32(1) - fromDate.Month,
                                    Station = reader.IsDBNull(2) ? null : reader.GetString(2),
                                    Cause = reader.IsDBNull(3) ? null : reader.GetString(3),
                                    Detail = reader.IsDBNull(4) ? null : reader.GetString(4),
                                    Action = reader.IsDBNull(5) ? null : reader.GetString(5),
                                    Quantity = reader.GetInt32(6),
                                    Confirmed = reader.GetInt32(7)
                                });
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                errorMessage = "Database error: " + ex.Message;
                Console.WriteLine(errorMessage);
            }

            var stationOrder = LineDef.Stations.Select(s => s.Key).Append(QualityDefectCatalog.OthersKey).ToList();
            var categoryOrder = LineDef.Categories.Select(c => c.Key).ToList();

            Stations = rawRows
                .Where(r => r.MonthIndex >= 0 && r.MonthIndex < Months.Count)
                .Select(r => new
                {
                    Row = r,
                    Station = QualityDefectCatalog.ResolveStation(LineDef, r.Station),
                    Category = QualityDefectCatalog.ResolveCategory(LineDef, r.Cause, r.Detail),
                    Item = QualityDefectCatalog.ItemLabel(r.Detail, r.Cause)
                })
                .GroupBy(x => x.Station.Key)
                .OrderBy(g => stationOrder.IndexOf(g.Key))
                .Select(stationGroup => new StationBlock
                {
                    Label = stationGroup.First().Station.Label,
                    Categories = stationGroup
                        .GroupBy(x => x.Category.Key)
                        .OrderBy(g => categoryOrder.IndexOf(g.Key))
                        .Select(categoryGroup => new CategoryBlock
                        {
                            Label = categoryGroup.First().Category.Label,
                            Items = categoryGroup
                                .GroupBy(x => x.Item.ToUpperInvariant())
                                .Select(itemGroup => BuildItem(itemGroup.First().Item, itemGroup.Select(x => x.Row)))
                                .OrderByDescending(i => i.Cells.Sum(c => c.Qty))
                                .ThenBy(i => i.Item)
                                .ToList()
                        })
                        .ToList()
                })
                .ToList();

            GrandTotal = NewCells();
            foreach (var item in Stations.SelectMany(s => s.Categories).SelectMany(c => c.Items))
            {
                for (int m = 0; m < Months.Count; m++)
                {
                    GrandTotal[m].Add(item.Cells[m]);
                }
            }
        }

        private ItemRow BuildItem(string label, IEnumerable<RawRow> rows)
        {
            var item = new ItemRow { Item = label, Cells = NewCells() };
            int dispose = 0, repair = 0;
            foreach (var r in rows)
            {
                var cell = item.Cells[r.MonthIndex];
                cell.Qty += r.Quantity;
                cell.Report += r.Confirmed;
                if (QualityDefectCatalog.IsDispose(r.Action))
                {
                    cell.Quality += r.Quantity;
                    dispose += r.Quantity;
                }
                else if (QualityDefectCatalog.IsRepair(r.Action))
                {
                    cell.Repair += r.Quantity;
                    repair += r.Quantity;
                }
            }
            item.Disposition = dispose == 0 && repair == 0 ? "" : dispose > repair ? "Dispose" : "Repair";
            return item;
        }

        private MonthCell[] NewCells() => Enumerable.Range(0, Months.Count).Select(_ => new MonthCell()).ToArray();

        private class RawRow
        {
            public int MonthIndex { get; set; }
            public string? Station { get; set; }
            public string? Cause { get; set; }
            public string? Detail { get; set; }
            public string? Action { get; set; }
            public int Quantity { get; set; }
            public int Confirmed { get; set; }
        }

        public class StationBlock
        {
            public string Label { get; set; } = "";
            public List<CategoryBlock> Categories { get; set; } = new();
            public int RowCount => Categories.Sum(c => c.Items.Count);
        }

        public class CategoryBlock
        {
            public string Label { get; set; } = "";
            public List<ItemRow> Items { get; set; } = new();
        }

        public class ItemRow
        {
            public string Item { get; set; } = "";
            public string Disposition { get; set; } = "";
            public MonthCell[] Cells { get; set; } = Array.Empty<MonthCell>();
        }

        public class MonthCell
        {
            public int Qty { get; set; }
            public int Quality { get; set; }
            public int Report { get; set; }
            public int Repair { get; set; }

            public int[] Values => new[] { Qty, Quality, Report, Repair };

            public void Add(MonthCell other)
            {
                Qty += other.Qty;
                Quality += other.Quality;
                Report += other.Report;
                Repair += other.Repair;
            }
        }
    }
}
