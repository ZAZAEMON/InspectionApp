using ClosedXML.Excel;
using System.IO;
using InspectionApp.Models;

namespace InspectionApp.Services
{
    public class ExcelExportService
    {
        private const int SnCol    = 1;   // A
        private const int ParamCol = 2;   // B
        private const int FirstCol = 3;   // C — first session column

        // ─── Generate a report Excel from queried inspection sessions ──────────────
        public string GenerateReport(string partNumberLabel, List<InspectionSession> sessions,
                                      string subfolder = "Product Audit")
        {
            var stamp  = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            var root   = Helpers.UserSettings.GetSavePath()
                         ?? Path.Combine(AppContext.BaseDirectory, "InspectionReports");
            var folder = Path.Combine(root, subfolder);
            Directory.CreateDirectory(folder);
            var safe     = string.Concat(partNumberLabel.Select(c => Path.GetInvalidFileNameChars().Contains(c) ? '_' : c));
            var filePath = Path.Combine(folder, $"Report_{safe}_{stamp}.xlsx");

            using var wb = new XLWorkbook();
            var ws = wb.Worksheets.Add("Inspection Report");

            // Every part has its own parameter set (different names AND counts), so a single
            // shared "Parameter" column misaligns readings — each part's values get jammed onto
            // another part's rows just because they share a serial number. Instead, group the
            // sessions by part and stack one self-contained block per part down the sheet, each
            // carrying that part's own parameters as rows and its own sessions as columns.
            var partGroups = sessions
                .GroupBy(s => s.PartNumber)        // LINQ keeps first-appearance order (= chronological)
                .Select(g => g.ToList())
                .ToList();

            bool multiPart     = partGroups.Count > 1;
            int  maxSessionCols = partGroups.Count == 0 ? 1 : partGroups.Max(g => g.Count);
            int  totalCols      = FirstCol - 1 + Math.Max(maxSessionCols, 1);

            // ── Title row ─────────────────────────────────────────────────────────
            int row = 1;
            ws.Range(row, SnCol, row, totalCols).Merge().Value
                = $"INSPECTION REPORT — Part: {partNumberLabel}";
            ws.Cell(row, SnCol).Style
                .Font.SetBold(true)
                .Font.SetFontSize(16)
                .Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center)
                .Fill.SetBackgroundColor(XLColor.FromHtml("#1E3A5F"))
                .Font.SetFontColor(XLColor.White);
            row++;

            // ── Info row ──────────────────────────────────────────────────────────
            ws.Range(row, SnCol, row, totalCols).Merge().Value
                = $"Generated: {DateTime.Now:dd-MM-yyyy HH:mm}   |   Sessions: {sessions.Count}";
            ws.Cell(row, SnCol).Style
                .Font.SetItalic(true)
                .Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center)
                .Fill.SetBackgroundColor(XLColor.FromHtml("#F5F7FA"))
                .Font.SetFontColor(XLColor.FromHtml("#546E7A"));
            row++;

            // ── One stacked block per part ────────────────────────────────────────
            foreach (var group in partGroups)
            {
                row++;   // blank spacer row separating the band above from this block
                row = WritePartBlock(ws, group, multiPart, row);
            }

            // ── Column widths ─────────────────────────────────────────────────────
            ws.Columns().AdjustToContents();
            ws.Column(SnCol).Width    = Math.Max(ws.Column(SnCol).Width, 6);
            ws.Column(ParamCol).Width = Math.Max(ws.Column(ParamCol).Width, 40);
            for (int c = 0; c < maxSessionCols; c++)
                ws.Column(FirstCol + c).Width = Math.Max(ws.Column(FirstCol + c).Width, 22);

