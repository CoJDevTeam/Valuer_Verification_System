// Requires NuGet: QuestPDF (2024.3.0+)
using Microsoft.EntityFrameworkCore;
using QuestPDF.Elements.Table;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using Valuer_Verification_System.Data;
using Valuer_Verification_System.Models.Data;
using Valuer_Verification_System.Models.Notice;

namespace Valuer_Verification_System.Services.Notice
{
    public sealed class NoticePdfService : INoticePdfService
    {
        // ── Branding ──────────────────────────────────────────────────────────
        private const string GoldHex = "#e6b000";
        private const string BlackHex = "#111111";
        private const string LightGoldHex = "#fff8e0";

        private const string Para1 =
            "Notice is hereby given in terms of Section 78(5)(a) read together with Section 78(5)(b) " +
            "of the Local Government: Municipal Property Rates Act, 2004 (Act No. 6 of 2004), as amended " +
            "hereinafter referred to as the \u201cAct\u201d, that a Supplementary Valuation to the General " +
            "Valuation Roll for the property described below has been completed.";

        private const string Para3 =
            "The form for the lodging of a review is obtainable from the City\u2019s website: " +
            "https://eservices.joburg.org.za/Pages/Overview.aspx or at the following address: " +
            "Valuation Services: Administration, 1st Floor, East Wing, Jorissen Place, " +
            "66 Jorissen Street, Braamfontein.";

        private const string Para5 =
            "If a representative is appointed, proof of authorisation must be attached to this form.";

        static NoticePdfService() => QuestPDF.Settings.License = LicenseType.Community;

        // ── Public API ────────────────────────────────────────────────────────

