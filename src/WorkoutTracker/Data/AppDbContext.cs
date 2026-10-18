using Microsoft.EntityFrameworkCore;

namespace WorkoutTracker.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<WorkoutType> WorkoutTypes => Set<WorkoutType>();
    public DbSet<Workout> Workouts => Set<Workout>();

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
        });
    }
}
