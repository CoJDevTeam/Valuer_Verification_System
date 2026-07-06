using Microsoft.EntityFrameworkCore;
using Valuer_Verification_System.Data;
using Valuer_Verification_System.Models.Data;
using Valuer_Verification_System.Models.ViewModels;

namespace Valuer_Verification_System.Services.Verifications
{
    public sealed class ValuerBulkVerificationService : IValuerBulkVerificationService
    {
        private readonly VvsDbContext _db;

        public ValuerBulkVerificationService(VvsDbContext db)
        {
            _db = db;
        }

        public async Task<ValuerBulkInboxVm> GetInboxAsync(
            int? batchId,
            string username,
            CancellationToken ct)
        {
            username = Normalize(username);

            var selectedBatchId = batchId ?? await _db.Batches
                .AsNoTracking()
                .OrderByDescending(x => x.Id)
                .Select(x => x.Id)
                .FirstOrDefaultAsync(ct);

            if (selectedBatchId <= 0)
            {
                return new ValuerBulkInboxVm
                {
                    BatchId = 0,
                    ValuerUsername = username,
                    Groups = new List<ValuerBulkGroupVm>()
                };
            }

            var rows = await _db.ExtractRows
                .AsNoTracking()
                .Where(x =>
                    x.BatchId == selectedBatchId &&
                    x.Username != null &&
                    x.Username.Trim() == username &&
                    x.UnitKey != null &&
                    x.UnitKey.Trim() != "")
                .Select(x => new
                {
                    x.Id,
                    x.BatchId,
                    x.ValuationKey,
                    x.ValuationKey2,
                    x.PremiseId,
                    x.UnitKey,
                    x.SgId,
                    x.PropertyId,
                    x.StandNo,
                    x.TownNameDesc,
                    x.UnitType,
                    x.UnitNo,
                    x.LegalArea,
                    x.PropertyDescription,
                    x.PropertyDesc,
                    x.CatDesc,
                    x.PropertyCategory,
                    x.MarketValue,
                    x.Decision
                })
                .ToListAsync(ct);

            var groups = rows
                .GroupBy(x => new
                {
                    x.BatchId,
                    UnitKey = Normalize(x.UnitKey)
                })
                .Where(g => g.Count() > 1)
                .Select(g =>
                {
                    var distinctUnitKeys = g
                        .Select(x => Normalize(x.UnitKey))
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .Count();

                    var distinctSgIds = g
                        .Select(x => Normalize(x.SgId))
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .Count();

                    var distinctPropertyIds = g
                        .Select(x => Normalize(x.PropertyId))
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .Count();

                    var distinctTowns = g
                        .Select(x => Normalize(x.TownNameDesc))
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .Count();

                    var distinctUnitNos = g
                        .Select(x => Normalize(x.UnitNo))
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .Count();

                    var distinctLegalAreas = g
                        .Select(x => Normalize(x.LegalArea))
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .Count();

                    var pending = g.Count(x => x.Decision == VerificationDecision.Pending);
                    var yes = g.Count(x => x.Decision == VerificationDecision.Yes);
                    var no = g.Count(x => x.Decision == VerificationDecision.No);

                    /*
                        Bulk safety rule:

                        We DO NOT require same PremiseId here.

                        In your extract, the linked records have same UnitKey but different PremiseId:
                        - ST PremiseId
                        - SL PremiseId

                        That is correct for your use case.
                    */
                    var isSafe =
                        distinctUnitKeys == 1 &&
                        distinctSgIds == 1 &&
                        distinctTowns == 1 &&
                        distinctUnitNos == 1 &&
                        distinctLegalAreas == 1 &&
                        pending > 0 &&
                        no == 0;

                    var propertyDescription =
                        g.Select(x => x.PropertyDescription).FirstOrDefault(x => !string.IsNullOrWhiteSpace(x))
                        ?? g.Select(x => x.PropertyDesc).FirstOrDefault(x => !string.IsNullOrWhiteSpace(x))
                        ?? "";

                    var safetyMessage = isSafe
                        ? "Ready for bulk verification"
                        : BuildUnitKeyBulkSafetyMessage(
                            distinctUnitKeys,
                            distinctSgIds,
                            distinctPropertyIds,
                            distinctTowns,
                            distinctUnitNos,
                            distinctLegalAreas,
                            pending,
                            no);

                    return new ValuerBulkGroupVm
                    {
                        BatchId = g.Key.BatchId,

                        // Display only. Not used as bulk key.
                        PremiseId = g.Select(x => x.PremiseId)
                            .FirstOrDefault(x => !string.IsNullOrWhiteSpace(x)) ?? "",

                        // Real bulk key.
                        UnitKey = g.Key.UnitKey,

                        PropertyDescription = propertyDescription,
                        TownNameDesc = g.Select(x => x.TownNameDesc)
                            .FirstOrDefault(x => !string.IsNullOrWhiteSpace(x)) ?? "",
                        UnitType = g.Select(x => x.UnitType)
                            .FirstOrDefault(x => !string.IsNullOrWhiteSpace(x)) ?? "",
                        UnitNo = g.Select(x => x.UnitNo)
                            .FirstOrDefault(x => !string.IsNullOrWhiteSpace(x)) ?? "",

                        RowCount = g.Count(),
                        PendingCount = pending,
                        YesCount = yes,
                        NoCount = no,

                        IsSafeForBulk = isSafe,
                        SafetyMessage = safetyMessage
                    };
                })
                .OrderByDescending(x => x.IsSafeForBulk)
                .ThenByDescending(x => x.PendingCount)
                .ThenBy(x => x.PropertyDescription)
                .ToList();

            return new ValuerBulkInboxVm
            {
                BatchId = selectedBatchId,
                ValuerUsername = username,
                Groups = groups
            };
        }

