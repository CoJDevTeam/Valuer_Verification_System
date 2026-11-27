using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Valuer_Verification_System.Data;

namespace Valuer_Verification_System.Services.Notice
{
    /// <summary>Week-table name helper: always [SECTION_78_NOTICE].[dbo].[name], letters/digits/underscore only.</summary>
    public static class WeekTableSql
    {
        public static string Short(string? name)
        {
            var n = (name ?? "").Trim();
            var dot = n.LastIndexOf('.'); if (dot >= 0) n = n[(dot + 1)..];
            n = n.Trim('[', ']');
            if (!Regex.IsMatch(n, "^[A-Za-z0-9_]+$")) throw new InvalidOperationException("Invalid week table name.");
            return n;
        }
        public static string Full(string? name) => $"[SECTION_78_NOTICE].[dbo].[{Short(name)}]";
    }

    public sealed class NoticeCheck { public string Label = ""; public bool Ok; public string Detail = ""; }

    public sealed class SectorLine { public string Sector = ""; public int Total, Yes, No, Pending; public bool Approved; }

    public sealed class NoticeGate
    {
        public string WeekLabel = ""; public int YesProperties; public List<SectorLine> Sectors = new();
        public int BatchId; public string BatchName = ""; public string? ShortWeekTable;
        public int Total, Yes, No, Pending;
        public bool SignedOff; public string? SignedBy;
        public DateOnly LetterDate, ReviewOpen, ReviewClose;
        public List<NoticeCheck> Checks = new();
        public bool Ready => Checks.Count > 0 && Checks.All(c => c.Ok);
    }

    public sealed class ENoticeStatus
    {
        public bool Found; public string? Message; public string? RunToken; public string? Status;
        public int Notices, WithEmail, NoEmail, Sent, SendFailed; public string? Error;
    }

    public interface INoticeRunService
    {
        Task<List<(int Id, string Name)>> BatchesAsync(CancellationToken ct);
        Task<NoticeGate> GateAsync(int batchId, CancellationToken ct);
        Task<(bool ok, string msg, string? token)> TriggerAsync(int batchId, string by, CancellationToken ct);
        Task<ENoticeStatus> StatusAsync(int batchId, CancellationToken ct);
        string ReviewBaseUrl { get; }
    }

    public sealed class NoticeRunService : INoticeRunService
    {
        private readonly VvsDbContext _db;
        private readonly HttpClient _http;
        private readonly IConfiguration _cfg;
        public NoticeRunService(VvsDbContext db, HttpClient http, IConfiguration cfg) { _db = db; _http = http; _cfg = cfg; }

        public string ReviewBaseUrl => (_cfg["Section78ENotice:BaseUrl"] ?? "").TrimEnd('/');
        private string ApiKey => (_cfg["Section78ENotice:ApiKey"] ?? "").Trim();

        public async Task<List<(int Id, string Name)>> BatchesAsync(CancellationToken ct)
        {
            var l = await _db.Batches.AsNoTracking().Where(b => b.CleanWeekTableName != null && b.CleanWeekTableName != "")
                .OrderByDescending(b => b.Id).Take(100)
                .Select(b => new { b.Id, b.BatchName, b.WeekWindow.WeekStart, b.WeekWindow.WeekEnd, b.WeekWindow.NoticeSendDate }).ToListAsync(ct);
            return l.Select(x => (x.Id, $"{x.WeekStart:dd MMM} – {x.WeekEnd:dd MMM yyyy}  (send {x.NoticeSendDate:dd MMM yyyy})  ·  {x.BatchName}")).ToList();
        }

