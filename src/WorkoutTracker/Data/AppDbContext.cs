using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace WorkoutTracker.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options), IDataProtectionKeyContext
{
    public DbSet<WorkoutType> WorkoutTypes => Set<WorkoutType>();
    public DbSet<Workout> Workouts => Set<Workout>();
    public DbSet<Routine> Routines => Set<Routine>();
    public DbSet<RoutineItem> RoutineItems => Set<RoutineItem>();
    public DbSet<RoutineRun> RoutineRuns => Set<RoutineRun>();
    public DbSet<DataProtectionKey> DataProtectionKeys => Set<DataProtectionKey>();

    // "Push ups" and "push ups" are the same name: the unique indexes and lookups by name ignore case.
    public const string CaseInsensitive = "case_insensitive";

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        // Every DateTime here is UTC. Npgsql refuses to write any other Kind to a timestamptz column, and a value
        // can lose its Kind on the way, e.g. in the workout state the browser keeps.
        configurationBuilder.Properties<DateTime>().HaveConversion<UtcConverter>();
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasCollation(CaseInsensitive, locale: "und-u-ks-level2", provider: "icu", deterministic: false);
        modelBuilder.Entity<WorkoutType>(e =>
        {
            e.Property(x => x.Name).HasMaxLength(100).IsRequired().UseCollation(CaseInsensitive);
            e.HasIndex(x => x.Name).IsUnique();
        });
        modelBuilder.Entity<Workout>(e =>
        {
            e.HasOne(x => x.WorkoutType).WithMany(t => t.Workouts)
                .HasForeignKey(x => x.WorkoutTypeId).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(x => new { x.WorkoutTypeId, x.StartedAtUtc });
            e.HasIndex(x => x.ClientId).IsUnique();
        });
        modelBuilder.Entity<Routine>(e =>
        {
            e.Property(x => x.Name).HasMaxLength(100).IsRequired().UseCollation(CaseInsensitive);
            e.HasIndex(x => x.Name).IsUnique();
        });
        modelBuilder.Entity<RoutineItem>(e =>
        {
            e.Property(x => x.ProposedReps).HasMaxLength(100);
            e.HasOne(x => x.Routine).WithMany(r => r.Items)
                .HasForeignKey(x => x.RoutineId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.WorkoutType).WithMany()
                .HasForeignKey(x => x.WorkoutTypeId).OnDelete(DeleteBehavior.Cascade);
        });
        modelBuilder.Entity<RoutineRun>(e =>
        {
            e.HasOne(x => x.Routine).WithMany(r => r.Runs)
                .HasForeignKey(x => x.RoutineId).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(x => new { x.RoutineId, x.StartedAtUtc });
            e.HasIndex(x => x.ClientId).IsUnique();
        });
    }

    sealed class UtcConverter() : ValueConverter<DateTime, DateTime>(
        v => DateTime.SpecifyKind(v, DateTimeKind.Utc), v => DateTime.SpecifyKind(v, DateTimeKind.Utc));
}