            wb.SaveAs(filePath);
            return filePath;
        }

        // Writes one part's block — optional part banner, a header row, then that part's own
        // parameter rows with each session as a column. Returns the next free row beneath it.
        private static int WritePartBlock(IXLWorksheet ws, List<InspectionSession> sessions,
                                          bool showBanner, int startRow)
        {
            int sessionCols = Math.Max(sessions.Count, 1);
            int lastCol     = FirstCol - 1 + sessionCols;
            int row         = startRow;

            // Part banner — only in multi-part reports, to label which part the block belongs to.
            if (showBanner)
            {
                int sc = sessions.Count;
                ws.Range(row, SnCol, row, lastCol).Merge().Value
                    = $"Part: {sessions.FirstOrDefault()?.PartNumber ?? ""}    ({sc} session{(sc == 1 ? "" : "s")})";
                ws.Cell(row, SnCol).Style
                    .Font.SetBold(true)
                    .Font.SetFontSize(12)
                    .Alignment.SetHorizontal(XLAlignmentHorizontalValues.Left)
                    .Alignment.SetVertical(XLAlignmentVerticalValues.Center)
                    .Fill.SetBackgroundColor(XLColor.FromHtml("#2A4E7A"))
                    .Font.SetFontColor(XLColor.White)
                    .Border.SetOutsideBorder(XLBorderStyleValues.Thin);
                ws.Row(row).Height = 22;
                row++;
            }

            // Header row: S.no | Parameter | one column per session of this part.
            int headerRow = row;
            ApplyHeaderCell(ws.Cell(headerRow, SnCol),    "S.no");
            ApplyHeaderCell(ws.Cell(headerRow, ParamCol), "Parameter");
            for (int s = 0; s < sessions.Count; s++)
            {
                var sess = sessions[s];
                var cell = ws.Cell(headerRow, FirstCol + s);
                cell.Value = $"{sess.SubmittedAt:dd-MM-yyyy HH:mm}\n{sess.Shift}\n{sess.Auditor}";
                cell.Style
                    .Font.SetBold(true)
                    .Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center)
                    .Alignment.SetVertical(XLAlignmentVerticalValues.Center)
                    .Alignment.SetWrapText(true)
                    .Fill.SetBackgroundColor(XLColor.FromHtml("#37474F"))
                    .Font.SetFontColor(XLColor.White)
                    .Border.SetOutsideBorder(XLBorderStyleValues.Thin);
            }
            ws.Row(headerRow).Height = 60;
            row++;

            // This part's own parameters (union across its sessions, by serial number).
            var paramMap = sessions
                .SelectMany(s => s.Readings)
                .GroupBy(r => r.SerialNumber)
                .OrderBy(g => g.Key)
                .Select(g => new { SerialNumber = g.Key, ParameterName = g.First().ParameterName })
                .ToList();

            for (int i = 0; i < paramMap.Count; i++)
            {
                int r       = row + i;
                var bgColor = i % 2 == 0 ? XLColor.White : XLColor.FromHtml("#F8FAFB");
                int sn      = paramMap[i].SerialNumber;

                ws.Cell(r, SnCol).Value    = sn;
                ws.Cell(r, ParamCol).Value = paramMap[i].ParameterName;
                for (int s = 0; s < sessions.Count; s++)
                {
                    var reading = sessions[s].Readings.FirstOrDefault(rd => rd.SerialNumber == sn);
                    ws.Cell(r, FirstCol + s).Value = reading?.Reading ?? "";
                }

                ws.Range(r, SnCol, r, lastCol).Style
                    .Fill.SetBackgroundColor(bgColor)
                    .Border.SetOutsideBorder(XLBorderStyleValues.Thin)
                    .Border.SetInsideBorder(XLBorderStyleValues.Hair)
                    .Alignment.SetVertical(XLAlignmentVerticalValues.Center);
            }

            return row + paramMap.Count;
        }

        private static void ApplyHeaderCell(IXLCell cell, string text)
        {
            cell.Value = text;
            cell.Style
                .Font.SetBold(true)
                .Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center)
                .Alignment.SetVertical(XLAlignmentVerticalValues.Center)
                .Fill.SetBackgroundColor(XLColor.FromHtml("#37474F"))
                .Font.SetFontColor(XLColor.White)
                .Border.SetOutsideBorder(XLBorderStyleValues.Thin);
        }
    }
}
