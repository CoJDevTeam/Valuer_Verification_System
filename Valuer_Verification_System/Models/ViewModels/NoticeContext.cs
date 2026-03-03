namespace Valuer_Verification_System.Models.Notice
{
    /// <summary>
    /// Batch-level context used when generating all notices for a single send run.
    /// These values are identical for every property in the batch — calculated once
    /// from the WeekWindow and passed into each PDF generation call.
    /// </summary>
    public sealed class NoticeContext
    {
        /// <summary>
        /// The date printed on every notice as the formal letter date.
        /// Equals the WeekWindow.NoticeSendDate (always a Friday, or Thursday if Friday is a holiday).
        /// </summary>
        public DateOnly LetterDate { get; set; }

        /// <summary>
        /// The first date of the review period.
        /// Calculated as the last Friday of the same calendar month as LetterDate.
        /// If that Friday is a public holiday, the last Thursday of the month is used instead.
        /// </summary>
        public DateOnly ReviewOpenDate { get; set; }

        /// <summary>
        /// The last date of the review period.
        /// Calculated as ReviewOpenDate + 60 calendar days.
        /// </summary>
        public DateOnly ReviewCloseDate { get; set; }

        /// <summary>
        /// Absolute file-system path to City_of_Joburg_logo.png (from wwwroot/Images/).
        /// Used by QuestPDF when rendering the notice header.
        /// If the file does not exist, the PDF falls back to plain text.
        /// </summary>
        public string LogoPath { get; set; } = string.Empty;

        /// <summary>
        /// The year of the General Valuation Roll being supplemented.
        /// Printed in the notice title: "GENERAL VALUATION ROLL {GvRollYear}".
        /// Defaults to 2023.
        /// </summary>
        public int GvRollYear { get; set; } = 2023;
    }
}