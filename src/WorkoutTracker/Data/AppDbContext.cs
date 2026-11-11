using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace WorkoutTracker.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options), IDataProtectionKeyContext
{
    public DbSet<WorkoutType> WorkoutTypes => Set<WorkoutType>();
    public DbSet<Workout> Workouts => Set<Workout>();
    public DbSet<Routine> Routines => Set<Routine>();
    public DbSet<RoutineItem> RoutineItems => Set<RoutineItem>();
    public DbSet<RoutineRun> RoutineRuns => Set<RoutineRun>();
    public DbSet<DataProtectionKey> DataProtectionKeys => Set<DataProtectionKey>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<WorkoutType>(e =>
        {
            e.Property(x => x.Name).HasMaxLength(100).IsRequired();
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
            e.Property(x => x.Name).HasMaxLength(100).IsRequired();
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
}