        public async Task<ValuerBulkDetailsVm?> GetDetailsAsync(
            int batchId,
            string unitKey,
            string username,
            CancellationToken ct)
        {
            username = Normalize(username);
            unitKey = Normalize(unitKey);

            var rows = await _db.ExtractRows
                .AsNoTracking()
                .Where(x =>
                    x.BatchId == batchId &&
                    x.Username != null &&
                    x.Username.Trim() == username &&
                    x.UnitKey != null &&
                    x.UnitKey.Trim() == unitKey)
                .OrderBy(x => x.UnitNo)
                .ThenBy(x => x.ValuationKey)
                .ToListAsync(ct);

            if (!rows.Any())
                return null;

            var distinctUnitKeys = rows
                .Select(x => Normalize(x.UnitKey))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Count();

            var distinctSgIds = rows
                .Select(x => Normalize(x.SgId))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Count();

            var distinctPropertyIds = rows
                .Select(x => Normalize(x.PropertyId))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Count();

            var distinctTowns = rows
                .Select(x => Normalize(x.TownNameDesc))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Count();

            var distinctUnitNos = rows
                .Select(x => Normalize(x.UnitNo))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Count();

            var distinctLegalAreas = rows
                .Select(x => Normalize(x.LegalArea))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Count();

            var pending = rows.Count(x => x.Decision == VerificationDecision.Pending);
            var no = rows.Count(x => x.Decision == VerificationDecision.No);

            var isSafe =
                distinctUnitKeys == 1 &&
                distinctSgIds == 1 &&
                distinctTowns == 1 &&
                distinctUnitNos == 1 &&
                distinctLegalAreas == 1 &&
                pending > 0 &&
                no == 0;

            return new ValuerBulkDetailsVm
            {
                BatchId = batchId,

                // Display only. Not used to verify.
                PremiseId = rows.Select(x => x.PremiseId)
                    .FirstOrDefault(x => !string.IsNullOrWhiteSpace(x)) ?? "",

                // Real bulk key.
                UnitKey = unitKey,

                PropertyDescription =
                    rows.Select(x => x.PropertyDescription).FirstOrDefault(x => !string.IsNullOrWhiteSpace(x))
                    ?? rows.Select(x => x.PropertyDesc).FirstOrDefault(x => !string.IsNullOrWhiteSpace(x))
                    ?? "",

                TownNameDesc = rows.Select(x => x.TownNameDesc)
                    .FirstOrDefault(x => !string.IsNullOrWhiteSpace(x)) ?? "",

                UnitType = rows.Select(x => x.UnitType)
                    .FirstOrDefault(x => !string.IsNullOrWhiteSpace(x)) ?? "",

                UnitNo = rows.Select(x => x.UnitNo)
                    .FirstOrDefault(x => !string.IsNullOrWhiteSpace(x)) ?? "",

                IsSafeForBulk = isSafe,

                SafetyMessage = isSafe
                    ? "Ready for bulk verification"
                    : BuildUnitKeyBulkSafetyMessage(
                        distinctUnitKeys,
                        distinctSgIds,
                        distinctPropertyIds,
                        distinctTowns,
                        distinctUnitNos,
                        distinctLegalAreas,
                        pending,
                        no),

                Rows = rows.Select(x => new ValuerBulkRowVm
                {
                    ExtractRowId = x.Id,
                    ValuationKey = x.ValuationKey ?? "",
                    ValuationKey2 = x.ValuationKey2 ?? "",
                    PropertyDescription = x.PropertyDescription ?? x.PropertyDesc ?? "",
                    Category = x.PropertyCategory ?? x.CatDesc ?? "",
                    MarketValue = x.MarketValue,
                    RateableArea = x.RateableArea,
                    LegalArea = x.LegalArea ?? "",
                    RightDescription = x.RightDescription ?? "",
                    RightsStatus = x.RightsStatus ?? "",
                    WefDate = x.WefDate,
                    Decision = x.Decision
                }).ToList()
            };
        }

