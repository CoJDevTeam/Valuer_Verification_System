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
            var q = _db.ExtractRows
                .AsNoTracking()
                .Where(r => r.Username != null && r.Username != "")
                .Select(r => r.Username!);

            if (batchId.HasValue && batchId.Value > 0)
            {
                q = _db.ExtractRows
                    .AsNoTracking()
                    .Where(r =>
                        r.BatchId == batchId.Value &&
                        r.Username != null &&
                        r.Username != "")
                    .Select(r => r.Username!);
            }

            return await q
                .Distinct()
                .OrderBy(v => v)
                .ToListAsync(ct);
        }

        public async Task<List<ReassignRowDto>> ListPendingAsync(
    int batchId,
    string? currentValuer,
    string? sector,
    int top,
    CancellationToken ct)
        {
            if (top <= 0) top = 500;
            if (top > 2000) top = 2000;

            var q = _db.ExtractRows
                .AsNoTracking()
                .Where(r => r.BatchId == batchId)
                .Where(r => r.Decision == VerificationDecision.Pending)
                .Select(r => new
                {
                    ExtractRowId = r.Id,
                    CurrentValuer = string.IsNullOrWhiteSpace(r.Username) ? "Unassigned" : r.Username!.Trim(),
                    r.ValuationKey,
                    r.Sector,
                    r.PropertyDescription,
                    r.DecisionAtUtc
                })
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(currentValuer))
                q = q.Where(x => x.CurrentValuer == currentValuer.Trim());

            if (!string.IsNullOrWhiteSpace(sector))
                q = q.Where(x => x.Sector == sector);

            var list = await q
                .OrderBy(x => x.CurrentValuer)
                .ThenBy(x => x.ValuationKey)
                .Take(top)
                .ToListAsync(ct);

            return list.Select(x => new ReassignRowDto
            {
                ExtractRowId = x.ExtractRowId,
                CurrentValuer = x.CurrentValuer,
                ValuationKey = x.ValuationKey,
                Sector = x.Sector,
                PropertyDescription = x.PropertyDescription,
                DecisionAtUtc = x.DecisionAtUtc
            }).ToList();
        }

        public async Task ReassignAsync(
    int extractRowId,
    string fromValuer,
    string toValuer,
    string reason,
    string managerUser,
    CancellationToken ct)
        {
            fromValuer = (fromValuer ?? "").Trim();
            toValuer = (toValuer ?? "").Trim();
            reason = (reason ?? "").Trim();
            managerUser = (managerUser ?? "manager").Trim();

            if (string.IsNullOrWhiteSpace(toValuer))
                throw new InvalidOperationException("Please select a new valuer.");

            if (string.Equals(fromValuer, toValuer, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("New valuer must be different from current valuer.");

            if (string.IsNullOrWhiteSpace(reason))
                throw new InvalidOperationException("Reason is required.");

            var row = await _db.ExtractRows
                .Include(r => r.Assignment)
                .FirstOrDefaultAsync(r => r.Id == extractRowId, ct);

            if (row is null)
                throw new InvalidOperationException("Record not found.");

            if (row.Decision != VerificationDecision.Pending)
                throw new InvalidOperationException("This record is already verified. Reassignment is only allowed for Pending records.");

            var current = string.IsNullOrWhiteSpace(row.Username)
                ? "Unassigned"
                : row.Username.Trim();

            if (!string.Equals(current, fromValuer, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("The current valuer has changed. Refresh the page and try again.");

            row.Username = toValuer;
            row.UpdatedAt = DateTime.UtcNow;
            row.UpdatedBy = managerUser;

            Assignment assignment;

            if (row.Assignment is null)
            {
                assignment = new Assignment
                {
                    ExtractRowId = row.Id,
                    AssignedToValuer = toValuer,
                    AssignedAtUtc = DateTime.UtcNow,
                    AssignedBy = managerUser,
                    CreatedBy = managerUser,
                    CreatedAt = DateTime.UtcNow
                };

                _db.Assignments.Add(assignment);
                await _db.SaveChangesAsync(ct);
            }
            else
            {
                assignment = row.Assignment;

                assignment.AssignedToValuer = toValuer;
                assignment.AssignedAtUtc = DateTime.UtcNow;
                assignment.AssignedBy = managerUser;
                assignment.UpdatedAt = DateTime.UtcNow;
                assignment.UpdatedBy = managerUser;

                await _db.SaveChangesAsync(ct);
            }

            _db.AssignmentHistories.Add(new AssignmentHistory
            {
                AssignmentId = assignment.Id,
                FromValuer = current,
                ToValuer = toValuer,
                ChangedBy = managerUser,
                ChangedAtUtc = DateTime.UtcNow,
                Reason = reason,
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
            fromValuer = (fromValuer ?? "").Trim();
            toValuer = (toValuer ?? "").Trim();
            reason = (reason ?? "").Trim();
            managerUser = (managerUser ?? "manager").Trim();

            if (batchId <= 0)
                throw new InvalidOperationException("Batch is required.");

            if (string.IsNullOrWhiteSpace(fromValuer))
                throw new InvalidOperationException("From valuer is required.");

            if (string.IsNullOrWhiteSpace(toValuer))
                throw new InvalidOperationException("To valuer is required.");

            if (string.Equals(fromValuer, toValuer, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("New valuer must be different from current valuer.");

            if (string.IsNullOrWhiteSpace(reason))
                throw new InvalidOperationException("Reason is required.");

            var rows = await _db.ExtractRows
                .Include(r => r.Assignment)
                .Where(r =>
                    r.BatchId == batchId &&
                    r.Username != null &&
                    r.Username.Trim() == fromValuer)
                .ToListAsync(ct);

            if (rows.Count == 0)
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
                TotalFound = rows.Count
            };

            var now = DateTime.UtcNow;

            foreach (var row in rows)
            {
                if (row.Decision != VerificationDecision.Pending)
                {
                    result.SkippedNotPending++;
                    continue;
                }

                var oldValuer = string.IsNullOrWhiteSpace(row.Username)
                    ? "Unassigned"
                    : row.Username.Trim();

                row.Username = toValuer;
                row.UpdatedAt = now;
                row.UpdatedBy = managerUser;

                Assignment assignment;

                if (row.Assignment is null)
                {
                    assignment = new Assignment
                    {
                        ExtractRowId = row.Id,
                        AssignedToValuer = toValuer,
                        AssignedAtUtc = now,
                        AssignedBy = managerUser,
                        CreatedBy = managerUser,
                        CreatedAt = now
                    };

                    _db.Assignments.Add(assignment);
                    await _db.SaveChangesAsync(ct);
                }
                else
                {
                    assignment = row.Assignment;

                    assignment.AssignedToValuer = toValuer;
                    assignment.AssignedAtUtc = now;
                    assignment.AssignedBy = managerUser;
                    assignment.UpdatedAt = now;
                    assignment.UpdatedBy = managerUser;
                }

                _db.AssignmentHistories.Add(new AssignmentHistory
                {
                    AssignmentId = assignment.Id,
                    FromValuer = oldValuer,
                    ToValuer = toValuer,
                    ChangedBy = managerUser,
                    ChangedAtUtc = now,
                    Reason = reason,
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
