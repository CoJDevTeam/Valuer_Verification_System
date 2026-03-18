// Requires NuGet: QuestPDF (2024.3.0+)
using Microsoft.EntityFrameworkCore;
using QuestPDF.Elements.Table;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using System.Globalization;
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
        private const string DividerHex = "#cccccc";
        private const string BlueHdrHex = "#46829e";   // split-table header (steel blue)

        // ── Paragraph text ────────────────────────────────────────────────────
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
            var culture = CultureInfo.GetCultureInfo("en-ZA");

            // ── Named TextStyles (Section49 pattern) ──────────────────────────
            var body9 = TextStyle.Default.FontFamily("Arial").FontSize(9);
            var body9b = TextStyle.Default.FontFamily("Arial").FontSize(9).SemiBold();
            var body85b = TextStyle.Default.FontFamily("Arial").FontSize(8.5f).SemiBold();
            var body85 = TextStyle.Default.FontFamily("Arial").FontSize(8.5f);
            var addr10b = TextStyle.Default.FontFamily("Arial").FontSize(10).SemiBold();
            var small8 = TextStyle.Default.FontFamily("Arial").FontSize(8);
            var small75 = TextStyle.Default.FontFamily("Arial").FontSize(7.5f);
            var small8b = TextStyle.Default.FontFamily("Arial").FontSize(8).SemiBold();
            var keyStyle = TextStyle.Default.FontFamily("Arial").FontSize(7).SemiBold().FontColor(Colors.Red.Medium);

            return Document.Create(c => c.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.MarginLeft(30);
                page.MarginRight(30);
                page.MarginTop(10);
                page.MarginBottom(10);

                // ══ PAGE CONTENT ══════════════════════════════════════════════
                page.Content().Column(col =>
                {
                    col.Spacing(6);

                    // ── HEADER: logo left | address block right ──────────────
                    col.Item().Row(hdr =>
                    {
                        // Left: logo
                        hdr.RelativeItem(2.6f).AlignMiddle().Column(logo =>
                        {
                            if (File.Exists(ctx.LogoPath))
                                logo.Item().Width(35, Unit.Millimetre).Image(ctx.LogoPath);
                            else
                                logo.Item().Text("City of Johannesburg").Style(body9b);
                        });

                        // Right: City name + dept / divider / street + website
                        hdr.RelativeItem(3.4f).Column(addr =>
                        {
                            addr.Item().Text("City of Johannesburg").Style(body9b);
                            addr.Item().Text("Finance: Valuation Services").Style(small8);
                            addr.Item().PaddingTop(2, Unit.Millimetre)
                                       .LineHorizontal(0.5f).LineColor(DividerHex);
                            addr.Item().PaddingTop(2, Unit.Millimetre);
                            addr.Item().Row(ab =>
                            {
                                ab.RelativeItem().Column(st =>
                                {
                                    st.Item().Text("Jorissen Place").Style(small75);
                                    st.Item().Text("66 Jorissen Street").Style(small75);
                                    st.Item().Text("Braamfontein").Style(small75);
                                });
                                ab.RelativeItem().AlignRight()
                                  .Text("www.joburg.org.za")
                                  .Style(small75.FontColor(GoldHex));
                            });
                        });
                    });



                    // ── OWNER ADDRESS (left) + LETTER DATE (right) — same row ──
                    col.Item().Row(addrRow =>
                    {
                        addrRow.RelativeItem().Column(ow =>
                        {
                            foreach (var line in new[]
                                { mainRow.Addr1, mainRow.Addr2, mainRow.Addr3,
                                  mainRow.Addr4, mainRow.Addr5 })
                            {
                                if (!string.IsNullOrWhiteSpace(line))
                                    ow.Item().Text(line.Trim()).Style(addr10b);
                            }
                        });
                        addrRow.ConstantItem(160).AlignRight().AlignBottom()
                               .Text(ctx.LetterDate.ToString("dd MMMM yyyy", culture).ToUpperInvariant())
                               .Style(body9b);
                    });

                    // ── ONE THICK BLACK LINE ──────────────────────────────────
                    col.Item().LineHorizontal(1.5f).LineColor(BlackHex);

                    // ── TITLE — bold, centred, NO underline ───────────────────
                    col.Item().AlignCenter()
                              .Text($"NOTICE OF A SUPPLEMENTARY VALUATION TO THE GENERAL VALUATION ROLL {ctx.GvRollYear}")
                              .Style(body9b);

                    // ── THICK LINE BELOW TITLE ────────────────────────────────
                    col.Item().LineHorizontal(1.5f).LineColor(BlackHex);

                    // ── DEAR GREETING ─────────────────────────────────────────
                    var ownerGreeting = Safe(mainRow.Addr1);
                    var isFallbackAddr = ownerGreeting.Equals(
                        "TO THE REGISTERED OWNER OF:", StringComparison.OrdinalIgnoreCase);
                    col.Item().Text(string.IsNullOrWhiteSpace(ownerGreeting) || isFallbackAddr
                        ? "Dear Sir/Madam"
                        : $"Dear: {ownerGreeting}").Style(body9b);

                    // ── PARAGRAPH 1 ───────────────────────────────────────────
                    col.Item().Text(Para1).Style(body9).LineHeight(1.45f);

                    // ── PARAGRAPH 2: dates bold inline ────────────────────────
                    col.Item().Text(t =>
                    {
                        t.Span("An invitation is hereby made in terms of Section 78(5)(b) of the Act " +
                               "that any owner of the property who so desires may request a review with " +
                               "the Municipal Manager in respect of any matter reflected in the " +
                               "supplementary valuation within the period: ").Style(body9);
                        t.Span(ctx.ReviewOpenDate.ToString("dd MMMM yyyy", culture).ToUpperInvariant() +
                               "  \u2013  " +
                               ctx.ReviewCloseDate.ToString("dd MMMM yyyy", culture).ToUpperInvariant())
                         .Style(body9b);
                    });

                    // ── PARAGRAPH 3 ───────────────────────────────────────────
                    col.Item().Text(Para3).Style(body9).LineHeight(1.45f);

                    // ── PARAGRAPH 4: close date bold inline ───────────────────
                    col.Item().Text(t =>
                    {
                        t.Span("The completed review forms should be emailed to " +
                               "valuationenquiries@joburg.org.za or by hand to the Valuation Services: " +
                               "Administration office on or before 15:00 on ").Style(body9);
                        t.Span(ctx.ReviewCloseDate.ToString("dd MMMM yyyy", culture).ToUpperInvariant())
                         .Style(body9b);
                    });

                    // ── PARAGRAPH 5 ───────────────────────────────────────────
                    col.Item().Text(Para5).Style(body9).LineHeight(1.45f);

                    // ── PROPERTY DETAILS HEADING ──────────────────────────────
                    col.Item().AlignCenter()
                              .Text("PROPERTY DETAILS THAT MAY BE REVIEWED:")
                              .Style(body85b);

                    // ── PROPERTY TABLE (single or multipurpose) ───────────────
                    if (isMulti)
                        col.Item().Element(cont =>
                            BuildMultiPurposeTable(cont, mainRow, splitRows, body85b, body85));
                    else
                        col.Item().Element(cont =>
                            BuildSingleTable(cont, mainRow, body85b, body85));

                    // ── FOOTER (in content — original position) ───────────────
                    col.Item().LineHorizontal(0.4f).LineColor(DividerHex);
                    col.Item().Row(ft =>
                    {
                        ft.RelativeItem(4).Column(enq =>
                        {
                            enq.Item().Text("For further enquiries please contact :").Style(small8b);
                            enq.Item().Text("\u2022Telephone 011-407-6622 or 011-407-6597").Style(small8b);
                            enq.Item().Text("\u2022Email valuationenquiries@joburg.org.za").Style(small8b);
                        });
                        ft.RelativeItem().AlignRight()
                          .Text(Safe(mainRow.ValuationKey)).Style(keyStyle);
                    });
                    col.Item().Text("Municipal Valuer").Style(body9b);
                });
            })).GeneratePdf();
        }

        // ══════════════════════════════════════════════════════════════════════
        // SINGLE PROPERTY TABLE
        // ══════════════════════════════════════════════════════════════════════
        private static void BuildSingleTable(
            IContainer container, ExtractRow row,
            TextStyle hdrStyle, TextStyle valStyle)
        {
            container.Column(col =>
            {
                col.Spacing(6);

                // 1) Property Description
                col.Item().Table(t =>
                {
                    t.ColumnsDefinition(c =>
                    {
                        c.RelativeColumn(2.2f);
                        c.RelativeColumn(4.8f);
                    });
                    t.Cell().Element(LabelCell).Text("Property Description:").Style(hdrStyle);
                    t.Cell().Element(ValueCell).Text(Safe(row.PropertyDesc)).Style(valStyle);
                });

                // 2) Owner | Physical Address | Remarks — header + data
                col.Item().Table(t =>
                {
                    t.ColumnsDefinition(c =>
                    {
                        c.RelativeColumn(2.5f);
                        c.RelativeColumn(3f);
                        c.RelativeColumn(1.5f);
                    });

                    IContainer ColHdr(IContainer c) =>
                        c.BorderBottom(0.6f).BorderColor(BlackHex).PaddingBottom(2);
                    IContainer ColData(IContainer c) =>
                        c.PaddingTop(2).PaddingBottom(2);

                    t.Cell().Element(ColHdr).Text("Owner Name.").Style(hdrStyle);
                    t.Cell().Element(ColHdr).Text("Physical Address.").Style(hdrStyle);
                    t.Cell().Element(ColHdr).Text("Remarks.").Style(hdrStyle);

                    t.Cell().Element(ColData).Text(Safe(row.OwnerName)).Style(valStyle);
                    t.Cell().Element(ColData).Text(BuildPhysical(row)).Style(valStyle);
                    t.Cell().Element(ColData).Text(Safe(row.RemarkDesc ?? row.Remarks)).Style(valStyle);
                });

                // 3) Category | Market Value | Extent | Effective Date — header + bold data
                col.Item().Table(t =>
                {
                    t.ColumnsDefinition(c =>
                    {
                        c.RelativeColumn(3f);
                        c.RelativeColumn(2f);
                        c.RelativeColumn(1.2f);
                        c.RelativeColumn(1.5f);
                    });

                    IContainer ColHdr2(IContainer c) =>
                        c.BorderBottom(0.6f).BorderColor(BlackHex).PaddingBottom(2);
                    IContainer ColData2(IContainer c) =>
                        c.PaddingTop(2).PaddingBottom(2);

                    t.Cell().Element(ColHdr2).Text("Property Category").Style(hdrStyle);
                    t.Cell().Element(ColHdr2).Text("Market Value").Style(hdrStyle);
                    t.Cell().Element(ColHdr2).Text("Extent").Style(hdrStyle);
                    t.Cell().Element(ColHdr2).Text("Effective Date").Style(hdrStyle);

                    t.Cell().Element(ColData2).Text(Safe(row.PropertyCategory ?? row.CatDesc)).Style(hdrStyle);
                    t.Cell().Element(ColData2).Text(FormatMv(row.MarketValue)).Style(hdrStyle);
                    t.Cell().Element(ColData2).Text(FormatExtent(row.Extent ?? row.LegalArea)).Style(hdrStyle);
                    t.Cell().Element(ColData2).Text(FormatDate(row.ValuationDate)).Style(hdrStyle);
                });
            });
        }

        // ══════════════════════════════════════════════════════════════════════
        // MULTIPURPOSE / SPLIT TABLE (up to 4 split rows)
        // ══════════════════════════════════════════════════════════════════════
        private static void BuildMultiPurposeTable(
            IContainer container, ExtractRow mainRow, List<ExtractRow> splits,
            TextStyle hdrStyle, TextStyle valStyle)
        {
            var splitHdr = TextStyle.Default.FontFamily("Arial").FontSize(8).SemiBold().FontColor(Colors.White);
            var splitBody = TextStyle.Default.FontFamily("Arial").FontSize(8);
            var splitBold = TextStyle.Default.FontFamily("Arial").FontSize(8).SemiBold();
            var totalSt = TextStyle.Default.FontFamily("Arial").FontSize(8.5f).SemiBold();

            container.Column(col =>
            {
                col.Spacing(6);

                // Property Description
                col.Item().Table(t =>
                {
                    t.ColumnsDefinition(c =>
                    {
                        c.RelativeColumn(2.2f);
                        c.RelativeColumn(4.8f);
                    });
                    t.Cell().Element(LabelCell).Text("Property Description:").Style(hdrStyle);
                    t.Cell().Element(ValueCell).Text(Safe(mainRow.PropertyDesc)).Style(valStyle);
                });

                // Owner / Address / Remarks
                col.Item().Table(t =>
                {
                    t.ColumnsDefinition(c =>
                    {
                        c.RelativeColumn(2.5f);
                        c.RelativeColumn(3f);
                        c.RelativeColumn(1.5f);
                    });

                    IContainer ColHdr(IContainer c) =>
                        c.BorderBottom(0.6f).BorderColor(BlackHex).PaddingBottom(2);
                    IContainer ColData(IContainer c) =>
                        c.PaddingTop(2).PaddingBottom(2);

                    t.Cell().Element(ColHdr).Text("Owner Name.").Style(hdrStyle);
                    t.Cell().Element(ColHdr).Text("Physical Address.").Style(hdrStyle);
                    t.Cell().Element(ColHdr).Text("Remarks.").Style(hdrStyle);

                    t.Cell().Element(ColData).Text(Safe(mainRow.OwnerName)).Style(valStyle);
                    t.Cell().Element(ColData).Text(BuildPhysical(mainRow)).Style(valStyle);
                    t.Cell().Element(ColData).Text(Safe(mainRow.RemarkDesc ?? mainRow.Remarks)).Style(valStyle);
                });

                // Splits grid — steel-blue header, alternating shaded rows
                col.Item().Table(t =>
                {
                    t.ColumnsDefinition(c =>
                    {
                        c.ConstantColumn(20);     // #
                        c.RelativeColumn(2.8f);   // Category Description
                        c.RelativeColumn(1.4f);   // Cat Code
                        c.RelativeColumn(1.8f);   // Market Value
                        c.RelativeColumn(1.2f);   // Extent
                        c.RelativeColumn(1.5f);   // Effective Date
                    });

                    void H(ITableCellContainer cell, string text) =>
                        cell.Element(c => c.Background(BlueHdrHex).Padding(3).AlignCenter())
                            .Text(text).Style(splitHdr);

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
                            c.Background(shade ? "#e8f0f8" : Colors.White)
                             .BorderBottom(0.3f).BorderColor(Colors.Grey.Lighten2)
                             .Padding(3);

                        t.Cell().Element(c => DC(c).AlignCenter()).Text(n.ToString()).Style(splitBody);
                        t.Cell().Element(DC).Text(Safe(s.CatDesc)).Style(splitBody);
                        t.Cell().Element(DC).Text(Safe(s.CatCode)).Style(splitBody);
                        t.Cell().Element(c => DC(c).AlignRight()).Text(FormatMv(s.MarketValue)).Style(splitBold);
                        t.Cell().Element(c => DC(c).AlignRight()).Text(FormatExtent(s.Extent ?? s.LegalArea)).Style(splitBold);
                        t.Cell().Element(c => DC(c).AlignCenter()).Text(FormatDate(s.ValuationDate)).Style(splitBody);
                    }
                });

                var totalMv = splits.Take(4).Sum(r => r.MarketValue ?? 0);
                col.Item().AlignRight()
                          .Text($"Total Market Value: {FormatMv(totalMv)}")
                          .Style(totalSt);
            });
        }

        // ── Preview ───────────────────────────────────────────────────────────
        public async Task<byte[]?> GeneratePreviewAsync(
            int batchId, NoticeContext ctx,
            Microsoft.EntityFrameworkCore.DbContext db, CancellationToken ct)
        {
            var vvsDb = (VvsDbContext)db;

            var mainRow = await vvsDb.ExtractRows
                .AsNoTracking()
                .Where(r => r.BatchId == batchId
                         && r.SendNotice != null
                         && r.SendNotice.ToUpper() == "Y")
                .OrderBy(r => r.ValuationKey)
                .FirstOrDefaultAsync(ct);

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

        private static string FormatMv(decimal? v) =>
            v.HasValue ? $"R {v.Value:N0}" : "R 0";

        private static string FormatDate(DateOnly? d) =>
            d.HasValue ? d.Value.ToString("yyyy/MM/dd") : "";

        private static string BuildPhysical(ExtractRow r) =>
            Safe(r.PhysicalAddress ?? r.PremiseAddress ?? r.LisStreetAddress) is { Length: > 0 } s ? s : "";

        private static string FormatExtent(string? e)
        {
            if (string.IsNullOrWhiteSpace(e)) return "";
            var s = e.Trim();
            if (decimal.TryParse(s,
                    System.Globalization.NumberStyles.Any,
                    System.Globalization.CultureInfo.InvariantCulture,
                    out var d))
                return $"{d:N0}";
            if (s.Contains('.'))
                s = s.TrimEnd('0').TrimEnd('.');
            return s;
        }

        private static IContainer LabelCell(IContainer c) =>
            c.PaddingBottom(2).PaddingRight(4);

        private static IContainer ValueCell(IContainer c) =>
            c.PaddingBottom(2);
    }
}