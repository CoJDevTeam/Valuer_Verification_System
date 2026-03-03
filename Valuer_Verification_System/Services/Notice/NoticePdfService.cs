// ┌─────────────────────────────────────────────────────────────────────────┐
// │  REQUIRES NuGet package: QuestPDF (version 2024.3.0 or later)           │
// │  Add to .csproj:                                                         │
// │  <PackageReference Include="QuestPDF" Version="2024.3.0" />             │
// └─────────────────────────────────────────────────────────────────────────┘
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using Valuer_Verification_System.Models.Data;
using Valuer_Verification_System.Models.Notice;

namespace Valuer_Verification_System.Services.Notice
{
    public sealed class NoticePdfService : INoticePdfService
    {
        // ── Branding constants ────────────────────────────────────────────────
        private const string GoldHex = "#e6b000";
        private const string BlackHex = "#111111";
        private const string GreyHex = "#555555";

        // Fixed legal body text (Section 78)
        private const string BodyPara1 =
            "Notice is hereby given in terms of Section 78(5)(a) read together with Section 78(5)(b) " +
            "of the Local Government: Municipal Property Rates Act, 2004 (Act No. 6 of 2004), as amended " +
            "hereinafter referred to as the \u201cAct\u201d, that a Supplementary Valuation to the General " +
            "Valuation Roll for the property described below has been completed.";

        private const string BodyPara2Template =
            "An invitation is hereby made in terms of Section 78(5)(b) of the Act that any owner of the " +
            "property who so desires may request a review with the Municipal Manager in respect of any matter " +
            "reflected in the supplementary valuation within the period: {0} - {1}";

        private const string BodyPara3 =
            "The form for the lodging of a review is obtainable from the City\u2019s website: " +
            "https://eservices.joburg.org.za/Pages/Overview.aspx or at the following address: " +
            "Valuation Services: Administration, 1st Floor, East Wing, Jorissen Place, " +
            "66 Jorissen Street, Braamfontein.";

        private const string BodyPara4Template =
            "The completed review forms should be emailed to valuationenquiries@joburg.org.za or " +
            "by hand to the Valuation Services: Administration office on or before 15:00 on {0}";

        private const string BodyPara5 =
            "If a representative is appointed, proof of authorisation must be attached to this form.";

        static NoticePdfService()
        {
            // QuestPDF community licence (free for open-source / non-commercial)
            QuestPDF.Settings.License = LicenseType.Community;
        }

