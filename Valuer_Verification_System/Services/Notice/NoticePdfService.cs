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
        private const string DividerHex = "#cccccc";   // thin grey line in header

        // ── Body text ─────────────────────────────────────────────────────────
        private const string Para1 =
            "Notice is hereby given in terms of Section 78(5)(a) read together with Section 78(5)(b) " +
            "of the Local Government: Municipal Property Rates Act, 2004 (Act No. 6 of 2004), as amended " +
            "hereinafter referred to as the \u201cAct\u201d,  that a Supplementary Valuation to the General " +
            "Valuation Roll for the property described below has been completed.";

        private const string Para3 =
            "The form for the lodging of a review is obtainable from the City\u2019s website:\n" +
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
                page.MarginHorizontal(18, Unit.Millimetre);
                page.MarginTop(14, Unit.Millimetre);
                page.MarginBottom(14, Unit.Millimetre);

                page.Content().Column(col =>
                {
                    // ══════════════════════════════════════════════════════════
                    // HEADER  — matches the uploaded PDF exactly
                    //
                    //  [Logo]   | City of Johannesburg
                    //           | Finance: Valuation Services
                    //           | ─────────────────────────────────
                    //           | Jorissen Place       www.joburg.org.za
                    //           | 66 Jorissen Street
                    //           | Braamfontein
                    // ══════════════════════════════════════════════════════════
                    col.Item().Row(hdr =>
                    {
                        // Left: logo
                        hdr.RelativeItem(2.6f).AlignMiddle().Column(logo =>
                        {
                            if (File.Exists(ctx.LogoPath))
                                logo.Item().Width(35, Unit.Millimetre).Image(ctx.LogoPath);
                            else
                                logo.Item().Text("City of Johannesburg").Bold().FontSize(12);
                        });

                        // Right: two-part address block with internal divider line
                        hdr.RelativeItem(3.4f).Column(addr =>
                        {
                            // Top part: City name + department
                            addr.Item().Text("City of Johannesburg").FontSize(8.5f).Bold();
                            addr.Item().Text("Finance: Valuation Services").FontSize(8f);
                            addr.Item().PaddingTop(2, Unit.Millimetre)
                                       .LineHorizontal(0.5f).LineColor(DividerHex);
                            addr.Item().PaddingTop(2, Unit.Millimetre);

                            // Bottom part: street address left, website right
                            addr.Item().Row(ab =>
                            {
                                ab.RelativeItem().Column(street =>
                                {
                                    street.Item().Text("Jorissen Place").FontSize(7.5f);
                                    street.Item().Text("66 Jorissen Street").FontSize(7.5f);
                                    street.Item().Text("Braamfontein").FontSize(7.5f);
                                });
                                ab.RelativeItem().AlignRight()
                                  .Text("www.joburg.org.za").FontSize(7.5f).FontColor(GoldHex);
                            });
                        });
                    });

                    // Thin gold rule under full header
                    col.Item().PaddingTop(3, Unit.Millimetre)
                              .LineHorizontal(0.6f).LineColor(GoldHex);
                    col.Item().PaddingTop(6, Unit.Millimetre);

                    // ══════════════════════════════════════════════════════════
                    // OWNER ADDRESS — bold, left, ~4 lines
                    // ══════════════════════════════════════════════════════════
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

                    // ══════════════════════════════════════════════════════════
                    // LETTER DATE — right aligned, bold
                    // ══════════════════════════════════════════════════════════
                    col.Item().AlignRight()
                              .Text(ctx.LetterDate.ToString("dd MMMM yyyy").ToUpperInvariant())
                              .Bold().FontSize(9.5f);

                    col.Item().PaddingTop(4, Unit.Millimetre);

                    // ══════════════════════════════════════════════════════════
                    // TITLE — bold, centred, underlined + thick black rule below
                    // ══════════════════════════════════════════════════════════
                    col.Item().AlignCenter()
                              .Text($"NOTICE OF A SUPPLEMENTARY VALUATION TO THE GENERAL VALUATION ROLL {ctx.GvRollYear}")
                              .Bold().FontSize(10.5f).Underline();
                    col.Item().PaddingTop(1.5f, Unit.Millimetre)
                              .LineHorizontal(1.5f).LineColor(BlackHex);
                    col.Item().PaddingTop(5, Unit.Millimetre);

                    // ══════════════════════════════════════════════════════════
                    // BODY PARAGRAPHS
                    // ══════════════════════════════════════════════════════════
                    col.Item().Text(Para1).FontSize(9f).LineHeight(1.45f);
                    col.Item().PaddingTop(4, Unit.Millimetre);

                    // Paragraph 2 — review period dates in bold inline
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

                    col.Item().PaddingTop(4, Unit.Millimetre);
                    col.Item().Text(Para3).FontSize(9f).LineHeight(1.45f);
                    col.Item().PaddingTop(4, Unit.Millimetre);

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

                    col.Item().PaddingTop(4, Unit.Millimetre);
                    col.Item().Text(Para5).FontSize(9f).LineHeight(1.45f);
                    col.Item().PaddingTop(6, Unit.Millimetre);

                    // ══════════════════════════════════════════════════════════
                    // PROPERTY DETAILS SECTION HEADING
                    // ══════════════════════════════════════════════════════════
                    col.Item().AlignCenter()
                              .Text("PROPERTY DETAILS THAT MAY BE REVIEWED:")
                              .Bold().FontSize(9.5f);
                    col.Item().PaddingTop(3, Unit.Millimetre);

                    if (isMulti)
                        col.Item().Element(c => BuildMultiPurposeTable(c, mainRow, splitRows));
                    else
                        col.Item().Element(c => BuildSingleTable(c, mainRow));

                    col.Item().PaddingTop(10, Unit.Millimetre);

                    // ══════════════════════════════════════════════════════════
                    // FOOTER — matches PDF exactly
                    //   For further enquiries please contact :
                    //   •Telephone 011-407-6622 or 011-407-6597
                    //   •Email valuationenquiries@joburg.org.za
                    //                                    [ValuationKey right]
                    // ══════════════════════════════════════════════════════════
                    col.Item().LineHorizontal(0.4f).LineColor(DividerHex);
                    col.Item().PaddingTop(3, Unit.Millimetre);
                    col.Item().Row(ft =>
                    {
                        ft.RelativeItem(4).Column(c =>
                        {
                            c.Item().Text("For further enquiries please contact :").FontSize(8f);
                            c.Item().Text("\u2022Telephone 011-407-6622 or 011-407-6597").FontSize(8f);
                            c.Item().Text("\u2022Email valuationenquiries@joburg.org.za").FontSize(8f);
                        });
                        ft.RelativeItem().AlignRight()
                          .Text(Safe(mainRow.ValuationKey)).FontSize(8f);
                    });
                    col.Item().PaddingTop(5, Unit.Millimetre);
                    col.Item().Text("Municipal Valuer").Bold().FontSize(10f);
                });
            })).GeneratePdf();
        }

        // ══════════════════════════════════════════════════════════════════════
        // SINGLE PROPERTY TABLE
        // Matches the uploaded PDF layout:
        //
        //  Property Description: | PORTION 2 ERF 201
        //                        | ROSEBANK
        //  ─────────────────────────────────────────────────────────────────
        //  Owner Name.  | Physical Address.  | Remarks.
        //  BANK THE     |                    |
        //  ─────────────────────────────────────────────────────────────────
        //  Property Category  | Market Value | Extent | Effective Date
        //  Business and Comm  | R349 610 000 | 2 000  | 2024/09/27
        // ══════════════════════════════════════════════════════════════════════
        private static void BuildSingleTable(IContainer container, ExtractRow row)
        {
            container.Column(col =>
            {
                // ── Row 1: Property Description ───────────────────────────────
                col.Item().Table(t =>
                {
                    t.ColumnsDefinition(c =>
                    {
                        c.RelativeColumn(2.2f);   // label
                        c.RelativeColumn(4.8f);   // value
                    });
                    t.Cell().Element(LabelCell).Text("Property Description:").Bold().FontSize(8.5f);
                    t.Cell().Element(ValueCell).Text(Safe(row.PropertyDesc)).FontSize(8.5f);
                });

                col.Item().PaddingTop(2, Unit.Millimetre);

                // ── Row 2: Owner / Address / Remarks header + data ────────────
                col.Item().Table(t =>
                {
                    t.ColumnsDefinition(c =>
                    {
                        c.RelativeColumn(2.5f);   // Owner Name
                        c.RelativeColumn(3f);     // Physical Address
                        c.RelativeColumn(1.5f);   // Remarks
                    });

                    // Header row
                    static IContainer Hdr(IContainer c) =>
                        c.BorderBottom(0.6f).BorderColor(BlackHex)
                         .PaddingBottom(1.5f, Unit.Millimetre);

                    t.Cell().Element(Hdr).Text("Owner Name.").Bold().FontSize(8.5f);
                    t.Cell().Element(Hdr).Text("Physical Address.").Bold().FontSize(8.5f);
                    t.Cell().Element(Hdr).Text("Remarks.").Bold().FontSize(8.5f);

                    // Data row
                    static IContainer DataR(IContainer c) =>
                        c.PaddingTop(1.5f, Unit.Millimetre).PaddingBottom(2f, Unit.Millimetre);

                    t.Cell().Element(DataR).Text(Safe(row.OwnerName)).FontSize(8.5f);
                    t.Cell().Element(DataR).Text(BuildPhysical(row)).FontSize(8.5f);
                    t.Cell().Element(DataR).Text(Safe(row.RemarkDesc ?? row.Remarks)).FontSize(8.5f);
                });

                col.Item().PaddingTop(2, Unit.Millimetre);

                // ── Row 3: Category / Value / Extent / Date header + data ─────
                col.Item().Table(t =>
                {
                    t.ColumnsDefinition(c =>
                    {
                        c.RelativeColumn(3f);     // Property Category
                        c.RelativeColumn(2f);     // Market Value
                        c.RelativeColumn(1.2f);   // Extent
                        c.RelativeColumn(1.5f);   // Effective Date
                    });

                    // Header row
                    static IContainer Hdr2(IContainer c) =>
                        c.BorderBottom(0.6f).BorderColor(BlackHex)
                         .PaddingBottom(1.5f, Unit.Millimetre);

                    t.Cell().Element(Hdr2).Text("Property Category").Bold().FontSize(8.5f);
                    t.Cell().Element(Hdr2).Text("Market Value").Bold().FontSize(8.5f);
                    t.Cell().Element(Hdr2).Text("Extent").Bold().FontSize(8.5f);
                    t.Cell().Element(Hdr2).Text("Effective Date").Bold().FontSize(8.5f);

                    // Data row — bold values matching the PDF
                    static IContainer DataR2(IContainer c) =>
                        c.PaddingTop(1.5f, Unit.Millimetre).PaddingBottom(2f, Unit.Millimetre);

                    t.Cell().Element(DataR2).Text(Safe(row.PropertyCategory ?? row.CatDesc)).Bold().FontSize(8.5f);
                    t.Cell().Element(DataR2).Text(FormatMv(row.MarketValue)).Bold().FontSize(8.5f);
                    t.Cell().Element(DataR2).Text(FormatExtent(row.Extent ?? row.LegalArea)).Bold().FontSize(8.5f);
                    t.Cell().Element(DataR2).Text(FormatDate(row.ValuationDate)).Bold().FontSize(8.5f);
                });
            });
        }

        // ══════════════════════════════════════════════════════════════════════
        // MULTIPURPOSE TABLE (up to 4 split rows)
        // Same layout as single but with a splits grid for the value rows
        // ══════════════════════════════════════════════════════════════════════
        private static void BuildMultiPurposeTable(IContainer container, ExtractRow mainRow, List<ExtractRow> splits)
        {
            container.Column(col =>
            {
                // Property Description
                col.Item().Table(t =>
                {
                    t.ColumnsDefinition(c =>
                    {
                        c.RelativeColumn(2.2f);
                        c.RelativeColumn(4.8f);
                    });
                    t.Cell().Element(LabelCell).Text("Property Description:").Bold().FontSize(8.5f);
                    t.Cell().Element(ValueCell).Text(Safe(mainRow.PropertyDesc)).FontSize(8.5f);
                });

                col.Item().PaddingTop(2, Unit.Millimetre);

                // Owner / Address / Remarks
                col.Item().Table(t =>
                {
                    t.ColumnsDefinition(c =>
                    {
                        c.RelativeColumn(2.5f);
                        c.RelativeColumn(3f);
                        c.RelativeColumn(1.5f);
                    });

                    static IContainer Hdr(IContainer c) =>
                        c.BorderBottom(0.6f).BorderColor(BlackHex).PaddingBottom(1.5f, Unit.Millimetre);
                    static IContainer DataR(IContainer c) =>
                        c.PaddingTop(1.5f, Unit.Millimetre).PaddingBottom(2f, Unit.Millimetre);

                    t.Cell().Element(Hdr).Text("Owner Name.").Bold().FontSize(8.5f);
                    t.Cell().Element(Hdr).Text("Physical Address.").Bold().FontSize(8.5f);
                    t.Cell().Element(Hdr).Text("Remarks.").Bold().FontSize(8.5f);

                    t.Cell().Element(DataR).Text(Safe(mainRow.OwnerName)).FontSize(8.5f);
                    t.Cell().Element(DataR).Text(BuildPhysical(mainRow)).FontSize(8.5f);
                    t.Cell().Element(DataR).Text(Safe(mainRow.RemarkDesc ?? mainRow.Remarks)).FontSize(8.5f);
                });

                col.Item().PaddingTop(2, Unit.Millimetre);

                // Splits grid
                col.Item().Table(t =>
                {
                    t.ColumnsDefinition(c =>
                    {
                        c.RelativeColumn(0.5f);   // #
                        c.RelativeColumn(2.8f);   // Category Description
                        c.RelativeColumn(1.4f);   // Cat Code
                        c.RelativeColumn(1.8f);   // Market Value
                        c.RelativeColumn(1.2f);   // Extent
                        c.RelativeColumn(1.5f);   // Effective Date
                    });

                    static IContainer GoldHdr(IContainer c) =>
                        c.Background(GoldHex).Padding(2.5f, Unit.Millimetre).AlignCenter();

                    void H(ITableCellContainer cell, string text) =>
                        cell.Element(GoldHdr).Text(text).Bold().FontSize(7.5f).FontColor(BlackHex);

                    H(t.Cell(), "#");
                    H(t.Cell(), "Category Description");
                    H(t.Cell(), "Cat Code");
                    H(t.Cell(), "Market Value");
                    H(t.Cell(), "Extent (m\u00b2)");
                    H(t.Cell(), "Effective Date");

                    int n = 0;
                    foreach (var s in splits.Take(4))
                    {
                        n++;
                        bool shade = n % 2 == 0;
                        IContainer DC(IContainer c) =>
                            c.Background(shade ? "#f9f3d9" : Colors.White)
                             .BorderBottom(0.3f).BorderColor(Colors.Grey.Lighten2)
                             .Padding(2.5f, Unit.Millimetre);

                        t.Cell().Element(DC).Text(n.ToString()).FontSize(8f).AlignCenter();
                        t.Cell().Element(DC).Text(Safe(s.CatDesc)).FontSize(8f);
                        t.Cell().Element(DC).Text(Safe(s.CatCode)).FontSize(8f);
                        t.Cell().Element(DC).Text(FormatMv(s.MarketValue)).FontSize(8f).AlignRight();
                        t.Cell().Element(DC).Text(FormatExtent(s.Extent ?? s.LegalArea)).FontSize(8f).AlignRight();
                        t.Cell().Element(DC).Text(FormatDate(s.ValuationDate)).FontSize(8f).AlignCenter();
                    }
                });

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

            // First try SendNotice = Y (no Decision filter — it's a preview)
            var mainRow = await vvsDb.ExtractRows
                .AsNoTracking()
                .Where(r => r.BatchId == batchId && r.SendNotice == "Y")
                .OrderBy(r => r.ValuationKey)
                .FirstOrDefaultAsync(ct);

            // Fallback: any row in the batch
            if (mainRow is null)
                mainRow = await vvsDb.ExtractRows
                    .AsNoTracking()
                    .Where(r => r.BatchId == batchId)
                    .OrderBy(r => r.ValuationKey)
                    .FirstOrDefaultAsync(ct);

            if (mainRow is null) return null;

            var splits = await vvsDb.ExtractRows
                .AsNoTracking()
                .Where(r => r.BatchId == batchId && r.ValuationKey == mainRow.ValuationKey)
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
            Safe(r.PhysicalAddress ?? r.PremiseAddress ?? r.LisStreetAddress) is { Length: > 0 } s ? s : "";

        private static string FormatExtent(string? e)
        {
            if (string.IsNullOrWhiteSpace(e)) return "";
            var s = e.Trim();
            // Parse with InvariantCulture so "2000.000000" always works regardless of server locale
            if (decimal.TryParse(s,
                    System.Globalization.NumberStyles.Any,
                    System.Globalization.CultureInfo.InvariantCulture,
                    out var d))
                return $"{d:N0}";   // e.g. 2 000  (no decimals, thousands separator)
                                    // Fallback: strip trailing .000... manually
            if (s.Contains('.'))
                s = s.TrimEnd('0').TrimEnd('.');
            return s;
        }
        private static IContainer LabelCell(IContainer c) =>
            c.PaddingBottom(2, Unit.Millimetre).PaddingRight(3, Unit.Millimetre);
        private static IContainer ValueCell(IContainer c) =>
            c.PaddingBottom(2, Unit.Millimetre);
    }
}