using Valuer_Verification_System.Services.Manager;

namespace Valuer_Verification_System.Models.ViewModels
{
    public sealed class ReassignVm
    {
        public int BatchId { get; set; }

        public string? CurrentValuer { get; set; }
        public string? Sector { get; set; }

        public int Top { get; set; } = 500;

        public List<BatchVm> Batches { get; set; } = new();

        /// <summary>Valuers who hold Pending rows now (filter and bulk "from").</summary>
        public List<CurrentValuerDto> CurrentValuers { get; set; } = new();

        /// <summary>Active users the manager can reassign to (modal).</summary>
        public List<ActiveValuerDto> ActiveValuers { get; set; } = new();

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
            public string? PremiseId { get; set; }
            public string? Sector { get; set; }
            public string? PropertyDescription { get; set; }
            public string? Address { get; set; }
            public string? Category { get; set; }
            public decimal? MarketValue { get; set; }
            public string? Owner { get; set; }
            public string? SchemeName { get; set; }
            public string? UnitNo { get; set; }
            public string? Town { get; set; }
            public string? ExtractValuer { get; set; }
            public string CurrentValuer { get; set; } = "";
        }
    }
}
