using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Reflection.Emit;
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

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // WeekWindow: unique per WeekStart
            modelBuilder.Entity<WeekWindow>()
                .HasIndex(x => x.WeekStart)
                .IsUnique();

            // Batch: unique BatchName
            modelBuilder.Entity<Batch>()
                .HasIndex(x => x.BatchName)
                .IsUnique();

            modelBuilder.Entity<Batch>()
                .HasOne(x => x.ExtractFile)
                .WithOne(x => x.Batch)
                .HasForeignKey<ExtractFile>(x => x.BatchId)
                .OnDelete(DeleteBehavior.Cascade);

            // ExtractRow: uniqueness per batch + valuationKey (prevents duplicate imports)
            modelBuilder.Entity<ExtractRow>()
                .HasIndex(x => new { x.BatchId, x.ValuationKey })
                .IsUnique();

            // Helpful indexes for inbox + stats queries
            modelBuilder.Entity<ExtractRow>()
                .HasIndex(x => new { x.BatchId, x.Sector, x.Decision });

            modelBuilder.Entity<ExtractRow>()
                .HasIndex(x => new { x.BatchId, x.Valuer });

            // Assignment 1:1 ExtractRow
            modelBuilder.Entity<Assignment>()
                .HasIndex(x => x.ExtractRowId)
                .IsUnique();

            modelBuilder.Entity<Assignment>()
                .HasOne(x => x.ExtractRow)
                .WithOne(x => x.Assignment)
                .HasForeignKey<Assignment>(x => x.ExtractRowId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<AssignmentHistory>()
                .HasIndex(x => new { x.AssignmentId, x.ChangedAtUtc });

            modelBuilder.Entity<EmailArchive>()
                .HasIndex(x => new { x.BatchId, x.Kind, x.SentAtUtc });

            modelBuilder.Entity<StatsFile>()
                .HasIndex(x => new { x.BatchId, x.Kind, x.GeneratedAtUtc });

            // Decimal precision for MarketValue
            modelBuilder.Entity<ExtractRow>()
                .Property(x => x.MarketValue)
                .HasPrecision(18, 2);

            modelBuilder.Entity<Notification>()
    .HasIndex(x => new { x.Audience, x.TargetKey, x.DedupKey })
    .IsUnique();

            modelBuilder.Entity<Notification>()
                .HasIndex(x => new { x.Audience, x.TargetKey, x.IsRead, x.CreatedAtUtc });

        }
    }
}