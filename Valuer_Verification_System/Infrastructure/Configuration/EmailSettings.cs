namespace Valuer_Verification_System.Infrastructure.Configuration
{
    public sealed class EmailSettings
    {
        public string Host { get; set; } = "";
        public int Port { get; set; }
        public bool EnableSsl { get; set; }

        public string FromAddress { get; set; } = "";
        public string DefaultBcc { get; set; } = "";
        public string BccAddress { get; set; } = "";

        public string? Username { get; set; }
        public string? Password { get; set; }
    }
}
