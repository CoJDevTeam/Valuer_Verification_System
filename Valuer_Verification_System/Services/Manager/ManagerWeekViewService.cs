using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Valuer_Verification_System.Data;

namespace Valuer_Verification_System.Services.Manager
{
    public sealed class ManagerWeekView
    {
        public int Total { get; set; }
        public int Properties { get; set; }
        public int Pending { get; set; }
        public int Yes { get; set; }
        public int No { get; set; }
        public List<ManagerWeekValuer> Valuers { get; set; } = new();
        public List<ManagerWeekSector> Sectors { get; set; } = new();
    }

    public sealed class ManagerWeekValuer
    {
        public string Sector { get; set; } = "";
        public string Username { get; set; } = "";
        public string Valuer { get; set; } = "";
        public int Total { get; set; }
        public int Pending { get; set; }
        public int Yes { get; set; }
        public int No { get; set; }
    }

    public sealed class ManagerWeekSector
    {
        public string Sector { get; set; } = "";
        public int Total { get; set; }
        public int Pending { get; set; }
        public int Yes { get; set; }
        public int No { get; set; }
    }

    public interface IManagerWeekViewService
    {
        Task<ManagerWeekView> GetAsync(string weekTable, string? sector, CancellationToken ct);
    }

    public sealed class ManagerWeekViewService : IManagerWeekViewService
    {
        private readonly VvsDbContext _db;
        public ManagerWeekViewService(VvsDbContext db) => _db = db;

        public async Task<ManagerWeekView> GetAsync(string weekTable, string? sector, CancellationToken ct)
        {
            var conn = _db.Database.GetDbConnection();
            if (conn.State != System.Data.ConnectionState.Open)
                await conn.OpenAsync(ct);

            await using var cmd = conn.CreateCommand();
            cmd.CommandText = "sp_VVS_WT_ManagerView";
            cmd.CommandType = System.Data.CommandType.StoredProcedure;
            cmd.CommandTimeout = 120;
            cmd.Parameters.Add(new SqlParameter("@WeekTable", weekTable));
            cmd.Parameters.Add(new SqlParameter("@Sector", (object?)sector ?? DBNull.Value));

            var v = new ManagerWeekView();
            await using var rd = await cmd.ExecuteReaderAsync(ct);

            if (await rd.ReadAsync(ct))
            {
                v.Total = rd.GetInt32(0);
                v.Properties = rd.GetInt32(1);
                v.Pending = rd.GetInt32(2);
                v.Yes = rd.GetInt32(3);
                v.No = rd.GetInt32(4);
            }

            if (await rd.NextResultAsync(ct))
                while (await rd.ReadAsync(ct))
                    v.Valuers.Add(new ManagerWeekValuer
                    {
                        Sector = rd.GetString(0),
                        Username = rd.GetString(1),
                        Valuer = rd.GetString(2),
                        Total = rd.GetInt32(3),
                        Pending = rd.GetInt32(4),
                        Yes = rd.GetInt32(5),
                        No = rd.GetInt32(6)
                    });

            if (await rd.NextResultAsync(ct))
                while (await rd.ReadAsync(ct))
                    v.Sectors.Add(new ManagerWeekSector
                    {
                        Sector = rd.GetString(0),
                        Total = rd.GetInt32(1),
                        Pending = rd.GetInt32(2),
                        Yes = rd.GetInt32(3),
                        No = rd.GetInt32(4)
                    });

            return v;
        }
    }
}