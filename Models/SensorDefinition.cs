namespace SensorManager.Models;

public class SensorDefinition
{
    public Guid Id { get; set; } = Guid.NewGuid();

    // Stable logical identifier used in measurements (e.g., "temp-1")
    public required string SensorId { get; set; }

    public required string SensorType { get; set; } // temperature / pressure / etc.

    public required string Unit { get; set; } // °C, bar, etc.

    public double OperatingMin { get; set; }
    public double OperatingMax { get; set; }

    public double WarningMin { get; set; }
    public double WarningMax { get; set; }

    // Simulation parameters
    public int IntervalMs { get; set; } = 2000;
    public bool Enabled { get; set; } = true;

    // If true, SensorSimulator should run it (used for "create/remove sensor")
    public bool Simulate { get; set; } = true;

    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}
