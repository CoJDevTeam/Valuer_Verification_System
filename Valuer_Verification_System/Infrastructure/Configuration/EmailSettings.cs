namespace Valuer_Verification_System.Infrastructure.Configuration
{
    public sealed class EmailSettings
    {
        // ── SMTP ──────────────────────────────────────────────────────────────
        public string Host { get; set; } = "";
        public int Port { get; set; }
        public bool EnableSsl { get; set; }
        public string? Username { get; set; }
        public string? Password { get; set; }

        // ── General sender (manager notifications) ────────────────────────────
        public string FromAddress { get; set; } = "";
        public string FromDisplayName { get; set; } = "";
        public string DefaultBcc { get; set; } = "";
        public string BccAddress { get; set; } = "";

        // ── Dev / test helpers ────────────────────────────────────────────────
        public bool EnableSending { get; set; } = true;
        public string? DevRedirectAllTo { get; set; }
        public List<string> AllowedRecipientDomains { get; set; } = new();

        // ── Section 78 Client Notice email ───────────────────────────────────
        /// <summary>Display name on client notices  e.g. "Valuation Queries"</summary>
        public string NoticeFromDisplayName { get; set; } = "Valuation Queries";

        /// <summary>From address on client notices</summary>
        public string NoticeFromAddress { get; set; } = "ValuationEnquiries@joburg.org.za";

        /// <summary>Reply-To on client notices (often same as From)</summary>
        public string NoticeReplyTo { get; set; } = "ValuationEnquiries@joburg.org.za";

        /// <summary>Subject prefix e.g. "GV2023 Section 78 Notice For : "</summary>
        public string NoticeSubjectPrefix { get; set; } = "GV2023 Section 78 Notice For : ";

        /// <summary>Contact email shown in body of each notice</summary>
        public string NoticeContactEmail { get; set; } = "ValuationEnquiries@joburg.org.za";

        /// <summary>Sign-off line at the bottom of each notice</summary>
        public string NoticeSignOff { get; set; } = "Municipal Valuer";

        /// <summary>Municipality name shown in notice header</summary>
        public string NoticeMunicipalityName { get; set; } = "City of Johannesburg";

        /// <summary>Rates Act reference shown in notice body</summary>
        public string NoticeActReference { get; set; } =
            "Local Government: Municipal Property Rates Act, 2004 (Act No. 6 of 2004)";

        public string NoticeQaManagerEmail { get; set; } = "";
    }
}