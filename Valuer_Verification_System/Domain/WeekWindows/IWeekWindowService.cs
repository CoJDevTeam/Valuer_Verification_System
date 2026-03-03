using Valuer_Verification_System.Models.Data;

namespace Valuer_Verification_System.Domain.WeekWindows
{
    public interface IWeekWindowService
    {
        /// <summary>
        /// Creates a new WeekWindow for the Mon-Sun week that contains
        /// anyDateInWeek, or returns the existing one.
        /// All derived dates (NoticeSendDate, ReviewOpenDate, ReviewCloseDate)
        /// are calculated and persisted automatically.
        /// </summary>
        Task<WeekWindow> CreateOrGetAsync(DateOnly anyDateInWeek, string createdBy, CancellationToken ct);

        /// <summary>
        /// Returns the WeekWindow with the given primary key, or null if not found.
        /// Includes all stored date fields (NoticeSendDate, ReviewOpenDate, ReviewCloseDate).
        /// </summary>
        Task<WeekWindow?> GetAsync(int id, CancellationToken ct);

        /// <summary>
        /// Returns the WeekWindow that owns the given batch, or null if not found.
        /// </summary>
        Task<WeekWindow?> GetByBatchAsync(int batchId, CancellationToken ct);

        /// <summary>
        /// Returns a paged list of WeekWindows ordered by WeekStart descending.
        /// </summary>
        Task<List<WeekWindow>> ListAsync(int take = 200, CancellationToken ct = default);

        /// <summary>
        /// Advances the WeekWindow status and persists the change.
        /// Throws InvalidOperationException if the requested transition is not valid.
        /// </summary>
        Task UpdateStatusAsync(int id, WeekWindowStatus newStatus, string updatedBy, CancellationToken ct);

        /// <summary>
        /// Returns true if a WeekWindow already exists for the given Monday week-start date.
        /// </summary>
        Task<bool> ExistsForWeekStartAsync(DateOnly weekStart, CancellationToken ct);
    }
}
