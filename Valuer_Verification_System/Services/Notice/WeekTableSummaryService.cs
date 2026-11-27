using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Valuer_Verification_System.Data;

namespace Valuer_Verification_System.Services.Notice
{
    public interface IWeekTableSummaryService
    {
        /// <summary>Totals, per-sector counts and unmatched valuers of a week table (the working table).</summary>
        Task<WeekTableSummary> GetAsync(string weekTable, CancellationToken ct);
    }

    public sealed class WeekTableSummary
    {
        public int TotalRows { get; set; }
        public int TotalProperties { get; set; }
        public int MatchedValuers { get; set; }
        public int UnmatchedRows { get; set; }
        public int Pending { get; set; }
        public int Yes { get; set; }
        public int No { get; set; }

        public List<WeekSectorSummary> Sectors { get; set; } = new();
        public List<WeekUnmatchedValuer> Unmatched { get; set; } = new();
        public List<WeekSectorValuer> SectorValuers { get; set; } = new();

        public int Verified => Yes + No;

        public decimal ProgressPercent =>
            TotalRows <= 0 ? 0 : Math.Round((decimal)Verified * 100m / TotalRows, 2);
    }

    public sealed class WeekSectorSummary
    {
        public string Sector { get; set; } = "";
        public int TotalRows { get; set; }
        public int TotalProperties { get; set; }
        public int TotalValuers { get; set; }
        public int UnmatchedRows { get; set; }
        public int Pending { get; set; }
        public int Yes { get; set; }
        public int No { get; set; }

        public int Verified => Yes + No;

        public decimal ProgressPercent =>
            TotalRows <= 0 ? 0 : Math.Round((decimal)Verified * 100m / TotalRows, 2);
    }

    public sealed class WeekUnmatchedValuer
    {
        public string Valuer { get; set; } = "";
        public int Rows { get; set; }
        public int Properties { get; set; }
        public string? MatchStatus { get; set; }
        public string? MatchComment { get; set; }
    }

    public sealed class WeekSectorValuer
    {
        public string Sector { get; set; } = "";
        public string Username { get; set; } = "";
        public string Valuer { get; set; } = "";
        public int TotalRows { get; set; }
        public int TotalProperties { get; set; }
    }

    public sealed class WeekTableSummaryService : IWeekTableSummaryService
    {
        private readonly VvsDbContext _db;

        public WeekTableSummaryService(VvsDbContext db) => _db = db;

        public async Task<WeekTableSummary> GetAsync(string weekTable, CancellationToken ct)
        {
            var conn = _db.Database.GetDbConnection();
            if (conn.State != System.Data.ConnectionState.Open)
                await conn.OpenAsync(ct);

            await using var cmd = conn.CreateCommand();
            cmd.CommandText = "sp_VVS_WT_Summary";
            cmd.CommandType = System.Data.CommandType.StoredProcedure;
            cmd.CommandTimeout = 120;
            cmd.Parameters.Add(new SqlParameter("@WeekTable", weekTable));

            var s = new WeekTableSummary();

            await using var rd = await cmd.ExecuteReaderAsync(ct);

            // 1. totals
            if (await rd.ReadAsync(ct))
            {
                s.TotalRows = rd.GetInt32(0);
                s.TotalProperties = rd.GetInt32(1);
                s.MatchedValuers = rd.GetInt32(2);
                s.UnmatchedRows = rd.GetInt32(3);
                s.Pending = rd.GetInt32(4);
                s.Yes = rd.GetInt32(5);
                s.No = rd.GetInt32(6);
            }

            // 2. per sector
            if (await rd.NextResultAsync(ct))
            {
                while (await rd.ReadAsync(ct))
                {
                    s.Sectors.Add(new WeekSectorSummary
                    {
                        Sector = rd.GetString(0),
                        TotalRows = rd.GetInt32(1),
                        TotalProperties = rd.GetInt32(2),
                        TotalValuers = rd.GetInt32(3),
                        UnmatchedRows = rd.GetInt32(4),
                        Pending = rd.GetInt32(5),
                        Yes = rd.GetInt32(6),
                        No = rd.GetInt32(7)
                    });
                }
            }

            // 3. unmatched valuers
            if (await rd.NextResultAsync(ct))
            {
                while (await rd.ReadAsync(ct))
                {
                    s.Unmatched.Add(new WeekUnmatchedValuer
                    {
                        Valuer = rd.GetString(0),
                        Rows = rd.GetInt32(1),
                        Properties = rd.GetInt32(2),
                        MatchStatus = rd.IsDBNull(3) ? null : rd.GetString(3),
                        MatchComment = rd.IsDBNull(4) ? null : rd.GetString(4)
                    });
                }
            }

            // 4. valuers per sector
            if (await rd.NextResultAsync(ct))
            {
                while (await rd.ReadAsync(ct))
                {
                    s.SectorValuers.Add(new WeekSectorValuer
                    {
                        Sector = rd.GetString(0),
                        Username = rd.GetString(1),
                        Valuer = rd.GetString(2),
                        TotalRows = rd.GetInt32(3),
                        TotalProperties = rd.GetInt32(4)
                    });
                }
            }

            return s;
        }
    }
}
