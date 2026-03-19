using DocumentFormat.OpenXml.Math;
using DocumentFormat.OpenXml.Office2016.Drawing.ChartDrawing;
using DocumentFormat.OpenXml.Spreadsheet;
using DocumentFormat.OpenXml.Wordprocessing;
using Humanizer;
using Microsoft.EntityFrameworkCore.Metadata.Internal;
using Microsoft.VisualStudio.Web.CodeGenerators.Mvc.Templates.Blazor;
using Org.BouncyCastle.Pqc.Crypto.Lms;
using System.Diagnostics.Metrics;
using static Microsoft.EntityFrameworkCore.DbLoggerCategory.Model;

namespace Valuer_Verification_System.Infrastructure.Configuration.Email
{
    /// <summary>
    /// All outgoing email templates for the Valuation Verification System.
    /// Colours: Gold #e6b000 | Dark #1a1a1a | Text #111111
    /// </summary>
    public static class EmailTemplates
    {
        // ─────────────────────────────────────────────────────────────────────
        // Template 1 – Extract Uploaded → Notify Sector Managers
        // ─────────────────────────────────────────────────────────────────────
        public static (string subject, string html, string text) ManagersUploadReady(
            string batchName, DateOnly batchDate, DateOnly weekStart, DateOnly weekEnd,
            DateOnly noticeSendDate, int totalRecords, int assignedRecords,
            string managerDashboardUrl)
        {
            var subject = $"[VVS] Extract Uploaded & Ready for Verification – {batchName}";
            int pendingRecords = totalRecords - assignedRecords;
            var weekRange = $"{weekStart:dd MMM yyyy} – {weekEnd:dd MMM yyyy}";
            var noticeDateStr = noticeSendDate.ToString("dd MMM yyyy");
            var uploadDateStr = batchDate.ToString("dd MMM yyyy");
            var year = batchDate.Year;

            var html = $@"<!DOCTYPE html>
<html lang=""en"">
<head><meta charset=""utf-8""/><meta name=""viewport"" content=""width=device-width,initial-scale=1""/>
<title>Valuation Verification – Extract Uploaded</title></head>
<body style=""margin:0;padding:0;background:#f4f4f4;font-family:'Segoe UI',Arial,sans-serif;"">
<table width=""100%"" cellpadding=""0"" cellspacing=""0"" border=""0"" style=""background:#f4f4f4;"">
<tr><td align=""center"" style=""padding:32px 16px;"">
<table width=""620"" cellpadding=""0"" cellspacing=""0"" border=""0""
  style=""max-width:620px;width:100%;background:#fff;border-radius:8px;overflow:hidden;box-shadow:0 2px 8px rgba(0,0,0,.08);"">
<tr><td style=""background:#e6b000;padding:20px 28px;"">
  <p style=""margin:0;font-size:13px;font-weight:700;color:#111;letter-spacing:1.5px;text-transform:uppercase;"">City of Johannesburg</p>
  <p style=""margin:2px 0 0;font-size:11px;color:#111;letter-spacing:.8px;text-transform:uppercase;opacity:.75;"">Group Property &amp; Revenue – Valuation Services</p>
</td></tr>
<tr><td style=""background:#c49800;height:3px;font-size:0;""> </td></tr>
<tr><td style=""padding:32px 28px 24px;"">
  <p style=""margin:0 0 20px;font-size:15px;color:#111;font-weight:600;"">Good day,</p>
  <p style=""margin:0 0 24px;font-size:14px;color:#333;line-height:1.6;"">
    A new <strong>Valuation Verification extract</strong> has been uploaded and is ready for verification.</p>
  <table width=""100%"" cellpadding=""0"" cellspacing=""0"" border=""0""
    style=""background:#fafafa;border:1px solid #e8e8e8;border-radius:6px;margin-bottom:24px;"">
    <tr><td colspan=""2"" style=""background:#e6b000;padding:10px 18px;border-radius:6px 6px 0 0;"">
      <p style=""margin:0;font-size:12px;font-weight:700;color:#111;letter-spacing:1px;text-transform:uppercase;"">Batch Details</p>
    </td></tr>
    {DetailRow("Batch Reference", $"<strong>{batchName}</strong>")}
    {DetailRow("Upload Date", uploadDateStr)}
    {DetailRow("Extract Week", $"<strong>{weekRange}</strong>")}
    {DetailRow("Notice Send Date", $"<strong style='color:#b38a00;'>{noticeDateStr}</strong>", true)}
  </table>
  <table width=""100%"" cellpadding=""0"" cellspacing=""0"" border=""0""
    style=""background:#fafafa;border:1px solid #e8e8e8;border-radius:6px;margin-bottom:28px;"">
    <tr><td colspan=""2"" style=""background:#e6b000;padding:10px 18px;border-radius:6px 6px 0 0;"">
      <p style=""margin:0;font-size:12px;font-weight:700;color:#111;letter-spacing:1px;text-transform:uppercase;"">Records Summary</p>
    </td></tr>
    {DetailRow("Total Records", $"<strong>{totalRecords:N0}</strong>")}
    {DetailRow("Assigned", $"<strong style='color:#1a7a1a;'>{assignedRecords:N0}</strong>")}
    {DetailRow("Pending Assignment", $"<strong style='color:{(pendingRecords > 0 ? "#b34700" : "#1a7a1a")};'>{pendingRecords:N0}</strong>", true)}
  </table>
  <table width=""100%"" cellpadding=""0"" cellspacing=""0"" border=""0"" style=""margin-bottom:28px;"">
    <tr><td align=""center"">
      <a href=""{managerDashboardUrl}""
        style=""display:inline-block;padding:13px 32px;background:#e6b000;color:#111;
               font-size:14px;font-weight:700;text-decoration:none;border-radius:6px;
               border:2px solid #c49800;"">Open Manager Dashboard →</a>
    </td></tr>
  </table>
  <p style=""margin:0;font-size:12px;color:#888;line-height:1.5;border-top:1px solid #eee;padding-top:16px;"">
    Automated notification — please do not reply to this email.</p>
</td></tr>
<tr><td style=""background:#111;padding:18px 28px;"">
  <p style=""margin:0;font-size:11px;color:#fff;font-weight:600;"">City of Johannesburg Metropolitan Municipality</p>
  <p style=""margin:3px 0 0;font-size:10px;color:#aaa;"">Group Property &amp; Revenue | Valuation Services | Section 78 Process</p>
</td></tr>
<tr><td style=""background:#e6b000;height:4px;font-size:0;""> </td></tr>
</table></td></tr></table>
</body></html>";

            var text = $@"CITY OF JOHANNESBURG – Valuation Verification System
==================================================
Good day,
A new Valuation Verification extract has been uploaded and is ready for verification.

BATCH DETAILS
  Batch Reference : {batchName}
  Upload Date     : {uploadDateStr}
  Extract Week    : {weekRange}
  Notice Send Date: {noticeDateStr}

RECORDS SUMMARY
  Total Records   : {totalRecords:N0}
  Assigned        : {assignedRecords:N0}
  Pending         : {pendingRecords:N0}

Dashboard: {managerDashboardUrl}
--
Automated notification. City of Johannesburg | Valuation Services";

            return (subject, html, text);
        }

        // ─────────────────────────────────────────────────────────────────────
        // Template 2 – Section 78 Client Notice Email
        // ─────────────────────────────────────────────────────────────────────
        /// <summary>
        /// Sent to each property owner when their Section 78 notice is dispatched.
        /// </summary>
        /// <param name="propertyDescription">e.g. "RE PORTION 80 ERF 113 INANDA EXT.1"</param>
        /// <param name="letterDate">Date printed on the notice letter</param>
        /// <param name="reviewOpenDate">Date the objection window opens</param>
        /// <param name="reviewCloseDate">Deadline for objections</param>
        /// <param name="contactEmail">From EmailSettings.NoticeContactEmail</param>
        /// <param name="signOff">From EmailSettings.NoticeSignOff</param>
        /// <param name="municipalityName">From EmailSettings.NoticeMunicipalityName</param>
        /// <param name="actReference">From EmailSettings.NoticeActReference</param>
        public static (string subject, string html, string text) ClientNotice(
       string propertyDescription,
       DateOnly letterDate,
       DateOnly reviewOpenDate,
       DateOnly reviewCloseDate,
       string contactEmail,
       string signOff,
       string municipalityName,
       string actReference,
       string subjectPrefix = "GV2023 Section 78 Notice For : ")
        {
            var subject = $"{subjectPrefix}{propertyDescription}";

            var letterDateStr = letterDate.ToString("dd MMMM yyyy").ToUpperInvariant();
            var reviewOpenStr = reviewOpenDate.ToString("dd MMMM yyyy").ToUpperInvariant();
            var reviewCloseStr = reviewCloseDate.ToString("dd MMMM yyyy").ToUpperInvariant();
            var year = letterDate.Year;

            var html = $@"<!DOCTYPE html>
<html lang=""en"">
<head>
<meta charset=""utf-8""/>
<meta name=""viewport"" content=""width=device-width,initial-scale=1""/>
<title>Section 78 Notice – {propertyDescription}</title>
</head>

<body style=""margin:0;padding:0;background:#f0f0f0;font-family:'Segoe UI',Arial,sans-serif;"">

<table width=""100%"" cellpadding=""0"" cellspacing=""0"" border=""0"" style=""background:#f0f0f0;"">
<tr>
<td align=""center"" style=""padding:32px 16px;"">

<table width=""600"" cellpadding=""0"" cellspacing=""0"" border=""0""
style=""max-width:600px;width:100%;background:#fff;border-radius:10px;
overflow:hidden;box-shadow:0 4px 20px rgba(0,0,0,0.10);"">

<tr>
<td style=""background:#e6b000;height:6px;font-size:0;""></td>
</tr>

<tr>
<td style=""background:#1a1a1a;padding:22px 30px;"">

<table width=""100%"" cellpadding=""0"" cellspacing=""0"" border=""0"">
<tr>

<td>
<p style=""margin:0;font-size:16px;font-weight:700;color:#e6b000;
letter-spacing:1px;text-transform:uppercase;"">
{municipalityName}
</p>

<p style=""margin:4px 0 0;font-size:11px;color:#aaa;letter-spacing:.6px;
text-transform:uppercase;"">
Group Property &amp; Revenue — Valuation Services
</p>
</td>

<td align=""right"">
<p style=""margin:0;font-size:10px;color:#666;font-style:italic;"">
Section 78 Notice
</p>

<p style=""margin:2px 0 0;font-size:10px;color:#555;"">
{letterDateStr}
</p>
</td>

</tr>
</table>

</td>
</tr>

<tr>
<td style=""background:#fffbe6;border-top:2px solid #e6b000;
border-bottom:2px solid #e6b000;padding:10px 30px;text-align:center;"">

<p style=""margin:0;font-size:12px;font-weight:700;color:#7a5c00;
letter-spacing:1px;text-transform:uppercase;"">
*** THIS IS AN AUTOMATED MESSAGE ***
</p>

</td>
</tr>

<tr>
<td style=""padding:32px 30px 24px;"">

<p style=""margin:0 0 20px;font-size:15px;color:#111;font-weight:600;"">
Good Day,
</p>

<p style=""margin:0 0 24px;font-size:14px;color:#333;line-height:1.8;"">
Notice is hereby given in terms of
<strong>Section 78(5)(a)</strong> read together with
<strong>Section 78(5)(b)</strong> of the
<em>{actReference}</em>,
as amended — hereinafter referred to as the <strong>Act</strong> — that a
<strong>Supplementary Valuation</strong>
to the General Valuation Roll for the property described below has been completed.
</p>

<table width=""100%"" cellpadding=""0"" cellspacing=""0"" border=""0""
style=""background:#fafafa;border:1px solid #e0e0e0;border-radius:8px;margin-bottom:24px;overflow:hidden;"">

<tr>
<td style=""background:#e6b000;padding:10px 20px;"">
<p style=""margin:0;font-size:11px;font-weight:700;color:#111;
letter-spacing:1px;text-transform:uppercase;"">
Property
</p>
</td>
</tr>

<tr>
<td style=""padding:16px 20px;"">
<p style=""margin:0;font-size:15px;font-weight:700;color:#111;"">
{propertyDescription}
</p>
</td>
</tr>

</table>

<table width=""100%"" cellpadding=""0"" cellspacing=""0"" border=""0""
style=""background:#fafafa;border:1px solid #e0e0e0;border-radius:8px;margin-bottom:28px;overflow:hidden;"">

<tr>
<td style=""background:#1a1a1a;padding:10px 20px;"">
<p style=""margin:0;font-size:11px;font-weight:700;color:#e6b000;
letter-spacing:1px;text-transform:uppercase;"">
Important Dates
</p>
</td>
</tr>

{NoticeDetailRow("Letter Date", letterDateStr)}
{NoticeDetailRow("Objection Period Opens", reviewOpenStr)}
{NoticeDetailRow("Objection Deadline", reviewCloseStr, true)}

</table>

<p style=""margin:0 0 8px;font-size:14px;color:#333;line-height:1.7;"">
For further details please contact:
</p>

<p style=""margin:0 0 4px;font-size:14px;color:#333;padding-left:20px;"">
The COJ Valuations Directorate at the address on the notice.
</p>

<p style=""margin:0 0 24px;font-size:14px;color:#333;padding-left:20px;"">
or eMail:
<a href=""mailto:{contactEmail}"" style=""color:#b38a00;font-weight:600;text-decoration:none;"">
{contactEmail}
</a>
</p>

<p style=""margin:0;font-size:14px;color:#111;"">Regards,</p>
<p style=""margin:4px 0 0;font-size:14px;font-weight:700;color:#111;"">
{signOff}
</p>

</td>
</tr>

<tr>
<td style=""background:#1a1a1a;padding:16px 30px;"">

<table width=""100%"" cellpadding=""0"" cellspacing=""0"" border=""0"">
<tr>

<td>
<p style=""margin:0;font-size:11px;color:#fff;font-weight:600;"">
{municipalityName} Metropolitan Municipality
</p>

<p style=""margin:3px 0 0;font-size:10px;color:#888;"">
Group Property &amp; Revenue | Valuation Services | Section 78 Process
</p>
</td>

<td align=""right"">
<p style=""margin:0;font-size:10px;color:#555;"">
© {year} {municipalityName}
</p>
</td>

</tr>
</table>

</td>
</tr>

<tr>
<td style=""background:#e6b000;height:4px;font-size:0;""></td>
</tr>

</table>

</td>
</tr>
</table>

</body>
</html>";

            var text = $@"*** THIS IS AN AUTOMATED MESSAGE ***

Good Day,

Notice is hereby given in terms of Section 78(5)(a) read together with Section 78(5)(b)
of the {actReference}, as amended, that a Supplementary Valuation to the General
Valuation Roll for the property described below has been completed.

PROPERTY
{propertyDescription}

IMPORTANT DATES
Letter Date             : {letterDateStr}
Objection Period Opens  : {reviewOpenStr}
Objection Deadline      : {reviewCloseStr}

For further details please contact:
The COJ Valuations Directorate at the address on the notice.
or eMail: {contactEmail}

Regards,
{signOff}

--
{municipalityName} Metropolitan Municipality
Group Property & Revenue | Valuation Services | Section 78 Process";

            return (subject, html, text);
        }
        // ─────────────────────────────────────────────────────────────────────
        // Private helpers
        // ─────────────────────────────────────────────────────────────────────

        private static string DetailRow(string label, string valueHtml, bool last = false)
        {
            var border = last ? "" : "border-bottom:1px solid #eee;";
            return $@"<tr>
              <td style=""padding:10px 18px;font-size:13px;color:#666;font-weight:600;width:45%;{border}"">{label}</td>
              <td style=""padding:10px 18px;font-size:13px;color:#111;{border}"">{valueHtml}</td>
            </tr>";
        }

        private static string NoticeDetailRow(string label, string value, bool last = false)
        {
            var border = last ? "" : "border-bottom:1px solid #ebebeb;";
            return $@"<tr>
              <td style=""padding:12px 20px;font-size:13px;color:#666;font-weight:600;width:45%;{border}"">{label}</td>
              <td style=""padding:12px 20px;font-size:13px;color:#111;font-weight:700;{border}"">{value}</td>
            </tr>";
        }
    }
}