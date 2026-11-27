using Microsoft.Extensions.Options;

namespace Valuer_Verification_System.Infrastructure.Configuration
{
    /// <summary>
    /// Environment switch (appsettings "AppMode"):
    ///   Development = true                  -> everything points to the Developer
    ///   Development = false, Testing = true -> everything points to the Testers (3 or more)
    ///   both false                          -> Production, real recipients, no test allocation
    /// Default is Development, so a missing setting can never email real people.
    /// </summary>
    public sealed class AppModeSettings
    {
        public const string SectionName = "AppMode";

        public bool Development { get; set; } = true;
        public bool Testing { get; set; }

        /// <summary>How many rows each person (Developer, or each Tester) gets to verify in a new week table.</summary>
        public int AllocateCount { get; set; } = 20;

        public AppModePerson Developer { get; set; } = new();

        /// <summary>The testers (3 or more). In Testing mode emails go to all of them and each one gets own rows.</summary>
        public List<AppModePerson> Testers { get; set; } = new();
    }

    public sealed class AppModePerson
    {
        public string Email { get; set; } = "";
        public string Username { get; set; } = "";
    }

    public enum AppModeKind
    {
        Development,
        Testing,
        Production
    }

    public interface IAppMode
    {
        AppModeKind Kind { get; }
        bool IsProduction { get; }

        /// <summary>"Development", "Testing" or "Production".</summary>
        string Label { get; }

        /// <summary>Every email goes to these addresses when not in Production. Empty in Production.</summary>
        IReadOnlyList<string> RedirectEmails { get; }

        /// <summary>Usernames that each get their own block of rows to verify when not in Production, in order.</summary>
        IReadOnlyList<string> AllocateUsernames { get; }

        int AllocateCount { get; }
    }

    public sealed class AppMode : IAppMode
    {
        private readonly AppModeSettings _s;

        public AppMode(IOptions<AppModeSettings> opt) => _s = opt.Value;

        public AppModeKind Kind =>
            _s.Development ? AppModeKind.Development :
            _s.Testing ? AppModeKind.Testing :
            AppModeKind.Production;

        public bool IsProduction => Kind == AppModeKind.Production;

        public string Label => Kind.ToString();

        private IEnumerable<AppModePerson> People =>
            Kind == AppModeKind.Development ? new[] { _s.Developer } :
            Kind == AppModeKind.Testing ? _s.Testers :
            Enumerable.Empty<AppModePerson>();

        // "<...>" values are placeholders that were never filled in: ignore them.
        private static bool Real(string? v) =>
            !string.IsNullOrWhiteSpace(v) && !v.Trim().StartsWith("<");

        public IReadOnlyList<string> RedirectEmails =>
            People.Where(p => p != null && Real(p.Email))
                  .Select(p => p.Email.Trim())
                  .Distinct(StringComparer.OrdinalIgnoreCase)
                  .ToList();

        public IReadOnlyList<string> AllocateUsernames =>
            People.Where(p => p != null && Real(p.Username))
                  .Select(p => p.Username.Trim())
                  .Distinct(StringComparer.OrdinalIgnoreCase)
                  .ToList();

        public int AllocateCount => _s.AllocateCount > 0 ? _s.AllocateCount : 20;
    }
}
