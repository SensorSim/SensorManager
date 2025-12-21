using Microsoft.EntityFrameworkCore;
using SensorManager.Models;

namespace SensorManager.Data;

public class SensorDbContext(DbContextOptions<SensorDbContext> options) : DbContext(options)
{
    public DbSet<SensorDefinition> Sensors => Set<SensorDefinition>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<SensorDefinition>()
            .HasIndex(s => s.SensorId)
            .IsUnique();

        // Seed a couple of default sensors for out-of-the-box demo
        modelBuilder.Entity<SensorDefinition>().HasData(
            new SensorDefinition
            {
                Id = Guid.Parse("11111111-1111-1111-1111-111111111111"),
                SensorId = "temp-1",
                SensorType = "temperature",
                Unit = "°C",
                OperatingMin = 50,
                OperatingMax = 150,
                WarningMin = 70,
                WarningMax = 130,
                IntervalMs = 2000,
                Enabled = true,
                Simulate = true,
                UpdatedAt = DateTimeOffset.UtcNow
            },
            new SensorDefinition
            {
                Id = Guid.Parse("22222222-2222-2222-2222-222222222222"),
                SensorId = "pressure-1",
                SensorType = "pressure",
                Unit = "bar",
                OperatingMin = 1,
                OperatingMax = 5,
                WarningMin = 1.5,
                WarningMax = 4.5,
                IntervalMs = 3000,
                Enabled = true,
                Simulate = true,
                UpdatedAt = DateTimeOffset.UtcNow
            }
        );
    }
}
