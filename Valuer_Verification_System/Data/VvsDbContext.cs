using Microsoft.EntityFrameworkCore;
using Valuer_Verification_System.Models.Data;

namespace Valuer_Verification_System.Data
{
    public sealed class VvsDbContext : DbContext
    {
        public VvsDbContext(DbContextOptions<VvsDbContext> options) : base(options) { }

        public DbSet<WeekWindow> WeekWindows => Set<WeekWindow>();
        public DbSet<Batch> Batches => Set<Batch>();
        public DbSet<ExtractFile> ExtractFiles => Set<ExtractFile>();
        public DbSet<ExtractRow> ExtractRows => Set<ExtractRow>();
        public DbSet<Assignment> Assignments => Set<Assignment>();
        public DbSet<AssignmentHistory> AssignmentHistories => Set<AssignmentHistory>();
        public DbSet<EmailArchive> EmailArchives => Set<EmailArchive>();
        public DbSet<StatsFile> StatsFiles => Set<StatsFile>();
        public DbSet<Notification> Notifications => Set<Notification>();
        public DbSet<SectorManager> SectorManagers => Set<SectorManager>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // ── WeekWindow ────────────────────────────────────────────────────
            modelBuilder.Entity<WeekWindow>()
                .HasIndex(x => x.WeekStart)
                .IsUnique();

            // ── Batch ─────────────────────────────────────────────────────────
            modelBuilder.Entity<Batch>()
                .HasIndex(x => x.BatchName)
                .IsUnique();

            modelBuilder.Entity<Batch>()
                .HasOne(x => x.ExtractFile)
                .WithOne(x => x.Batch)
                .HasForeignKey<ExtractFile>(x => x.BatchId)
                .OnDelete(DeleteBehavior.Cascade);

            // ── ExtractRow indexes ────────────────────────────────────────────
            //
            // Multipurpose / Split properties share the same VALUATION_KEY.
            // e.g. key 10604186 has 3 rows: SplitInd = P, Y, Y
            //
            // BEFORE (broken): unique(BatchId, ValuationKey)
            //   → blocked sibling split rows from being inserted
            //
            // AFTER (correct):
            //   1. Non-unique index on (BatchId, ValuationKey)
            //      → fast lookups of all rows for one property
            //   2. Unique index on (BatchId, ValuationKey, ValuationSplitNo)
            //      → still prevents true accidental duplicates
            //      → allows P row (SplitNo=0) + Y rows (SplitNo=8049596, 8049597)

            modelBuilder.Entity<ExtractRow>()
                .HasIndex(x => new { x.BatchId, x.ValuationKey })
                .HasDatabaseName("IX_ExtractRows_BatchId_ValuationKey");   // non-unique

            modelBuilder.Entity<ExtractRow>()
                .HasIndex(x => new { x.BatchId, x.ValuationKey, x.ValuationSplitNo })
                .IsUnique()
                .HasDatabaseName("IX_ExtractRows_BatchId_ValuationKey_SplitNo");

            modelBuilder.Entity<ExtractRow>()
                .HasIndex(x => new { x.BatchId, x.Sector, x.Decision });

            modelBuilder.Entity<ExtractRow>()
                .HasIndex(x => new { x.BatchId, x.Valuer });

            modelBuilder.Entity<ExtractRow>()
                .Property(x => x.MarketValue)
                .HasPrecision(18, 2);

            modelBuilder.Entity<ExtractRow>()
                .Property(x => x.RateableArea)
                .HasPrecision(18, 2);

            modelBuilder.Entity<ExtractRow>()
                .Property(x => x.RightsPremiseSize)
                .HasPrecision(18, 2);

            // ── Assignment ────────────────────────────────────────────────────
            modelBuilder.Entity<Assignment>()
                .HasIndex(x => x.ExtractRowId)
                .IsUnique();

            modelBuilder.Entity<Assignment>()
                .HasOne(x => x.ExtractRow)
                .WithOne(x => x.Assignment)
                .HasForeignKey<Assignment>(x => x.ExtractRowId)
                .OnDelete(DeleteBehavior.Cascade);

            // ── AssignmentHistory ─────────────────────────────────────────────
            modelBuilder.Entity<AssignmentHistory>()
                .HasIndex(x => new { x.AssignmentId, x.ChangedAtUtc });

            // ── EmailArchive ──────────────────────────────────────────────────
            modelBuilder.Entity<EmailArchive>()
                .HasIndex(x => new { x.BatchId, x.Kind, x.SentAtUtc });

            // ── StatsFile ─────────────────────────────────────────────────────
            modelBuilder.Entity<StatsFile>()
                .HasIndex(x => new { x.BatchId, x.Kind, x.GeneratedAtUtc });

            // ── Notification ──────────────────────────────────────────────────
            modelBuilder.Entity<Notification>()
                .HasIndex(x => new { x.Audience, x.TargetKey, x.DedupKey })
                .IsUnique();

            modelBuilder.Entity<Notification>()
                .HasIndex(x => new { x.Audience, x.TargetKey, x.IsRead, x.CreatedAtUtc });
        }
    }
}