        public async Task<int> VerifyAllCorrectAsync(
            BulkVerifyRequestVm vm,
            string username,
            CancellationToken ct)
        {
            username = Normalize(username);
            var unitKey = Normalize(vm.UnitKey);

            var rows = await _db.ExtractRows
                .Where(x =>
                    x.BatchId == vm.BatchId &&
                    x.Username != null &&
                    x.Username.Trim() == username &&
                    x.UnitKey != null &&
                    x.UnitKey.Trim() == unitKey)
                .ToListAsync(ct);

            if (!rows.Any())
                return 0;

            var distinctUnitKeys = rows
                .Select(x => Normalize(x.UnitKey))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Count();

            var distinctSgIds = rows
                .Select(x => Normalize(x.SgId))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Count();

            var distinctTowns = rows
                .Select(x => Normalize(x.TownNameDesc))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Count();

            var distinctUnitNos = rows
                .Select(x => Normalize(x.UnitNo))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Count();

            var distinctLegalAreas = rows
                .Select(x => Normalize(x.LegalArea))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Count();

            var hasNoRows = rows.Any(x => x.Decision == VerificationDecision.No);

            if (distinctUnitKeys != 1 ||
                distinctSgIds != 1 ||
                distinctTowns != 1 ||
                distinctUnitNos != 1 ||
                distinctLegalAreas != 1 ||
                hasNoRows)
            {
                throw new InvalidOperationException("This group is not safe for bulk verification. Please verify manually.");
            }

            var pendingRows = rows
                .Where(x => x.Decision == VerificationDecision.Pending)
                .ToList();

            foreach (var row in pendingRows)
            {
                row.Decision = VerificationDecision.Yes;
                row.DecisionAtUtc = DateTime.UtcNow;
              //  row.DecisionBy = username;
                row.DecisionComment = string.IsNullOrWhiteSpace(vm.Comment)
                    ? "Bulk verified as correct."
                    : vm.Comment.Trim();

                row.UpdatedBy = username;
                row.UpdatedAt = DateTime.UtcNow;
            }

            await _db.SaveChangesAsync(ct);

            return pendingRows.Count;
        }

        private static string Normalize(string? value)
        {
            return string.IsNullOrWhiteSpace(value)
                ? ""
                : value.Trim();
        }

        private static string BuildUnitKeyBulkSafetyMessage(
            int distinctUnitKeys,
            int distinctSgIds,
            int distinctPropertyIds,
            int distinctTowns,
            int distinctUnitNos,
            int distinctLegalAreas,
            int pending,
            int no)
        {
            var issues = new List<string>();

            if (distinctUnitKeys != 1)
                issues.Add("different UnitKeys");

            if (distinctSgIds != 1)
                issues.Add("different SG IDs");

            if (distinctPropertyIds > 1)
                issues.Add("different Property IDs");

            if (distinctTowns != 1)
                issues.Add("different towns");

            if (distinctUnitNos != 1)
                issues.Add("different unit numbers");

            if (distinctLegalAreas != 1)
                issues.Add("different legal areas");

            if (pending <= 0)
                issues.Add("no pending rows");

            if (no > 0)
                issues.Add("some rows already marked No");

            return issues.Count == 0
                ? "Ready for bulk verification"
                : "Needs manual review: " + string.Join(", ", issues);
        }
    }
}