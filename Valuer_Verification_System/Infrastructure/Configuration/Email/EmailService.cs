using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Options;
using MimeKit;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Valuer_Verification_System.Infrastructure.Configuration.Email
{
    public sealed class EmailService : IEmailService
    {
        private readonly EmailSettings _cfg;

        public EmailService(IOptions<EmailSettings> cfg)
        {
            _cfg = cfg.Value;
        }

        public async Task SendAndArchiveAsync(
            EmailRequest req,
            string archiveEmlPath,
            CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(req.To))
                throw new ArgumentException("To is required.", nameof(req));

            if (string.IsNullOrWhiteSpace(req.Subject))
                throw new ArgumentException("Subject is required.", nameof(req));

            // Build MIME message (also what we'll save as .eml)
            var msg = new MimeMessage();
            msg.From.Add(MailboxAddress.Parse(_cfg.FromAddress));
            msg.To.Add(MailboxAddress.Parse(req.To));

            foreach (var cc in req.Cc.Where(x => !string.IsNullOrWhiteSpace(x)))
                msg.Cc.Add(MailboxAddress.Parse(cc));

            // Add default BCCs from config
            var allBcc = new List<string>();
            if (!string.IsNullOrWhiteSpace(_cfg.DefaultBcc)) allBcc.Add(_cfg.DefaultBcc);
            if (!string.IsNullOrWhiteSpace(_cfg.BccAddress)) allBcc.Add(_cfg.BccAddress);
            allBcc.AddRange(req.Bcc.Where(x => !string.IsNullOrWhiteSpace(x)));

            foreach (var bcc in allBcc.Distinct(StringComparer.OrdinalIgnoreCase))
                msg.Bcc.Add(MailboxAddress.Parse(bcc));

            msg.Subject = req.Subject;

            var builder = new BodyBuilder
            {
                TextBody = req.BodyText
            };

            if (!string.IsNullOrWhiteSpace(req.BodyHtml))
                builder.HtmlBody = req.BodyHtml;

            // Attachments (optional)
            foreach (var a in req.Attachments)
            {
                if (a.Content is null || a.Content.Length == 0) continue;
                builder.Attachments.Add(a.FileName, a.Content, ContentType.Parse(a.ContentType));
            }

            msg.Body = builder.ToMessageBody();

            // Ensure directory exists and save .eml FIRST (so even send failures can be audited)
            var dir = Path.GetDirectoryName(archiveEmlPath);
            if (!string.IsNullOrWhiteSpace(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            await using (var fs = File.Create(archiveEmlPath))
            {
                await msg.WriteToAsync(fs, ct);
            }

            // Send email using MailKit SmtpClient
            using var smtp = new MailKit.Net.Smtp.SmtpClient();

            await smtp.ConnectAsync(
                _cfg.Host,
                _cfg.Port,
                _cfg.EnableSsl ? SecureSocketOptions.StartTlsWhenAvailable : SecureSocketOptions.None,
                ct);

            if (!string.IsNullOrWhiteSpace(_cfg.Username))
                await smtp.AuthenticateAsync(_cfg.Username, _cfg.Password ?? "", ct);

            await smtp.SendAsync(msg, ct);
            await smtp.DisconnectAsync(true, ct);
        }
    }
}
