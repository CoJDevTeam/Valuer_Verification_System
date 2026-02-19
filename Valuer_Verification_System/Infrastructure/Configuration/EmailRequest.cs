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

        // optional: attachments later if you want
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