        public async Task<NoticeGate> GateAsync(int batchId, CancellationToken ct)
        {
            var b = await _db.Batches.AsNoTracking().Include(x => x.WeekWindow).FirstOrDefaultAsync(x => x.Id == batchId, ct)
                    ?? throw new InvalidOperationException("Batch not found.");
            var g = new NoticeGate { BatchId = b.Id, BatchName = b.BatchName };
            g.WeekLabel = $"{b.WeekWindow.WeekStart:dd MMM} – {b.WeekWindow.WeekEnd:dd MMM yyyy}";
            g.LetterDate = b.WeekWindow.NoticeSendDate; g.ReviewOpen = b.WeekWindow.ReviewOpenDate; g.ReviewClose = b.WeekWindow.ReviewCloseDate;

            if (string.IsNullOrWhiteSpace(b.CleanWeekTableName))
            {
                g.Checks.Add(new NoticeCheck { Label = "Week table", Ok = false, Detail = "This batch has no cleaned week table." });
                return g;
            }
            g.ShortWeekTable = WeekTableSql.Short(b.CleanWeekTableName);
            var t = WeekTableSql.Full(b.CleanWeekTableName);

            await using var c = new SqlConnection(_db.Database.GetConnectionString());
            await c.OpenAsync(ct);
            await using (var cmd = new SqlCommand($@"SELECT COUNT(*), SUM(CASE WHEN Decision='Yes' THEN 1 ELSE 0 END),
                    SUM(CASE WHEN Decision='No' THEN 1 ELSE 0 END), SUM(CASE WHEN ISNULL(Decision,'Pending') NOT IN ('Yes','No') THEN 1 ELSE 0 END) FROM {t}", c))
            await using (var r = await cmd.ExecuteReaderAsync(ct))
                if (await r.ReadAsync(ct)) { g.Total = r.GetInt32(0); g.Yes = r.IsDBNull(1) ? 0 : r.GetInt32(1); g.No = r.IsDBNull(2) ? 0 : r.GetInt32(2); g.Pending = r.IsDBNull(3) ? 0 : r.GetInt32(3); }

            await using (var cmd = new SqlCommand($@"SELECT ISNULL(NULLIF(LTRIM(RTRIM(SECTOR)),''),'Unknown'), COUNT(*),
                    SUM(CASE WHEN Decision='Yes' THEN 1 ELSE 0 END), SUM(CASE WHEN Decision='No' THEN 1 ELSE 0 END),
                    SUM(CASE WHEN ISNULL(Decision,'Pending') NOT IN ('Yes','No') THEN 1 ELSE 0 END)
                    FROM {t} GROUP BY ISNULL(NULLIF(LTRIM(RTRIM(SECTOR)),''),'Unknown') ORDER BY 1", c))
            await using (var r = await cmd.ExecuteReaderAsync(ct))
                while (await r.ReadAsync(ct))
                    g.Sectors.Add(new SectorLine { Sector = r.GetString(0), Total = r.GetInt32(1), Yes = r.GetInt32(2), No = r.GetInt32(3), Pending = r.GetInt32(4) });
            await using (var cmd = new SqlCommand($"SELECT COUNT(DISTINCT VALUATION_KEY) FROM {t} WHERE Decision='Yes'", c))
                g.YesProperties = Convert.ToInt32(await cmd.ExecuteScalarAsync(ct) ?? 0);
            try
            {
                await using (var cmd = new SqlCommand("SELECT Sector FROM dbo.QaSectorApproval WHERE BatchId=@b", c))
                {
                    cmd.Parameters.AddWithValue("@b", batchId);
                    await using var r = await cmd.ExecuteReaderAsync(ct);
                    var ok = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    while (await r.ReadAsync(ct)) ok.Add(r.GetString(0));
                    foreach (var sl in g.Sectors) sl.Approved = ok.Contains(sl.Sector);
                }
            }
            catch (SqlException) { }

            try
            {
                await using var cmd = new SqlCommand("SELECT SignedBy FROM dbo.QaSignoff WHERE BatchId=@b", c);
                cmd.Parameters.AddWithValue("@b", batchId);
                var by = await cmd.ExecuteScalarAsync(ct) as string;
                g.SignedOff = by != null; g.SignedBy = by;
            }
            catch (SqlException) { g.SignedOff = false; }

            g.Checks.Add(new NoticeCheck { Label = "Week table found", Ok = true, Detail = g.ShortWeekTable! + " (" + g.Total + " rows)" });
            g.Checks.Add(new NoticeCheck { Label = "Every property verified", Ok = g.Pending == 0, Detail = g.Pending == 0 ? "No Pending rows." : g.Pending + " row(s) are still Pending." });
            g.Checks.Add(new NoticeCheck { Label = "QA signed off by the Senior Manager", Ok = g.SignedOff, Detail = g.SignedOff ? "Signed by " + g.SignedBy : "Not signed off yet." });
            g.Checks.Add(new NoticeCheck { Label = "Properties to send (Yes)", Ok = g.Yes > 0, Detail = g.Yes + " Yes, " + g.No + " No (No are not sent)." });
            return g;
        }

        public async Task<(bool ok, string msg, string? token)> TriggerAsync(int batchId, string by, CancellationToken ct)
        {
            var g = await GateAsync(batchId, ct);
            if (!g.Ready) return (false, "Not ready: " + string.Join(" ", g.Checks.Where(x => !x.Ok).Select(x => x.Label + " - " + x.Detail)), null);
            if (string.IsNullOrWhiteSpace(ReviewBaseUrl) || string.IsNullOrWhiteSpace(ApiKey) || ApiKey.StartsWith("<"))
                return (false, "Section78ENotice BaseUrl / ApiKey are not set in appsettings.json.", null);

            var b = await _db.Batches.AsNoTracking().FirstAsync(x => x.Id == batchId, ct);
            var extraction = await _db.ExtractRows.AsNoTracking().Where(r => r.BatchId == batchId && r.ExtractionDate.HasValue)
                .Select(r => r.ExtractionDate).FirstOrDefaultAsync(ct) ?? b.BatchDate;

            var body = new
            {
                weekTableName = g.ShortWeekTable, batchId, batchName = b.BatchName,
                extractionDate = extraction.ToString("yyyy-MM-dd"),
                letterDate = g.LetterDate.ToString("yyyy-MM-dd"),
                reviewOpenDate = g.ReviewOpen.ToString("yyyy-MM-dd"),
                reviewCloseDate = g.ReviewClose.ToString("yyyy-MM-dd"),
                requestedBy = by
            };

            try
            {
                using var req = new HttpRequestMessage(HttpMethod.Post, ReviewBaseUrl + "/api/section78-notice/trigger") { Content = JsonContent.Create(body) };
                req.Headers.Add("X-Api-Key", ApiKey);
                using var resp = await _http.SendAsync(req, ct);
                var text = await resp.Content.ReadAsStringAsync(ct);
                if (resp.StatusCode == System.Net.HttpStatusCode.Unauthorized) return (false, "eNotice refused the key. ApiKey here must equal Section78Notice:TriggerApiKey in eNotice.", null);
                if (!resp.IsSuccessStatusCode) return (false, "eNotice answered " + (int)resp.StatusCode + ": " + text, null);
                using var doc = JsonDocument.Parse(text);
                var token = doc.RootElement.TryGetProperty("runToken", out var t) ? t.GetString() : null;
                var created = doc.RootElement.TryGetProperty("created", out var cr) && cr.GetBoolean();
                return (true, created ? "Sent to eNotice. It is preparing the notices and will email the QA link." : "eNotice already has a request for this week table (see status below).", token);
            }
            catch (HttpRequestException ex) { return (false, "Cannot reach eNotice at " + ReviewBaseUrl + ". Is it running? " + ex.Message, null); }
            catch (TaskCanceledException) { return (false, "eNotice did not answer in time.", null); }
        }

        public async Task<ENoticeStatus> StatusAsync(int batchId, CancellationToken ct)
        {
            var b = await _db.Batches.AsNoTracking().FirstOrDefaultAsync(x => x.Id == batchId, ct);
            if (b == null || string.IsNullOrWhiteSpace(b.CleanWeekTableName)) return new ENoticeStatus { Message = "No week table." };
            try
            {
                var url = $"{ReviewBaseUrl}/api/section78-notice/status?weekTableName={WeekTableSql.Short(b.CleanWeekTableName)}&batchId={batchId}";
                using var req = new HttpRequestMessage(HttpMethod.Get, url);
                req.Headers.Add("X-Api-Key", ApiKey);
                using var resp = await _http.SendAsync(req, ct);
                if (resp.StatusCode == System.Net.HttpStatusCode.NotFound) return new ENoticeStatus { Message = "Not sent to eNotice yet." };
                if (!resp.IsSuccessStatusCode) return new ENoticeStatus { Message = "eNotice answered " + (int)resp.StatusCode + "." };
                using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync(ct));
                var r = doc.RootElement;
                int I(string n) => r.TryGetProperty(n, out var v) && v.ValueKind == JsonValueKind.Number ? v.GetInt32() : 0;
                string? S(string n) => r.TryGetProperty(n, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;
                return new ENoticeStatus { Found = true, RunToken = S("runToken"), Status = S("status"), Notices = I("notices"), WithEmail = I("withEmail"),
                    NoEmail = I("noEmail"), Sent = I("sent"), SendFailed = I("sendFailed"), Error = S("error") };
            }
            catch (Exception ex) { return new ENoticeStatus { Message = "Cannot reach eNotice: " + ex.Message }; }
        }
    }
}
