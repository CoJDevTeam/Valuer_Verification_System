using Microsoft.EntityFrameworkCore;
using Valuer_Verification_System.Data;
using Valuer_Verification_System.Models.Data;

namespace Valuer_Verification_System.Services.Manager
{
    public interface IManagerReassignmentService
    {
        Task<List<string>> GetValuerListAsync(int? batchId, CancellationToken ct);

        Task<List<ReassignRowDto>> ListPendingAsync(int batchId, string? currentValuer, string? sector, int top, CancellationToken ct);

        Task ReassignAsync(int extractRowId, string fromValuer, string toValuer, string reason, string managerUser, CancellationToken ct);
        Task<BulkReassignResult> BulkReassignAsync(int batchId, string fromValuer, string toValuer, string reason, string managerUser, CancellationToken ct);

    }
    public sealed class BulkReassignResult
    {
        public int TotalFound { get; set; }
        public int Moved { get; set; }
        public int SkippedNotPending { get; set; }
    }
    public sealed class ReassignRowDto
    {
        public int ExtractRowId { get; set; }
        public string ValuationKey { get; set; } = "";
        public string? Sector { get; set; }
        public string? PropertyDescription { get; set; }
        public string CurrentValuer { get; set; } = "";
        public DateTime? DecisionAtUtc { get; set; } // should be null for Pending
    }

    public sealed class ManagerReassignmentService : IManagerReassignmentService
    {
        private readonly VvsDbContext _db;

        public ManagerReassignmentService(VvsDbContext db)
        {
            _db = db;
        }

        public async Task<List<string>> GetValuerListAsync(int? batchId, CancellationToken ct)
        {
            var q = _db.Assignments.AsNoTracking().Select(a => a.AssignedToValuer);

            if (batchId.HasValue && batchId.Value > 0)
                q = _db.Assignments.AsNoTracking()
                    .Where(a => a.ExtractRow.BatchId == batchId.Value)
                    .Select(a => a.AssignedToValuer);

            return await q
                .Where(v => v != null && v != "")
                .Distinct()
                .OrderBy(v => v)
                .ToListAsync(ct);
        }

        public async Task<List<ReassignRowDto>> ListPendingAsync(int batchId, string? currentValuer, string? sector, int top, CancellationToken ct)
        {
            if (top <= 0) top = 500;
            if (top > 2000) top = 2000;

            var q = _db.Assignments
                .AsNoTracking()
                .Where(a => a.ExtractRow.BatchId == batchId)
                .Where(a => a.ExtractRow.Decision == VerificationDecision.Pending)
                .Select(a => new
                {
                    a.ExtractRowId,
                    a.AssignedToValuer,
                    a.ExtractRow.ValuationKey,
                    a.ExtractRow.Sector,
                    a.ExtractRow.PropertyDescription,
                    a.ExtractRow.DecisionAtUtc
                })
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(currentValuer))
                q = q.Where(x => x.AssignedToValuer == currentValuer);

            if (!string.IsNullOrWhiteSpace(sector))
                q = q.Where(x => x.Sector == sector);

            var list = await q
                .OrderBy(x => x.AssignedToValuer)
                .ThenBy(x => x.ValuationKey)
                .Take(top)
                .ToListAsync(ct);

            return list.Select(x => new ReassignRowDto
            {
                ExtractRowId = x.ExtractRowId,
                CurrentValuer = x.AssignedToValuer,
                ValuationKey = x.ValuationKey,
                Sector = x.Sector,
                PropertyDescription = x.PropertyDescription,
                DecisionAtUtc = x.DecisionAtUtc
            }).ToList();
        }

        public async Task ReassignAsync(int extractRowId, string fromValuer, string toValuer, string reason, string managerUser, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(toValuer))
                throw new InvalidOperationException("Please select a new valuer.");

