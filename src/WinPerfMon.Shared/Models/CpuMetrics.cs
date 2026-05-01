namespace WinPerfMon.Shared.Models;

public record CpuMetrics
{
    public DateTimeOffset Timestamp { get; init; }

    public float TotalLoad { get; init; }           // percent
    public float PackageTemperature { get; init; }  // °C
    public bool IsThermallyThrottled { get; init; }

    public required CoreMetrics[] Cores { get; init; }

    public int ProcessCount { get; init; }
    public int ThreadCount { get; init; }
    public long ContextSwitchesPerSec { get; init; }
}

public record CoreMetrics
{
    public int Index { get; init; }
    public bool IsEfficiencyCore { get; init; }     // Intel hybrid (E-core)
    public float Load { get; init; }                // percent
    public float FrequencyMhz { get; init; }
    public float TemperatureCelsius { get; init; }
}
