namespace Valuer_Verification_System.Infrastructure.Configuration
{
    public class ExtractInboxSettings
    {
        public string FolderPath { get; set; } = "";
        public string ArchivePath { get; set; } = "";
        public string FailedPath { get; set; } = "";

        // Daily job will run once per day after this hour.
        // Example: 6 means after 06:00.
        public int RunAfterHour { get; set; } = 6;

        public string FileNamePattern { get; set; } =
            @"^LIS_SV_(\d{8})_To_(\d{8})\.(xls|xlsx|csv)$";
        public bool AutoCleanAfterImport { get; set; } = true;

    }
}
