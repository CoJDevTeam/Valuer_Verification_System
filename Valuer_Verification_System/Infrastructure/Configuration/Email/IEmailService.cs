using Valuer_Verification_System.Infrastructure.Configuration;

namespace Valuer_Verification_System.Infrastructure.Configuration.Email
{
    using MailKit.Net.Smtp;
    using Microsoft.Extensions.Options;
    using MimeKit;

    namespace Valuer_Verification_System.Infrastructure.Configuration.Email
    {
        public interface IEmailService
        {
            Task<EmailSendResult> SendAndArchiveAsync(EmailRequest req, string archiveEmlPath, CancellationToken ct);
        }

        public sealed class EmailService : IEmailService
        {
            private readonly EmailSettings _s;

            public EmailService(IOptions<EmailSettings> opt)
            {
                _s = opt.Value;
            }

            public async Task<EmailSendResult> SendAndArchiveAsync(EmailRequest req, string archiveEmlPath, CancellationToken ct)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(archiveEmlPath)!);

                // Build message
                var msg = BuildMessage(req, out var finalToDisplay);

                // Always archive first (audit)
                await using (var fs = File.Create(archiveEmlPath))
                    await msg.WriteToAsync(fs, ct);

                // If sending disabled -> finish
                if (!_s.EnableSending)
                {
                    return new EmailSendResult
                    {
                        Sent = false,
                        Error = "Email sending disabled (Email:EnableSending=false). Archived only.",
                        ArchivePath = archiveEmlPath,
                        FinalTo = finalToDisplay
                    };
                }

                // Try send; never throw out of here
                try
                {
                    using var smtp = new SmtpClient();
                    await smtp.ConnectAsync(_s.Host, _s.Port, _s.EnableSsl, ct);

                    // If you later add auth:
                    // if(!string.IsNullOrWhiteSpace(_s.Username)) await smtp.AuthenticateAsync(_s.Username,_s.Password,ct);

                    await smtp.SendAsync(msg, ct);
                    await smtp.DisconnectAsync(true, ct);

                    return new EmailSendResult
                    {
                        Sent = true,
                        ArchivePath = archiveEmlPath,
                        FinalTo = finalToDisplay
                    };
                }
                catch (Exception ex)
                {
                    // ✅ swallow + return result
                    return new EmailSendResult
                    {
                        Sent = false,
                        Error = ex.Message,
                        ArchivePath = archiveEmlPath,
                        FinalTo = finalToDisplay
                    };
                }
            }

            private MimeMessage BuildMessage(EmailRequest req, out string finalToDisplay)
            {
                var msg = new MimeMessage();

                var fromName = string.IsNullOrWhiteSpace(_s.FromDisplayName)
                    ? "City of Johannesburg - Valuation Services"
                    : _s.FromDisplayName;

                msg.From.Add(new MailboxAddress(fromName, _s.FromAddress));

                // ✅ Redirect logic for DEV/relay issues
                var allTo = req.To?.Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x.Trim()).Distinct().ToList()
                          ?? new List<string>();

                var allCc = req.Cc?.Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x.Trim()).Distinct().ToList()
                          ?? new List<string>();

                var allBcc = req.Bcc?.Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x.Trim()).Distinct().ToList()
                           ?? new List<string>();

                if (!string.IsNullOrWhiteSpace(_s.DefaultBcc))
                    allBcc.Add(_s.DefaultBcc.Trim());

                // If DevRedirectAllTo set -> override all recipients
                if (!string.IsNullOrWhiteSpace(_s.DevRedirectAllTo))
                {
                    allTo = new List<string> { _s.DevRedirectAllTo.Trim() };
                    allCc.Clear();
                    allBcc.Clear();
                    msg.Headers.Add("X-VVS-Redirected", "true");
                }
                else
                {
                    // If AllowedRecipientDomains specified -> keep only those domains (optional hard safety)
                    if (_s.AllowedRecipientDomains != null && _s.AllowedRecipientDomains.Count > 0)
                    {
                        bool Allowed(string email)
                        {
                            var at = email.LastIndexOf('@');
                            if (at < 0) return false;
                            var dom = email[(at + 1)..].Trim().ToLowerInvariant();
                            return _s.AllowedRecipientDomains.Any(d => string.Equals(d.Trim().ToLowerInvariant(), dom, StringComparison.OrdinalIgnoreCase));
                        }

                        allTo = allTo.Where(Allowed).ToList();
                        allCc = allCc.Where(Allowed).ToList();
                        allBcc = allBcc.Where(Allowed).ToList();
                    }
                }

                foreach (var t in allTo) msg.To.Add(MailboxAddress.Parse(t));
                foreach (var c in allCc) msg.Cc.Add(MailboxAddress.Parse(c));
                foreach (var b in allBcc) msg.Bcc.Add(MailboxAddress.Parse(b));

                msg.Subject = req.Subject;

                var builder = new BodyBuilder
                {
                    HtmlBody = req.BodyHtml,
                    TextBody = req.BodyText
                };
                msg.Body = builder.ToMessageBody();

                finalToDisplay = string.Join("; ", allTo);
                return msg;
            }
        }
    }

}
