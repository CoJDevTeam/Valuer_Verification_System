namespace Valuer_Verification_System.Infrastructure.Configuration
{
   
        public sealed class ExtractInboxSettings
        {
            public string FolderPath { get; set; } = "";

            // Daily job will run once per day after this hour.
            // Example: 6 means after 06:00 South African time.
            public int RunAfterHour { get; set; } = 6;

            public string FileNamePattern { get; set; } =
                @"^LIS_SV_(\d{8})_To_(\d{8})\.(xls|xlsx|csv)$";

            public bool AutoCleanAfterImport { get; set; } = true;
        }
    }