            if (string.Equals(fromValuer?.Trim(), toValuer.Trim(), StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("New valuer must be different from current valuer.");

            if (string.IsNullOrWhiteSpace(reason))
                throw new InvalidOperationException("Reason is required.");

            // Load assignment + row
            var assignment = await _db.Assignments
                .Include(a => a.ExtractRow)
                .FirstOrDefaultAsync(a => a.ExtractRowId == extractRowId, ct);

            if (assignment is null)
                throw new InvalidOperationException("Assignment not found.");

            // Only pending records can be reassigned
            if (assignment.ExtractRow.Decision != VerificationDecision.Pending)
                throw new InvalidOperationException("This record is already verified. Reassignment is only allowed for Pending records.");

            var current = assignment.AssignedToValuer;

            if (!string.Equals(current, fromValuer, StringComparison.OrdinalIgnoreCase))
            {
                // optional: stops stale UI submissions
                throw new InvalidOperationException("The current valuer has changed. Refresh the page and try again.");
            }

            // Update assignment
            assignment.AssignedToValuer = toValuer.Trim();
            assignment.AssignedAtUtc = DateTime.UtcNow;
            assignment.AssignedBy = managerUser;
            assignment.UpdatedAt = DateTime.UtcNow;
            assignment.UpdatedBy = managerUser;

            // Audit history
            _db.AssignmentHistories.Add(new AssignmentHistory
            {
                AssignmentId = assignment.Id,
                FromValuer = current,
                ToValuer = toValuer.Trim(),
                ChangedBy = managerUser,
                ChangedAtUtc = DateTime.UtcNow,
                Reason = reason.Trim(),
                CreatedBy = managerUser,
                CreatedAt = DateTime.UtcNow
            });

            await _db.SaveChangesAsync(ct);
        }
        public async Task<BulkReassignResult> BulkReassignAsync(
    int batchId,
    string fromValuer,
    string toValuer,
    string reason,
    string managerUser,
    CancellationToken ct)
        {
            if (batchId <= 0) throw new InvalidOperationException("Batch is required.");
            if (string.IsNullOrWhiteSpace(fromValuer)) throw new InvalidOperationException("From valuer is required.");
            if (string.IsNullOrWhiteSpace(toValuer)) throw new InvalidOperationException("To valuer is required.");
            if (string.Equals(fromValuer.Trim(), toValuer.Trim(), StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("New valuer must be different from current valuer.");
            if (string.IsNullOrWhiteSpace(reason)) throw new InvalidOperationException("Reason is required.");

            // Load all assignments in that batch for that valuer
            var assignments = await _db.Assignments
                .Include(a => a.ExtractRow)
                .Where(a => a.ExtractRow.BatchId == batchId && a.AssignedToValuer == fromValuer)
                .ToListAsync(ct);

            if (assignments.Count == 0)
            {
                return new BulkReassignResult
                {
                    TotalFound = 0,
                    Moved = 0,
                    SkippedNotPending = 0
                };
            }

            var result = new BulkReassignResult
            {
                TotalFound = assignments.Count
            };

            var now = DateTime.UtcNow;

            foreach (var a in assignments)
            {
                // Only move Pending
                if (a.ExtractRow.Decision != VerificationDecision.Pending)
                {
                    result.SkippedNotPending++;
                    continue;
                }

                var oldValuer = a.AssignedToValuer;

                a.AssignedToValuer = toValuer.Trim();
                a.AssignedAtUtc = now;
                a.AssignedBy = managerUser;
                a.UpdatedAt = now;
                a.UpdatedBy = managerUser;

                _db.AssignmentHistories.Add(new AssignmentHistory
                {
                    AssignmentId = a.Id,
                    FromValuer = oldValuer,
                    ToValuer = toValuer.Trim(),
                    ChangedBy = managerUser,
                    ChangedAtUtc = now,
                    Reason = reason.Trim(),
                    CreatedBy = managerUser,
                    CreatedAt = now
                });

                result.Moved++;
            }

            await _db.SaveChangesAsync(ct);
            return result;
        }

    }
}
