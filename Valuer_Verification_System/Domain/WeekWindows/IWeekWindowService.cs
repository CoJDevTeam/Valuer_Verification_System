using Valuer_Verification_System.Models.Data;

namespace Valuer_Verification_System.Domain.WeekWindows
{
    public interface IWeekWindowService
    {
        Task<WeekWindow> CreateOrGetAsync(DateOnly anyDateInWeek, string createdBy, CancellationToken ct);
        Task<WeekWindow?> GetAsync(int id, CancellationToken ct);
        Task<List<WeekWindow>> ListAsync(int take = 200, CancellationToken ct = default);
    }
}
