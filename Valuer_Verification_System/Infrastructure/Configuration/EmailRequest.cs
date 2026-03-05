namespace Valuer_Verification_System.Infrastructure.Configuration
{
    public sealed class EmailRequest
    {
        public List<string>? To { get; set; }
        public string Subject { get; set; } = "";
        public string BodyText { get; set; } = "";
        public string? BodyHtml { get; set; }
        public List<string> Cc { get; set; } = new();
        public List<string> Bcc { get; set; } = new();
        public string? ReplyTo { get; set; }   // ← added for notice emails
        public string? FromAddress { get; set; }   // ← override From per-email
        public string? FromDisplay { get; set; }   // ← override display name per-email
        public List<EmailAttachment> Attachments { get; set; } = new();
    }

    public sealed class EmailSendResult
    {
        public bool Sent { get; set; }
        public string? Error { get; set; }
        public string ArchivePath { get; set; } = "";
        public string FinalTo { get; set; } = "";
    }

    public sealed class EmailAttachment
    {
        public string FileName { get; set; } = "";
        public string ContentType { get; set; } = "application/octet-stream";
        public byte[] Content { get; set; } = Array.Empty<byte>();
    }
}