        public byte[] GenerateNotice(ExtractRow row, NoticeContext ctx)
        {
            return Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.MarginTop(15, Unit.Millimetre);
                    page.MarginBottom(15, Unit.Millimetre);
                    page.MarginLeft(20, Unit.Millimetre);
                    page.MarginRight(20, Unit.Millimetre);

                    page.Content().Column(col =>
                    {
                        // ── 1. HEADER ──────────────────────────────────────────
                        col.Item().Row(header =>
                        {
                            // Logo
                            header.RelativeItem(3).AlignMiddle().AlignLeft().Column(logo =>
                            {
                                if (File.Exists(ctx.LogoPath))
                                    logo.Item().Width(80, Unit.Millimetre).Image(ctx.LogoPath);
                                else
                                    logo.Item().Text("City of Johannesburg").Bold().FontSize(14);
                            });

                            // COJ address block
                            header.RelativeItem(2).AlignRight().Column(addr =>
                            {
                                addr.Item().Text("City of Johannesburg").FontSize(8);
                                addr.Item().Text("Finance: Valuation Services").FontSize(8);
                                addr.Item().Text("Jorissen Place").FontSize(8);
                                addr.Item().Text("66 Jorissen Street").FontSize(8);
                                addr.Item().Text("Braamfontein").FontSize(8);
                                addr.Item().Text("www.joburg.org.za").FontSize(8).FontColor(GoldHex);
                            });
                        });

                        col.Item().PaddingTop(2, Unit.Millimetre)
                            .LineHorizontal(0.5f).LineColor(GoldHex);

                        col.Item().PaddingTop(6, Unit.Millimetre);

                        // ── 2. OWNER ADDRESS BLOCK ─────────────────────────────
                        col.Item().Column(ownerCol =>
                        {
                            ownerCol.Item().Text(Safe(row.Addr1)).Bold().FontSize(10);
                            ownerCol.Item().Text(Safe(row.Addr2)).Bold().FontSize(10);
                            ownerCol.Item().Text(Safe(row.Addr3)).Bold().FontSize(10);
                            if (!string.IsNullOrWhiteSpace(row.Addr4))
                                ownerCol.Item().Text(row.Addr4).Bold().FontSize(10);
                            if (!string.IsNullOrWhiteSpace(row.Addr5))
                                ownerCol.Item().Text(row.Addr5).Bold().FontSize(10);
                        });

                        col.Item().PaddingTop(6, Unit.Millimetre);

                        // ── 3. LETTER DATE (right-aligned) ─────────────────────
                        col.Item().AlignRight()
                            .Text(ctx.LetterDate.ToString("dd MMMM yyyy").ToUpperInvariant())
                            .Bold().FontSize(10);

                        col.Item().PaddingTop(6, Unit.Millimetre);

                        // ── 4. TITLE ───────────────────────────────────────────
                        col.Item().AlignCenter()
                            .Text($"NOTICE OF A SUPPLEMENTARY VALUATION TO THE GENERAL VALUATION ROLL {ctx.GvRollYear}")
                            .Bold().FontSize(11).Underline();

                        col.Item().PaddingTop(1, Unit.Millimetre)
                            .LineHorizontal(1f).LineColor(BlackHex);

                        col.Item().PaddingTop(6, Unit.Millimetre);

                        // ── 5. BODY PARAGRAPHS ─────────────────────────────────
                        col.Item().Text(BodyPara1).FontSize(9.5f).LineHeight(1.45f);

                        col.Item().PaddingTop(4, Unit.Millimetre);

                        var reviewPeriod = string.Format(BodyPara2Template,
                            ctx.ReviewOpenDate.ToString("dd MMMM yyyy").ToUpperInvariant(),
                            ctx.ReviewCloseDate.ToString("dd MMMM yyyy").ToUpperInvariant());

                        col.Item().Text(t =>
                        {
                            t.DefaultTextStyle(x => x
    .FontSize(9.5f)
    .LineHeight(1.45f));
                            t.Span("An invitation is hereby made in terms of Section 78(5)(b) of the Act that any owner of the " +
                                   "property who so desires may request a review with the Municipal Manager in respect of any matter " +
                                   "reflected in the supplementary valuation within the period: ").FontSize(9.5f);
                            t.Span($"{ctx.ReviewOpenDate:dd MMMM yyyy}".ToUpperInvariant() +
                                   $"  -  {ctx.ReviewCloseDate:dd MMMM yyyy}".ToUpperInvariant())
                                .Bold().FontSize(9.5f);
                        });

                        col.Item().PaddingTop(4, Unit.Millimetre);
                        col.Item().Text(BodyPara3).FontSize(9.5f).LineHeight(1.45f);

                        col.Item().PaddingTop(4, Unit.Millimetre);
                        col.Item().Text(t =>
                        {
                            t.DefaultTextStyle(x => x
    .FontSize(9.5f)
    .LineHeight(1.45f));
                            t.Span("The completed review forms should be emailed to valuationenquiries@joburg.org.za or " +
                                   "by hand to the Valuation Services: Administration office on or before 15:00 on ").FontSize(9.5f);
                            t.Span(ctx.ReviewCloseDate.ToString("dd MMMM yyyy").ToUpperInvariant())
                                .Bold().FontSize(9.5f);
                        });

                        col.Item().PaddingTop(4, Unit.Millimetre);
                        col.Item().Text(BodyPara5).FontSize(9.5f).LineHeight(1.45f);

                        col.Item().PaddingTop(6, Unit.Millimetre);

                        // ── 6. PROPERTY DETAILS TABLE ──────────────────────────
                        col.Item().AlignCenter()
                            .Text("PROPERTY DETAILS THAT MAY BE REVIEWED:")
                            .Bold().FontSize(10);

                        col.Item().PaddingTop(3, Unit.Millimetre);

                        col.Item().Table(table =>
                        {
                            table.ColumnsDefinition(cols =>
                            {
                                cols.RelativeColumn(2);  // label
                                cols.RelativeColumn(5);  // value
                            });

                            // Property Description
                            table.Cell().Element(LabelCell).Text("Property Description:").Bold().FontSize(9);
                            table.Cell().Element(ValueCell)
                                .Text(Safe(row.PropertyDescription ?? row.PropertyDesc)).FontSize(9);

                            // Owner / Physical / Remarks row headers
                            table.Cell().ColumnSpan(2).Element(c =>
                                c.PaddingTop(4, Unit.Millimetre).Row(r =>
                                {
                                    r.RelativeItem().Text("Owner Name.").Bold().FontSize(9);
                                    r.RelativeItem(2).Text("Physical Address.").Bold().FontSize(9);
                                    r.RelativeItem().Text("Remarks.").Bold().FontSize(9);
                                }));

                            // Owner / Physical / Remarks values
                            table.Cell().ColumnSpan(2).Element(c =>
                                c.Row(r =>
                                {
                                    r.RelativeItem().Text(Safe(row.OwnerName)).FontSize(9);
                                    r.RelativeItem(2).Text(BuildPhysicalAddress(row)).FontSize(9);
                                    r.RelativeItem().Text(Safe(row.RemarkDesc ?? row.Remarks)).FontSize(9);
                                }));
                        });

                        col.Item().PaddingTop(4, Unit.Millimetre);

                        // Property Category / Market Value / Extent / Effective Date header
                        col.Item().Row(r =>
                        {
                            r.RelativeItem(2).AlignCenter().Text("Property Category").Bold().FontSize(9);
                            r.RelativeItem(2).AlignCenter().Text("Market Value").Bold().FontSize(9);
                            r.RelativeItem().AlignCenter().Text("Extent").Bold().FontSize(9);
                            r.RelativeItem(2).AlignCenter().Text("Effective Date").Bold().FontSize(9);
                        });

                        // Property Category / Market Value / Extent / Effective Date values
                        col.Item().Row(r =>
                        {
                            r.RelativeItem(2).AlignCenter()
                                .Text(Safe(row.PropertyCategory ?? row.CatDesc)).Bold().FontSize(10);
                            r.RelativeItem(2).AlignCenter()
                                .Text(FormatMarketValue(row.MarketValue)).Bold().FontSize(10);
                            r.RelativeItem().AlignCenter()
                                .Text(FormatExtent(row.Extent ?? row.LegalArea)).Bold().FontSize(10);
                            r.RelativeItem(2).AlignCenter()
                                .Text(FormatDate(row.ValuationDate)).Bold().FontSize(10);
                        });

                        // Spacer
                        col.Item().PaddingTop(10, Unit.Millimetre);

                        // ── 7. FOOTER ──────────────────────────────────────────
                        col.Item().LineHorizontal(0.5f).LineColor(Colors.Grey.Lighten2);
                        col.Item().PaddingTop(3, Unit.Millimetre);

                        col.Item().Row(footer =>
                        {
                            footer.RelativeItem(3).Column(c =>
                            {
                                c.Item().Text("For further enquiries please contact :").FontSize(8.5f);
                                c.Item().Text("\u2022  Telephone 011-407-6622 or 011-407-6597").FontSize(8.5f);
                                c.Item().Text("\u2022  Email valuationenquiries@joburg.org.za").FontSize(8.5f);
                            });

                            footer.RelativeItem().AlignRight()
                                .Text(Safe(row.ValuationKey)).FontSize(8.5f);
                        });

                        col.Item().PaddingTop(4, Unit.Millimetre);
                        col.Item().Text("Municipal Valuer").Bold().FontSize(11);
                    });
                });
            }).GeneratePdf();
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        private static string Safe(string? s) =>
            string.IsNullOrWhiteSpace(s) ? "" : s.Trim();

        private static string BuildPhysicalAddress(ExtractRow row)
        {
            var addr = row.PhysicalAddress ?? row.PremiseAddress ?? row.LisStreetAddress;
            return string.IsNullOrWhiteSpace(addr) ? "XXXX" : addr.Trim();
        }

        private static string FormatMarketValue(decimal? mv) =>
            mv.HasValue ? $"R{mv.Value:N0}" : "R0";

        private static string FormatExtent(string? ext)
        {
            if (string.IsNullOrWhiteSpace(ext)) return "";
            return decimal.TryParse(ext.Trim(), out var d) ? $"{d:N0}" : ext.Trim();
        }

        private static string FormatDate(DateOnly? d) =>
            d.HasValue ? d.Value.ToString("yyyy/MM/dd") : "";

        private static IContainer LabelCell(IContainer c) =>
            c.PaddingBottom(2, Unit.Millimetre).PaddingRight(4, Unit.Millimetre);

        private static IContainer ValueCell(IContainer c) =>
            c.PaddingBottom(2, Unit.Millimetre);
    }
}