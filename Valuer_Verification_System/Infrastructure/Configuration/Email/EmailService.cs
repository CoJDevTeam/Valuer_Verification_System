namespace Valuer_Verification_System.Infrastructure.Configuration.Email
{
    /// <summary>
    /// All outgoing email templates for the Valuer Verification System.
    /// Colours: Gold #e6b000 | Background #ffffff | Text #111111
    /// </summary>
    public static class EmailTemplates
    {
        // ─────────────────────────────────────────────────────────────────────
        // Template 1 – Extract Uploaded → Notify Sector Managers
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Sent to all active sector managers when a new extract batch is uploaded
        /// and ready for valuer verification.
        /// </summary>
        public static (string subject, string html, string text) ManagersUploadReady(
            string batchName,
            DateOnly batchDate,
            DateOnly weekStart,
            DateOnly weekEnd,
            DateOnly noticeSendDate,
            int totalRecords,
            int assignedRecords,
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
<head>
  <meta charset=""utf-8""/>
  <meta name=""viewport"" content=""width=device-width, initial-scale=1""/>
  <!--[if mso]><noscript><xml><o:OfficeDocumentSettings><o:PixelsPerInch>96</o:PixelsPerInch></o:OfficeDocumentSettings></xml></noscript><![endif]-->
  <title>Valuer Verification – Extract Uploaded</title>
</head>
<body style=""margin:0;padding:0;background-color:#f4f4f4;font-family:'Segoe UI',Arial,sans-serif;"">

  <!-- Outer wrapper -->
  <table width=""100%"" cellpadding=""0"" cellspacing=""0"" border=""0"" style=""background-color:#f4f4f4;"">
    <tr>
      <td align=""center"" style=""padding:32px 16px;"">

        <!-- Email card -->
        <table width=""620"" cellpadding=""0"" cellspacing=""0"" border=""0""
               style=""max-width:620px;width:100%;background-color:#ffffff;
                      border-radius:8px;overflow:hidden;
                      box-shadow:0 2px 8px rgba(0,0,0,0.08);"">

          <!-- ═══ GOLD HEADER BAR ═══ -->
          <tr>
            <td style=""background-color:#e6b000;padding:0;"">
              <table width=""100%"" cellpadding=""0"" cellspacing=""0"" border=""0"">
                <tr>
                  <!-- Logo area -->
                  <td style=""padding:20px 28px;"">
                    <p style=""margin:0;font-size:13px;font-weight:700;color:#111111;
                               letter-spacing:1.5px;text-transform:uppercase;"">
                      City of Johannesburg
                    </p>
                    <p style=""margin:2px 0 0 0;font-size:11px;font-weight:600;color:#111111;
                               letter-spacing:0.8px;text-transform:uppercase;opacity:0.75;"">
                      Group Property &amp; Revenue – Valuation Services
                    </p>
                  </td>
                  <!-- System label -->
                  <td align=""right"" style=""padding:20px 28px;"">
                    <p style=""margin:0;font-size:11px;font-weight:700;color:#111111;
                               letter-spacing:1px;text-transform:uppercase;opacity:0.75;"">
                      Valuer Verification System
                    </p>
                  </td>
                </tr>
              </table>
            </td>
          </tr>

          <!-- ═══ GOLD ACCENT LINE ═══ -->
          <tr>
            <td style=""background-color:#c49800;height:3px;font-size:0;line-height:0;"">&nbsp;</td>
          </tr>

          <!-- ═══ BODY ═══ -->
          <tr>
            <td style=""padding:32px 28px 24px 28px;"">

              <!-- Greeting -->
              <p style=""margin:0 0 20px 0;font-size:15px;color:#111111;font-weight:600;"">
                Good day,
              </p>

              <!-- Intro paragraph -->
              <p style=""margin:0 0 24px 0;font-size:14px;color:#333333;line-height:1.6;"">
                A new <strong>Valuer Verification extract</strong> has been uploaded to the system
                and is ready for allocation tracking and verification.
                Please review the details below and action any outstanding assignments.
              </p>

              <!-- ── BATCH DETAILS CARD ── -->
              <table width=""100%"" cellpadding=""0"" cellspacing=""0"" border=""0""
                     style=""background-color:#fafafa;border:1px solid #e8e8e8;
                            border-radius:6px;margin-bottom:24px;"">
                <!-- Card header -->
                <tr>
                  <td colspan=""2""
                      style=""background-color:#e6b000;padding:10px 18px;
                             border-radius:6px 6px 0 0;"">
                    <p style=""margin:0;font-size:12px;font-weight:700;color:#111111;
                               letter-spacing:1px;text-transform:uppercase;"">
                      Batch Details
                    </p>
                  </td>
                </tr>
                <!-- Rows -->
                {DetailRow("Batch Reference", $"<strong>{batchName}</strong>")}
                {DetailRow("Upload Date", uploadDateStr)}
                {DetailRow("Extract Week", $"<strong>{weekRange}</strong>")}
                {DetailRow("Notice Send Date", $"<strong style='color:#b38a00;'>{noticeDateStr}</strong>")}
              </table>

              <!-- ── RECORDS SUMMARY CARD ── -->
              <table width=""100%"" cellpadding=""0"" cellspacing=""0"" border=""0""
                     style=""background-color:#fafafa;border:1px solid #e8e8e8;
                            border-radius:6px;margin-bottom:28px;"">
                <!-- Card header -->
                <tr>
                  <td colspan=""2""
                      style=""background-color:#e6b000;padding:10px 18px;
                             border-radius:6px 6px 0 0;"">
                    <p style=""margin:0;font-size:12px;font-weight:700;color:#111111;
                               letter-spacing:1px;text-transform:uppercase;"">
                      Records Summary
                    </p>
                  </td>
                </tr>
                {DetailRow("Total Records", $"<strong>{totalRecords:N0}</strong>")}
                {DetailRow("Assigned", $"<strong style='color:#1a7a1a;'>{assignedRecords:N0}</strong>")}
                {DetailRow("Pending Assignment", $"<strong style='color:{(pendingRecords > 0 ? "#b34700" : "#1a7a1a")};'>{pendingRecords:N0}</strong>")}
              </table>

              <!-- ── CTA BUTTON ── -->
              <table width=""100%"" cellpadding=""0"" cellspacing=""0"" border=""0""
                     style=""margin-bottom:28px;"">
                <tr>
                  <td align=""center"">
                    <a href=""{managerDashboardUrl}""
                       style=""display:inline-block;padding:13px 32px;
                              background-color:#e6b000;color:#111111;
                              font-size:14px;font-weight:700;text-decoration:none;
                              border-radius:6px;letter-spacing:0.5px;
                              border:2px solid #c49800;"">
                      Open Manager Dashboard →
                    </a>
                  </td>
                </tr>
              </table>

              <!-- Note -->
              <p style=""margin:0;font-size:12px;color:#888888;line-height:1.5;
                         border-top:1px solid #eeeeee;padding-top:16px;"">
                This is an automated notification from the Valuer Verification System.
                Please do not reply directly to this email.
                For queries contact <a href=""mailto:propertyinfo@joburg.org.za""
                style=""color:#b38a00;text-decoration:none;"">propertyinfo@joburg.org.za</a>.
              </p>

            </td>
          </tr>

          <!-- ═══ FOOTER ═══ -->
          <tr>
            <td style=""background-color:#111111;padding:18px 28px;"">
              <table width=""100%"" cellpadding=""0"" cellspacing=""0"" border=""0"">
                <tr>
                  <td>
                    <p style=""margin:0;font-size:11px;color:#ffffff;font-weight:600;"">
                      City of Johannesburg Metropolitan Municipality
                    </p>
                    <p style=""margin:3px 0 0 0;font-size:10px;color:#aaaaaa;"">
                      Group Property &amp; Revenue | Valuation Services | Section 78 Process
                    </p>
                  </td>
                  <td align=""right"">
                    <p style=""margin:0;font-size:10px;color:#666666;"">
                      &copy; {year} City of Johannesburg
                    </p>
                  </td>
                </tr>
              </table>
            </td>
          </tr>

          <!-- Bottom gold rule -->
          <tr>
            <td style=""background-color:#e6b000;height:4px;font-size:0;line-height:0;"">&nbsp;</td>
          </tr>

        </table>
        <!-- /card -->

      </td>
    </tr>
  </table>

</body>
</html>";

            var text = $@"
CITY OF JOHANNESBURG – VALUER VERIFICATION SYSTEM
==================================================

Good day,

A new Valuer Verification extract has been uploaded and is ready for verification.

BATCH DETAILS
  Batch Reference : {batchName}
  Upload Date     : {uploadDateStr}
  Extract Week    : {weekRange}
  Notice Send Date: {noticeDateStr}

RECORDS SUMMARY
  Total Records   : {totalRecords:N0}
  Assigned        : {assignedRecords:N0}
  Pending         : {pendingRecords:N0}

Please log in to the Manager Dashboard to monitor progress and action any pending assignments:
  {managerDashboardUrl}

--
This is an automated notification. Please do not reply to this email.
City of Johannesburg | Group Property & Revenue | Valuation Services
";

            return (subject, html, text);
        }

        // ─────────────────────────────────────────────────────────────────────
        // Private helpers
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>Renders a single label/value row inside a detail card table.</summary>
        private static string DetailRow(string label, string valueHtml, bool lastRow = false)
        {
            var border = lastRow ? "" : "border-bottom:1px solid #eeeeee;";
            return $@"
                <tr>
                  <td style=""padding:10px 18px;font-size:13px;color:#666666;
                             font-weight:600;width:45%;{border}"">
                    {label}
                  </td>
                  <td style=""padding:10px 18px;font-size:13px;color:#111111;{border}"">
                    {valueHtml}
                  </td>
                </tr>";
        }
    }
}