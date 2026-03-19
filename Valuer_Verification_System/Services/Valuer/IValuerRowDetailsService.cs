using Microsoft.EntityFrameworkCore;
using Valuer_Verification_System.Data;
using Valuer_Verification_System.Models.Data;
using Valuer_Verification_System.Models.ViewModels;

namespace Valuer_Verification_System.Services.Valuer
{
    public interface IValuerRowDetailsService
    {
        Task<ValuerRowDetailsVm?> GetAsync(int extractRowId, CancellationToken ct);
    }

    public sealed class ValuerRowDetailsService : IValuerRowDetailsService
    {
        private readonly VvsDbContext _db;

        public ValuerRowDetailsService(VvsDbContext db) => _db = db;

        public async Task<ValuerRowDetailsVm?> GetAsync(int extractRowId, CancellationToken ct)
        {
            var row = await _db.ExtractRows
                .AsNoTracking()
                .Include(r => r.Batch)
                .Include(r => r.Assignment)
                .FirstOrDefaultAsync(r => r.Id == extractRowId, ct);

            if (row is null) return null;

            // All rows sharing the same ValuationKey in the same batch (siblings)
            var siblings = await _db.ExtractRows
                .AsNoTracking()
                .Where(r => r.BatchId == row.BatchId
                         && r.ValuationKey == row.ValuationKey)
                .OrderBy(r => r.ValuationSplitNo)
                .ToListAsync(ct);

            bool isMulti = siblings.Count > 1 ||
                siblings.Any(r =>
                    !string.IsNullOrWhiteSpace(r.ValuationSplitInd) &&
                    r.ValuationSplitInd.Trim().ToUpper() is "Y" or "P");

            return new ValuerRowDetailsVm
            {
                // ── Identity ────────────────────────────────────────────────
                ExtractRowId = row.Id,
                ValuationKey = row.ValuationKey,
                BatchId = row.BatchId,
                BatchName = row.Batch?.BatchName ?? "",

                // ── Property ────────────────────────────────────────────────
                PropertyDesc = row.PropertyDesc,
                PropertyDescription = row.PropertyDescription,
                TownNameDesc = row.TownNameDesc,
                StandNo = row.StandNo,
                PremiseId = row.PremiseId,
                SgId = row.SgId,
                PropertyId = row.PropertyId,
                Erf = row.Erf,
                Ptn = row.Ptn,
                Re = row.Re,

                // ── Category ────────────────────────────────────────────────
                CatCode = row.CatCode,
                CatDesc = row.CatDesc,
                PropertyCategory = row.PropertyCategory,
                StatusSubtype = row.StatusSubtype,
                StatusCode = row.StatusCode,

                // ── Valuation figures ────────────────────────────────────────
                MarketValue = row.MarketValue,
                RateableArea = row.RateableArea,
                LegalArea = CleanDecimal(row.LegalArea),
                Extent = row.Extent,
                ValuationDate = row.ValuationDate,
                WefDate = row.WefDate,
                ValuationType = row.ValuationType,
                ValuationRollKey = row.ValuationRollKey,

                // ── Split ────────────────────────────────────────────────────
                ValuationSplitNo = row.ValuationSplitNo,
                ValuationSplitInd = row.ValuationSplitInd,
                IsMultipurpose = isMulti,

                // ── Unit / rights ────────────────────────────────────────────
                UnitKey = row.UnitKey,
                UnitType = row.UnitType,
                UnitNo = row.UnitNo,
                UnitRightsKey = row.UnitRightsKey,
                RightsPremise = row.RightsPremise,
                RightsPremiseSize = row.RightsPremiseSize,
                RightsStatus = row.RightsStatus,
                RightDescription = row.RightDescription,

                // ── Address ──────────────────────────────────────────────────
                OwnerName = row.OwnerName,
                Addr1 = row.Addr1,
                Addr2 = row.Addr2,
                Addr3 = row.Addr3,
                Addr4 = row.Addr4,
                Addr5 = row.Addr5,
                PhysicalAddress = row.PhysicalAddress,
                PremiseAddress = row.PremiseAddress,
                EmailAddr = row.EmailAddr,
                EmailInd = row.EmailInd,

                // ── Flags ────────────────────────────────────────────────────
                SendNotice = row.SendNotice,
                CojOwned = row.CojOwned,
                Usable = row.Usable,
                SapData = row.SapData,
                RemarkDesc = row.RemarkDesc,
                Remarks = row.Remarks,

                // ── Dates ────────────────────────────────────────────────────
                ExtractionDate = row.ExtractionDate,
                ReviewOpenDate = row.ReviewOpenDate,
                ReviewCloseDate = row.ReviewCloseDate,
                SvCapturePeriod = row.ExtractPeriod,

                // ── Workflow ─────────────────────────────────────────────────
                // ── Valuer & Sector from ExtractRows ─────────────────────
                Valuer = row.Valuer,
                Sector = row.Sector,

                Decision = row.Decision,
                DecisionComment = row.DecisionComment,
                DecisionAtUtc = row.DecisionAtUtc,
                AssignedTo = row.Assignment?.AssignedToValuer,

                // ── Split siblings ───────────────────────────────────────────
                SplitRows = siblings.Select(s => new ValuerRowDetailsVm.SplitRowVm
                {
                    ExtractRowId = s.Id,
                    ValuationSplitNo = s.ValuationSplitNo,
                    ValuationSplitInd = s.ValuationSplitInd,
                    CatCode = s.CatCode,
                    CatDesc = s.CatDesc,
                    MarketValue = s.MarketValue,
                    LegalArea = s.LegalArea,
                    Extent = s.Extent,
                    ValuationDate = s.ValuationDate
                }).ToList()
            };
        }
        private static string? CleanDecimal(string? s)
        {
            if (string.IsNullOrWhiteSpace(s)) return s;
            if (decimal.TryParse(s.Trim(),
                    System.Globalization.NumberStyles.Any,
                    System.Globalization.CultureInfo.InvariantCulture,
                    out var d))
                return $"{d:N0}";
            var t = s.Trim();
            if (t.Contains('.')) t = t.TrimEnd('0').TrimEnd('.');
            return t;
        }
    }
}