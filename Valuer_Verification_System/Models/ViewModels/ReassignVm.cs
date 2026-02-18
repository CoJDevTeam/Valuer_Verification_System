namespace Valuer_Verification_System.Models.ViewModels
{
    public sealed class ReassignVm
    {
        public int BatchId { get; set; }

        public string? CurrentValuer { get; set; }
        public string? Sector { get; set; }

        public int Top { get; set; } = 500;

        public List<BatchVm> Batches { get; set; } = new();
        public List<string> Valuers { get; set; } = new();
        public List<string> Sectors { get; set; } = new();

        public List<RowVm> Rows { get; set; } = new();

        public sealed class BatchVm
        {
            public int Id { get; set; }
            public string BatchName { get; set; } = "";
            public DateOnly BatchDate { get; set; }
        }

        public sealed class RowVm
        {
            public int ExtractRowId { get; set; }
            public string ValuationKey { get; set; } = "";
            public string? Sector { get; set; }
            public string? PropertyDescription { get; set; }
            public string CurrentValuer { get; set; } = "";
        }
    }
}