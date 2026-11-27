using MailKit.Net.Smtp;
using Microsoft.Extensions.Options;
using MimeKit;

namespace Valuer_Verification_System.Infrastructure.Configuration.Email
{
    public interface IEmailService
    {
        Task<EmailSendResult> SendAndArchiveAsync(
            EmailRequest req, string archiveEmlPath, CancellationToken ct);
    }

    public sealed class EmailService : IEmailService
    {
        private readonly EmailSettings _s;
        private readonly IAppMode _mode;

        public EmailService(IOptions<EmailSettings> opt, IAppMode mode)
        {
            _s = opt.Value;
            _mode = mode;
        }

        public async Task<EmailSendResult> SendAndArchiveAsync(
            EmailRequest req, string archiveEmlPath, CancellationToken ct)
        {
            // Not Production and nobody to send to: send nothing, so a test can never reach a real person.
            if (!_mode.IsProduction && _mode.RedirectEmails.Count == 0)
                return new EmailSendResult
                {
                    Sent = false,
                    Error = $"AppMode is {_mode.Label} but it has no email address (AppMode:Developer or AppMode:Testers). Nothing was sent.",
                    ArchivePath = archiveEmlPath,
                    FinalTo = ""
                };

            Directory.CreateDirectory(Path.GetDirectoryName(archiveEmlPath)!);

            var msg = BuildMessage(req, out var finalToDisplay);

            // Always archive first (audit trail)
            await using (var fs = File.Create(archiveEmlPath))
                await msg.WriteToAsync(fs, ct);

            if (!_s.EnableSending)
                return new EmailSendResult
                {
                    Sent = false,
                    Error = "Email sending disabled (Email:EnableSending=false). Archived only.",
                    ArchivePath = archiveEmlPath,
                    FinalTo = finalToDisplay
                };

            try
            {
                using var smtp = new SmtpClient();
                await smtp.ConnectAsync(_s.Host, _s.Port, _s.EnableSsl, ct);

                if (!string.IsNullOrWhiteSpace(_s.Username))
                    await smtp.AuthenticateAsync(_s.Username, _s.Password ?? "", ct);

                await smtp.SendAsync(msg, ct);
                await smtp.DisconnectAsync(true, ct);

                return new EmailSendResult { Sent = true, ArchivePath = archiveEmlPath, FinalTo = finalToDisplay };
            }
            catch (Exception ex)
            {
                return new EmailSendResult { Sent = false, Error = ex.Message, ArchivePath = archiveEmlPath, FinalTo = finalToDisplay };
            }
        }

        private MimeMessage BuildMessage(EmailRequest req, out string finalToDisplay)
        {
            var msg = new MimeMessage();

            // ── From ──────────────────────────────────────────────────────────
            var fromAddr = req.FromAddress ?? _s.FromAddress;
            var fromDisplay = req.FromDisplay ?? (_s.FromDisplayName.Length > 0
                                ? _s.FromDisplayName
                                : "City of Johannesburg - Valuation Services");
            msg.From.Add(new MailboxAddress(fromDisplay, fromAddr));

            // ── Reply-To ──────────────────────────────────────────────────────
            if (!string.IsNullOrWhiteSpace(req.ReplyTo))
                msg.ReplyTo.Add(MailboxAddress.Parse(req.ReplyTo));

            // ── Recipients ────────────────────────────────────────────────────
            var allTo = Sanitise(req.To);
            var allCc = Sanitise(req.Cc);
            var allBcc = Sanitise(req.Bcc);

            if (!string.IsNullOrWhiteSpace(_s.DefaultBcc)) allBcc.Add(_s.DefaultBcc.Trim());

            string? redirectNote = null;

            if (!_mode.IsProduction)
            {
                // TEST: remember who this email was meant for, so it can be told apart in one inbox.
                redirectNote = $"{_mode.Label.ToUpperInvariant()} MODE. This email was meant for - To: {string.Join("; ", allTo)}"
                             + (allCc.Count > 0 ? $" | Cc: {string.Join("; ", allCc)}" : "");

                allTo = _mode.RedirectEmails.ToList();
                allCc.Clear();
                allBcc.Clear();
                msg.Headers.Add("X-VVS-Redirected", "true");
            }
            else if (_s.AllowedRecipientDomains?.Count > 0)
            {
                bool Allowed(string e)
                {
                    var at = e.LastIndexOf('@');
                    if (at < 0) return false;
                    var dom = e[(at + 1)..].Trim().ToLowerInvariant();
                    return _s.AllowedRecipientDomains.Any(d =>
                        string.Equals(d.Trim(), dom, StringComparison.OrdinalIgnoreCase));
                }
                allTo = allTo.Where(Allowed).ToList();
                allCc = allCc.Where(Allowed).ToList();
                allBcc = allBcc.Where(Allowed).ToList();
            }

            foreach (var t in allTo) msg.To.Add(MailboxAddress.Parse(t));
            foreach (var c in allCc) msg.Cc.Add(MailboxAddress.Parse(c));
            foreach (var b in allBcc) msg.Bcc.Add(MailboxAddress.Parse(b));

            msg.Subject = redirectNote is null ? req.Subject : $"[{_mode.Label.ToUpperInvariant()}] {req.Subject}";

            var banner = redirectNote is null
                ? ""
                : "<div style='background:#fff3cd;border:1px solid #ffe69c;padding:8px 12px;margin-bottom:10px;" +
                  "font:12px Segoe UI,Arial,sans-serif;'>" + System.Net.WebUtility.HtmlEncode(redirectNote) + "</div>";

            var builder = new BodyBuilder
            {
                HtmlBody = req.BodyHtml is null ? null : banner + req.BodyHtml,
                TextBody = redirectNote is null ? req.BodyText : redirectNote + Environment.NewLine + Environment.NewLine + req.BodyText
            };
            foreach (var a in req.Attachments.Where(a => a.Content.Length > 0))
                builder.Attachments.Add(a.FileName, a.Content, MimeKit.ContentType.Parse(a.ContentType));

            msg.Body = builder.ToMessageBody();

            finalToDisplay = string.Join("; ", allTo);
            return msg;
        }

        private static List<string> Sanitise(IEnumerable<string>? src) =>
            src?.Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x.Trim()).Distinct().ToList()
            ?? new List<string>();
    }
}