        public byte[] GenerateNotice(ExtractRow mainRow, NoticeContext ctx, List<ExtractRow> splitRows)
        {
            bool isMulti = IsMultipurpose(splitRows);

            return Document.Create(c => c.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(18, Unit.Millimetre);

                page.Content().Column(col =>
                {
                    // ── HEADER ─────────────────────────────────────────────
                    col.Item().Row(h =>
                    {
                        h.RelativeItem(3).AlignMiddle().Column(logo =>
                        {
                            if (File.Exists(ctx.LogoPath))
                                logo.Item().Width(75, Unit.Millimetre).Image(ctx.LogoPath);
                            else
                                logo.Item().Text("City of Johannesburg").Bold().FontSize(13);
                        });
                        h.RelativeItem(2).AlignRight().Column(addr =>
                        {
                            foreach (var line in new[]
                            {
                                "City of Johannesburg",
                                "Finance: Valuation Services",
                                "Jorissen Place, 66 Jorissen Street",
                                "Braamfontein",
                                "www.joburg.org.za"
                            })
                                addr.Item().Text(t =>
                                {
                                    if (line.StartsWith("www"))
                                        t.Span(line).FontSize(7.5f).FontColor(GoldHex);
                                    else
                                        t.Span(line).FontSize(7.5f);
                                });
                        });
                    });

                    col.Item().PaddingTop(2, Unit.Millimetre)
                              .LineHorizontal(0.8f).LineColor(GoldHex);
                    col.Item().PaddingTop(5, Unit.Millimetre);

                    // ── OWNER ADDRESS ──────────────────────────────────────
                    col.Item().Column(ow =>
                    {
                        foreach (var line in new[]
                            { mainRow.Addr1, mainRow.Addr2, mainRow.Addr3, mainRow.Addr4, mainRow.Addr5 })
                        {
                            if (!string.IsNullOrWhiteSpace(line))
                                ow.Item().Text(line.Trim()).Bold().FontSize(9.5f);
                        }
                    });

                    col.Item().PaddingTop(5, Unit.Millimetre);

                    // ── LETTER DATE ────────────────────────────────────────
                    col.Item().AlignRight()
                              .Text(ctx.LetterDate.ToString("dd MMMM yyyy").ToUpperInvariant())
                              .Bold().FontSize(9.5f);

                    col.Item().PaddingTop(5, Unit.Millimetre);

                    // ── TITLE ──────────────────────────────────────────────
                    col.Item().AlignCenter()
                              .Text($"NOTICE OF A SUPPLEMENTARY VALUATION TO THE GENERAL VALUATION ROLL {ctx.GvRollYear}")
                              .Bold().FontSize(10.5f).Underline();
                    col.Item().PaddingTop(1, Unit.Millimetre).LineHorizontal(1f).LineColor(BlackHex);
                    col.Item().PaddingTop(5, Unit.Millimetre);

                    // ── BODY PARAGRAPHS ────────────────────────────────────
                    col.Item().Text(Para1).FontSize(9f).LineHeight(1.45f);
                    col.Item().PaddingTop(3, Unit.Millimetre);

                    col.Item().Text(t =>
                    {
                        t.DefaultTextStyle(x => x
    .FontSize(9.5f)
    .LineHeight(1.45f));
                        t.Span("An invitation is hereby made in terms of Section 78(5)(b) of the Act that any " +
                               "owner of the property who so desires may request a review with the Municipal Manager " +
                               "in respect of any matter reflected in the supplementary valuation within the period: ")
                         .FontSize(9f);
                        t.Span(ctx.ReviewOpenDate.ToString("dd MMMM yyyy").ToUpperInvariant() +
                               "  \u2013  " +
                               ctx.ReviewCloseDate.ToString("dd MMMM yyyy").ToUpperInvariant())
                         .Bold().FontSize(9f);
                    });

                    col.Item().PaddingTop(3, Unit.Millimetre);
                    col.Item().Text(Para3).FontSize(9f).LineHeight(1.45f);
                    col.Item().PaddingTop(3, Unit.Millimetre);

                    col.Item().Text(t =>
                    {
                        t.DefaultTextStyle(x => x
    .FontSize(9.5f)
    .LineHeight(1.45f));
                        t.Span("The completed review forms should be emailed to " +
                               "valuationenquiries@joburg.org.za or by hand to the Valuation Services: " +
                               "Administration office on or before 15:00 on ").FontSize(9f);
                        t.Span(ctx.ReviewCloseDate.ToString("dd MMMM yyyy").ToUpperInvariant())
                         .Bold().FontSize(9f);
                    });

                    col.Item().PaddingTop(3, Unit.Millimetre);
                    col.Item().Text(Para5).FontSize(9f).LineHeight(1.45f);
                    col.Item().PaddingTop(5, Unit.Millimetre);

                    // ── PROPERTY DETAILS TABLE ─────────────────────────────
                    col.Item().AlignCenter()
                              .Text("PROPERTY DETAILS THAT MAY BE REVIEWED:")
                              .Bold().FontSize(9.5f);
                    col.Item().PaddingTop(2, Unit.Millimetre);

                    if (isMulti)
                        col.Item().Element(c => BuildMultiPurposeTable(c, mainRow, splitRows));
                    else
                        col.Item().Element(c => BuildSingleTable(c, mainRow));

                    col.Item().PaddingTop(8, Unit.Millimetre);

                    // ── FOOTER ─────────────────────────────────────────────
                    col.Item().LineHorizontal(0.4f).LineColor(Colors.Grey.Lighten2);
                    col.Item().PaddingTop(3, Unit.Millimetre);
                    col.Item().Row(ft =>
                    {
                        ft.RelativeItem(3).Column(c =>
                        {
                            c.Item().Text("For further enquiries please contact:").FontSize(8f);
                            c.Item().Text("\u2022  Tel: 011-407-6622 / 011-407-6597").FontSize(8f);
                            c.Item().Text("\u2022  Email: valuationenquiries@joburg.org.za").FontSize(8f);
                        });
                        ft.RelativeItem().AlignRight()
                          .Text(Safe(mainRow.ValuationKey)).FontSize(8f);
                    });
                    col.Item().PaddingTop(4, Unit.Millimetre);
                    col.Item().Text("Municipal Valuer").Bold().FontSize(10f);
                });
            })).GeneratePdf();
        }

        // ── Single-property table (1 data row) ───────────────────────────────
        private static void BuildSingleTable(IContainer container, ExtractRow row)
        {
            container.Table(t =>
            {
                t.ColumnsDefinition(c =>
                {
                    c.RelativeColumn(2.2f);  // label
                    c.RelativeColumn(4.8f);  // value
                });

                void Row(string label, string value)
                {
                    t.Cell().Element(LabelCell).Text(label).Bold().FontSize(8.5f);
                    t.Cell().Element(ValueCell).Text(value).FontSize(8.5f);
                }

                Row("Property Description:", Safe(row.PropertyDesc));
                Row("Owner Name:", Safe(row.OwnerName));
                Row("Physical Address:", BuildPhysical(row));
                Row("Property Category:", Safe(row.PropertyCategory ?? row.CatDesc));
                Row("Market Value:", FormatMv(row.MarketValue));
                Row("Extent (m\u00b2):", FormatExtent(row.Extent ?? row.LegalArea));
                Row("Effective Date:", FormatDate(row.ValuationDate));
                Row("Remarks:", Safe(row.RemarkDesc ?? row.Remarks));
            });
        }

        // ── Multipurpose table (up to 4 split data rows) ─────────────────────
        private static void BuildMultiPurposeTable(IContainer container, ExtractRow mainRow, List<ExtractRow> splits)
        {
            container.Column(col =>
            {
                // Top block: property description + owner + physical address
                col.Item().Table(top =>
                {
                    top.ColumnsDefinition(c =>
                    {
                        c.RelativeColumn(2.2f);
                        c.RelativeColumn(4.8f);
                    });

                    void Row(string label, string val)
                    {
                        top.Cell().Element(LabelCell).Text(label).Bold().FontSize(8.5f);
                        top.Cell().Element(ValueCell).Text(val).FontSize(8.5f);
                    }

                    Row("Property Description:", Safe(mainRow.PropertyDesc));
                    Row("Owner Name:", Safe(mainRow.OwnerName));
                    Row("Physical Address:", BuildPhysical(mainRow));
                });

                col.Item().PaddingTop(3, Unit.Millimetre);
                col.Item().Text("Multipurpose — Valuation Splits:").Bold().FontSize(8.5f);
                col.Item().PaddingTop(2, Unit.Millimetre);

                // Split rows table — max 4 rows
                col.Item().Table(t =>
                {
                    t.ColumnsDefinition(c =>
                    {
                        c.RelativeColumn(0.6f);   // #
                        c.RelativeColumn(2.5f);   // Cat Code / Desc
                        c.RelativeColumn(1.5f);   // Cat Code
                        c.RelativeColumn(1.8f);   // Market Value
                        c.RelativeColumn(1.2f);   // Extent
                        c.RelativeColumn(1.4f);   // Effective Date
                    });

                    // Header
                    static IContainer HeaderCell(IContainer c) =>
                        c.Background(GoldHex).Padding(3, Unit.Millimetre).AlignCenter();

                    static void Hdr(ITableCellContainer cell, string text)
                    {
                        cell.Element(HeaderCell).Text(text).Bold().FontSize(7.5f).FontColor(BlackHex);
                    }

                    Hdr(t.Cell(), "#");
                    Hdr(t.Cell(), "Category Description");
                    Hdr(t.Cell(), "Cat Code");
                    Hdr(t.Cell(), "Market Value");
                    Hdr(t.Cell(), "Extent (m\u00b2)");
                    Hdr(t.Cell(), "Effective Date");

                    // Data rows (up to 4)
                    int rowNum = 0;
                    foreach (var split in splits.Take(4))
                    {
                        rowNum++;
                        bool shade = rowNum % 2 == 0;

                        static IContainer DataCell(IContainer c, bool shaded) =>
                            c.Background(shaded ? "#f9f3d9" : Colors.White)
                             .BorderBottom(0.3f).BorderColor(Colors.Grey.Lighten2)
                             .Padding(2.5f, Unit.Millimetre);

                        t.Cell().Element(c => DataCell(c, shade))
                                .Text(rowNum.ToString()).FontSize(8f).AlignCenter();
                        t.Cell().Element(c => DataCell(c, shade))
                                .Text(Safe(split.CatDesc)).FontSize(8f);
                        t.Cell().Element(c => DataCell(c, shade))
                                .Text(Safe(split.CatCode)).FontSize(8f);
                        t.Cell().Element(c => DataCell(c, shade))
                                .Text(FormatMv(split.MarketValue)).FontSize(8f).AlignRight();
                        t.Cell().Element(c => DataCell(c, shade))
                                .Text(FormatExtent(split.Extent ?? split.LegalArea)).FontSize(8f).AlignRight();
                        t.Cell().Element(c => DataCell(c, shade))
                                .Text(FormatDate(split.ValuationDate)).FontSize(8f).AlignCenter();
                    }
                });

                // Totals row
                var totalMv = splits.Take(4).Sum(r => r.MarketValue ?? 0);
                col.Item().PaddingTop(1, Unit.Millimetre)
                   .AlignRight()
                   .Text($"Total Market Value: {FormatMv(totalMv)}")
                   .Bold().FontSize(8.5f);
            });
        }

        // ── Preview ───────────────────────────────────────────────────────────

        public async Task<byte[]?> GeneratePreviewAsync(
            int batchId, NoticeContext ctx,
            Microsoft.EntityFrameworkCore.DbContext db, CancellationToken ct)
        {
            var vvsDb = (VvsDbContext)db;

            // Find the first sendable row
            var mainRow = await vvsDb.ExtractRows
                .AsNoTracking()
                .Where(r => r.BatchId == batchId
                         && r.SendNotice == "Y"
                         && r.Decision == VerificationDecision.Yes)
                .OrderBy(r => r.ValuationKey)
                .FirstOrDefaultAsync(ct);

            if (mainRow is null) return null;

            // Get all splits for this ValuationKey
            var splits = await vvsDb.ExtractRows
                .AsNoTracking()
                .Where(r => r.BatchId == batchId
                         && r.ValuationKey == mainRow.ValuationKey
                         && r.SendNotice == "Y"
                         && r.Decision == VerificationDecision.Yes)
                .OrderBy(r => r.ValuationSplitNo)
                .ToListAsync(ct);

            if (!splits.Any()) splits = new List<ExtractRow> { mainRow };

            return GenerateNotice(mainRow, ctx, splits);
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        private static bool IsMultipurpose(List<ExtractRow> rows) =>
            rows.Count > 1 ||
            rows.Any(r =>
                string.Equals(r.CatDesc, "Multipurpose", StringComparison.OrdinalIgnoreCase) &&
                !string.IsNullOrWhiteSpace(r.ValuationSplitInd));

        private static string Safe(string? s) => string.IsNullOrWhiteSpace(s) ? "" : s.Trim();
        private static string FormatMv(decimal? v) => v.HasValue ? $"R {v.Value:N0}" : "R 0";
        private static string FormatDate(DateOnly? d) => d.HasValue ? d.Value.ToString("yyyy/MM/dd") : "";
        private static string BuildPhysical(ExtractRow r) =>
            Safe(r.PhysicalAddress ?? r.PremiseAddress ?? r.LisStreetAddress) is { Length: > 0 } s ? s : "XXXX";
        private static string FormatExtent(string? e)
        {
            if (string.IsNullOrWhiteSpace(e)) return "";
            return decimal.TryParse(e.Trim(), out var d) ? $"{d:N0}" : e.Trim();
        }

        private static IContainer LabelCell(IContainer c) =>
            c.PaddingBottom(2, Unit.Millimetre).PaddingRight(3, Unit.Millimetre);
        private static IContainer ValueCell(IContainer c) =>
            c.PaddingBottom(2, Unit.Millimetre);
    }
}