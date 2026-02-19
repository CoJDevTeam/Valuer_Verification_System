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

        public ValuerVerificationService(VvsDbContext db, INotificationService notif)
        {
            _db = db;
            _notif = notif;
        }

        public async Task<(List<Batch> batches, List<ExtractRow> rows, int unreadNotifications, List<Notification> notifications)> GetInboxAsync(
            string valuer,
            int? batchId,
            string filter,
            CancellationToken ct)
        {
            valuer = (valuer ?? "").Trim();
            if (string.IsNullOrWhiteSpace(valuer))
                throw new InvalidOperationException("Valuer is required.");

            // Batches where this valuer has assignments (so dropdown only shows relevant batches)
            var batches = await _db.Batches
                .Include(b => b.WeekWindow)
                .Where(b => _db.Assignments.Any(a =>
                    a.AssignedToValuer == valuer &&
                    a.ExtractRow.BatchId == b.Id))
                .OrderByDescending(b => b.Id)
                .Take(200)
                .ToListAsync(ct);

            var q = _db.Assignments
                .AsNoTracking()
                .Where(a => a.AssignedToValuer == valuer)
                .Select(a => a.ExtractRow)
                .AsQueryable();

            if (batchId.HasValue && batchId.Value > 0)
                q = q.Where(r => r.BatchId == batchId.Value);

            filter = (filter ?? "Pending").Trim();

            q = filter switch
            {
                "Yes" => q.Where(r => r.Decision == VerificationDecision.Yes),
                "No" => q.Where(r => r.Decision == VerificationDecision.No),
                "All" => q,
                _ => q.Where(r => r.Decision == VerificationDecision.Pending)
            };

            var rows = await q
                .OrderBy(r => r.Sector)
                .ThenBy(r => r.ValuationKey)
                .Take(5000)
                .ToListAsync(ct);

            // ✅ Notifications
            var unread = await _notif.GetUnreadCountForValuerAsync(valuer, ct);
            var latest = await _notif.GetLatestForValuerAsync(valuer, top: 8, ct);

            return (batches, rows, unread, latest);
        }

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

            // Ensure this row belongs to this valuer (security)
            var assigned = await _db.Assignments
                .AsNoTracking()
                .AnyAsync(a => a.ExtractRowId == extractRowId && a.AssignedToValuer == valuer, ct);

            if (!assigned)
                throw new InvalidOperationException("You are not allowed to verify this record.");

            var row = await _db.ExtractRows.FirstOrDefaultAsync(r => r.Id == extractRowId, ct);
            if (row is null)
                throw new InvalidOperationException("Record not found.");

            if (decision == VerificationDecision.No && string.IsNullOrWhiteSpace(comment))
                throw new InvalidOperationException("Comment is required when selecting No.");

            row.Decision = decision;
            row.DecisionAtUtc = DateTime.UtcNow;

            // Save comment only when No; clear it if Yes
            row.DecisionComment = decision == VerificationDecision.No ? comment?.Trim() : null;

            row.UpdatedAt = DateTime.UtcNow;
            row.UpdatedBy = valuer;

            await _db.SaveChangesAsync(ct);
        }

        public Task MarkNotificationReadAsync(long notificationId, string valuer, CancellationToken ct)
            => _notif.MarkReadAsync(notificationId, (valuer ?? "").Trim(), ct);

        public Task MarkAllNotificationsReadAsync(string valuer, CancellationToken ct)
            => _notif.MarkAllReadAsync((valuer ?? "").Trim(), ct);

        public async Task<ValuerRowDetailsVm?> GetRowDetailsAsync(int extractRowId, string valuer, CancellationToken ct)
        {
            // Security: only rows assigned to this valuer
            var row = await _db.Assignments
                .AsNoTracking()
                .Where(a => a.ExtractRowId == extractRowId && a.AssignedToValuer == valuer)
                .Select(a => new
                {
                    a.AssignedToValuer,
                    Row = a.ExtractRow,
                    Batch = a.ExtractRow.Batch,
                    Week = a.ExtractRow.Batch.WeekWindow
                })
                .FirstOrDefaultAsync(ct);

            if (row == null) return null;

            return new ValuerRowDetailsVm
            {
                ExtractRowId = row.Row.Id,
                ValuationKey = row.Row.ValuationKey ?? "",
                Sector = row.Row.Sector ?? "",
                PropertyDescription = row.Row.PropertyDescription ?? "",
                MarketValue = row.Row.MarketValue,
                Category = row.Row.Category ?? "",
                PropertyType = row.Row.PropertyType ?? "",

                AssignedToValuer = row.AssignedToValuer ?? "",
                Decision = row.Row.Decision,
                DecisionAtUtc = row.Row.DecisionAtUtc,
                DecisionComment = row.Row.DecisionComment,

                BatchName = row.Batch.BatchName ?? "",
                BatchDate = row.Batch.BatchDate,

                WeekStart = row.Week.WeekStart,
                WeekEnd = row.Week.WeekEnd,
                NoticeSendDate = row.Week.NoticeSendDate
            };
        }
    }
}
