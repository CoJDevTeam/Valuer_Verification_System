namespace Valuer_Verification_System.Infrastructure.Configuration
{
    public sealed class StorageSettings
    {
        public string RootPath { get; set; } = "";

        public string ExtractsFolder { get; set; } = "Extracts";
        public string NoticesFolder { get; set; } = "Notices";
        public string StatsFolder { get; set; } = "Stats";

        public string ExtractsPath => Path.Combine(RootPath, ExtractsFolder);
        public string NoticesPath => Path.Combine(RootPath, NoticesFolder);
        public string StatsPath => Path.Combine(RootPath, StatsFolder);
    }
}
