using Microsoft.Data.SqlClient;
using System.Data;
using Valuer_Verification_System.Models.Data;

namespace Valuer_Verification_System.Services.Permissions
{
    public class UserService : IUserService
    {
        private readonly string _connectionString;
        private readonly string _systemId;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public UserService(IConfiguration config, IHttpContextAccessor httpContextAccessor)
        {
            _connectionString = config.GetConnectionString("UserManagement")
                ?? throw new InvalidOperationException(
                    "Connection string 'UserManagement' is missing from appsettings.json.");

            // Read SystemId from config — change "AppSystem:SystemId" in appsettings to update it
            _systemId = config["AppSystem:SystemId"]
                ?? throw new InvalidOperationException(
                    "'AppSystem:SystemId' is missing from appsettings.json.");

            _httpContextAccessor = httpContextAccessor;
        }

        // ── Public API ────────────────────────────────────────────────────────

        public Task<AppUser?> GetCurrentUserAsync(CancellationToken ct = default)
        {
            var windowsUser = _httpContextAccessor.HttpContext?.User?.Identity?.Name;
            return string.IsNullOrWhiteSpace(windowsUser)
                ? Task.FromResult<AppUser?>(null)
                : GetUserAsync(windowsUser, ct);
        }

        public async Task<AppUser?> GetUserAsync(string windowsUsername, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(windowsUsername)) return null;

            await using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync(ct);

            await using var cmd = new SqlCommand("[dbo].[Login]", conn)
            {
                CommandType = CommandType.StoredProcedure
            };

            cmd.Parameters.AddWithValue("@Username", windowsUsername.Trim());
            cmd.Parameters.AddWithValue("@System", _systemId);

            await using var reader = await cmd.ExecuteReaderAsync(ct);

            return await reader.ReadAsync(ct) ? MapRow(reader) : null;
        }

        // ── Mapping ───────────────────────────────────────────────────────────

        private static AppUser MapRow(SqlDataReader r)
        {
            string? Str(string col)
            {
                try { return r.IsDBNull(r.GetOrdinal(col)) ? null : r.GetString(r.GetOrdinal(col)); }
                catch { return null; }
            }

            int? Int(string col)
            {
                try { return r.IsDBNull(r.GetOrdinal(col)) ? null : Convert.ToInt32(r.GetValue(r.GetOrdinal(col))); }
                catch { return null; }
            }

            bool Bool(string col)
            {
                try { return !r.IsDBNull(r.GetOrdinal(col)) && Convert.ToBoolean(r.GetValue(r.GetOrdinal(col))); }
                catch { return false; }
            }

            var firstName = Str("FirstName") ?? "";
            var secondName = Str("SecondName") ?? "";
            var surname = Str("Surname") ?? "";

            var fullName = string.Join(" ", new[] { firstName, secondName, surname }
                .Where(x => !string.IsNullOrWhiteSpace(x)))
                .Trim();

            if (string.IsNullOrWhiteSpace(fullName))
                fullName = Str("FullName") ?? Str("Username") ?? "Unknown User";

            return new AppUser
            {
                UserID = Int("UserID") ?? 0,
                Username = Str("Username") ?? "",
                UsernameOld = Str("UsernameOld") ?? "",

                FirstName = firstName,
                SecondName = secondName,
                Surname = surname,
                FullName = fullName,

                Position = Str("Position") ?? "",
                Role = Str("Role") ?? "",
                Active = Bool("Active")
            };
        }

       
    }
}
