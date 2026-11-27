using Microsoft.EntityFrameworkCore;
using Valuer_Verification_System.Data;
using Valuer_Verification_System.Models.Data;
using Valuer_Verification_System.Models.ViewModels;
using Valuer_Verification_System.Services.Notifications;

namespace Valuer_Verification_System.Services.Valuer
{
    public interface IValuerVerificationService
    {
        Task<(List<Batch> batches, List<ExtractRow> rows, int unreadNotifications, List<Notification> notifications)> GetInboxAsync(
            string valuer,
            int? batchId,
            string filter,
            CancellationToken ct);

        Task VerifyAsync(
            int extractRowId,
            string valuer,
            VerificationDecision decision,
            string? comment,
            CancellationToken ct);

        Task MarkNotificationReadAsync(long notificationId, string valuer, CancellationToken ct);
        Task MarkAllNotificationsReadAsync(string valuer, CancellationToken ct);

        Task<ValuerRowDetailsVm?> GetRowDetailsAsync(int extractRowId, string valuer, CancellationToken ct);
    }

    public sealed class ValuerVerificationService : IValuerVerificationService
    {
        private readonly VvsDbContext _db;
        private readonly INotificationService _notif;
        private readonly IWeekRowStore _weekRows;

        public ValuerVerificationService(VvsDbContext db, INotificationService notif, IWeekRowStore weekRows)
        {
            _db = db;
            _notif = notif;
            _weekRows = weekRows;
        }

        // ── Inbox ─────────────────────────────────────────────────────────────
        public async Task<(List<Batch> batches, List<ExtractRow> rows, int unreadNotifications, List<Notification> notifications)> GetInboxAsync(
      string valuer,
      int? batchId,
      string filter,
      CancellationToken ct)
        {
            valuer = (valuer ?? "").Trim();

            if (string.IsNullOrWhiteSpace(valuer))
                throw new InvalidOperationException("Valuer is required.");

            // The week table is the working table. Only batches the Project Manager approved are visible.
            var approved = await _weekRows.ApprovedBatchesAsync(ct);

            var myAll = await _weekRows.LoadForValuerAsync(valuer, null, null, ct);
            var myBatchIds = myAll.Select(r => r.BatchId).Distinct().ToHashSet();
            var batches = approved.Where(x => myBatchIds.Contains(x.Id)).ToList();

            filter = (filter ?? "Pending").Trim();
            string? decision = filter switch
            {
                "Yes" => "Yes",
                "No" => "No",
                "All" => null,
                _ => "Pending"
            };

            var rows = (await _weekRows.LoadForValuerAsync(
                    valuer, batchId.HasValue && batchId.Value > 0 ? batchId : null, decision, ct))
                .OrderBy(r => r.Sector)
                .ThenBy(r => r.ValuationKey)
                .ToList();

            var unread = await _notif.GetUnreadCountForValuerAsync(valuer, ct);
            var latest = await _notif.GetLatestForValuerAsync(valuer, top: 8, ct);

            return (batches, rows, unread, latest);
        }

        // ── Verify ────────────────────────────────────────────────────────────
        public async Task VerifyAsync(
            int extractRowId,
            string valuer,
            VerificationDecision decision,
            string? comment,
            CancellationToken ct)
        {
            if (decision is not (VerificationDecision.Yes or VerificationDecision.No))
                throw new InvalidOperationException("Invalid verification decision.");

            valuer = (valuer ?? "").Trim();
            if (string.IsNullOrWhiteSpace(valuer))
                throw new InvalidOperationException("Valuer is required.");

            if (decision == VerificationDecision.No && string.IsNullOrWhiteSpace(comment))
                throw new InvalidOperationException("Comment is required when selecting No.");

            var changed = await _weekRows.SetDecisionAsync(
                new[] { extractRowId }, null, valuer,
                decision == VerificationDecision.Yes ? "Yes" : "No",
                decision == VerificationDecision.No ? comment?.Trim() : null,
                ct);

            if (changed == 0)
                throw new InvalidOperationException(
                    "You are not allowed to verify this record, or its extract has not been approved yet.");
        }

        public Task MarkNotificationReadAsync(long notificationId, string valuer, CancellationToken ct)
            => _notif.MarkReadAsync(notificationId, (valuer ?? "").Trim(), ct);

        public Task MarkAllNotificationsReadAsync(string valuer, CancellationToken ct)
            => _notif.MarkAllReadAsync((valuer ?? "").Trim(), ct);

