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

        public EmailService(IOptions<EmailSettings> opt) => _s = opt.Value;

        public async Task<EmailSendResult> SendAndArchiveAsync(
            EmailRequest req, string archiveEmlPath, CancellationToken ct)
        {
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

            if (!string.IsNullOrWhiteSpace(_s.DevRedirectAllTo))
            {
                allTo = new List<string> { _s.DevRedirectAllTo.Trim() };
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

            msg.Subject = req.Subject;

            var builder = new BodyBuilder { HtmlBody = req.BodyHtml, TextBody = req.BodyText };
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