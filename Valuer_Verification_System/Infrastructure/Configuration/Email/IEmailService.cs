using Valuer_Verification_System.Infrastructure.Configuration;

namespace Valuer_Verification_System.Infrastructure.Configuration.Email
{
    public interface IEmailService
    {
        /// <summary>
        /// Sends the email and saves an RFC822 .eml copy to disk.
        /// </summary>
        Task SendAndArchiveAsync(
            EmailRequest req,
            string archiveEmlPath,
            CancellationToken ct = default);
    }
}