        // ── Row Details — full field mapping ─────────────────────────────────
        public async Task<ValuerRowDetailsVm?> GetRowDetailsAsync(
            int extractRowId, string valuer, CancellationToken ct)
        {
            valuer = (valuer ?? "").Trim();

            // Security: only rows of this valuer, in approved batches (read from the week table)
            var row = await _weekRows.GetRowAsync(extractRowId, valuer, ct);
            if (row is null) return null;

            var batch = await _db.Batches
                .AsNoTracking()
                .Include(x => x.WeekWindow)
                .FirstOrDefaultAsync(x => x.Id == row.BatchId, ct);

            // All rows sharing the same ValuationKey in the same batch (for split/multipurpose)
            var siblings = await _weekRows.GetSiblingsAsync(row.BatchId, row.ValuationKey, ct);

            bool isMulti = siblings.Count > 1 ||
                siblings.Any(r =>
                    !string.IsNullOrWhiteSpace(r.ValuationSplitInd) &&
                    r.ValuationSplitInd.Trim().ToUpper() is "Y" or "P");

            return new ValuerRowDetailsVm
            {
                // ── Identity ─────────────────────────────────────────────────
                ExtractRowId = row.Id,
                ValuationKey = row.ValuationKey,
                BatchId = row.BatchId,
                BatchName = batch?.BatchName ?? "",

                // ── Property identification ───────────────────────────────────
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

                // ── Category ─────────────────────────────────────────────────
                CatCode = row.CatCode,
                CatDesc = row.CatDesc,
                PropertyCategory = row.PropertyCategory,
                StatusSubtype = row.StatusSubtype,
                StatusCode = row.StatusCode,

                // ── Valuation figures ─────────────────────────────────────────
                MarketValue = row.MarketValue,
                RateableArea = row.RateableArea,
                LegalArea = CleanDecimalString(row.LegalArea),
                Extent = row.Extent,
                ValuationDate = row.ValuationDate,
                WefDate = row.WefDate,
                ValuationType = row.ValuationType,
                ValuationRollKey = row.ValuationRollKey,

                // ── Split ─────────────────────────────────────────────────────
                ValuationSplitNo = row.ValuationSplitNo,
                ValuationSplitInd = row.ValuationSplitInd,
                IsMultipurpose = isMulti,

                // ── Unit / rights ─────────────────────────────────────────────
                UnitKey = row.UnitKey,
                UnitType = row.UnitType,
                UnitNo = row.UnitNo,
                UnitRightsKey = row.UnitRightsKey,
                RightsPremise = row.RightsPremise,
                RightsPremiseSize = row.RightsPremiseSize,
                RightsStatus = row.RightsStatus,
                RightDescription = row.RightDescription,

                // ── Address ───────────────────────────────────────────────────
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

                // ── Flags ─────────────────────────────────────────────────────
                SendNotice = row.SendNotice,
                CojOwned = row.CojOwned,
                Usable = row.Usable,
                SapData = row.SapData,
                RemarkDesc = row.RemarkDesc,
                Remarks = row.Remarks,

                // ── Dates ─────────────────────────────────────────────────────
                ExtractionDate = row.ExtractionDate,
                ReviewOpenDate = row.ReviewOpenDate,
                ReviewCloseDate = row.ReviewCloseDate,
                SvCapturePeriod = row.ExtractPeriod,

                // ── Valuer & Sector ───────────────────────────────────────────
                Valuer = row.Valuer,
                Sector = row.Sector,

                // ── Workflow ──────────────────────────────────────────────────
                Decision = row.Decision,
                DecisionComment = row.DecisionComment,
                DecisionAtUtc = row.DecisionAtUtc,
                AssignedTo = valuer,

                // ── Split siblings ────────────────────────────────────────────
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
        // ── Strip trailing decimal zeros from string decimal fields ─────────
        private static string? CleanDecimalString(string? s)
        {
            if (string.IsNullOrWhiteSpace(s)) return s;
            if (decimal.TryParse(s.Trim(),
                    System.Globalization.NumberStyles.Any,
                    System.Globalization.CultureInfo.InvariantCulture,
                    out var d))
                return $"{d:N0}";                 // e.g. "1107.00000000" → "1 107"
            // fallback: trim trailing zeros manually
            var t = s.Trim();
            if (t.Contains('.'))
                t = t.TrimEnd('0').TrimEnd('.');
            return t;
        }
    }
}