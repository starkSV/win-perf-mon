namespace WinPerfMon.Shared.Models;

public record StorageMetrics
{
    public DateTimeOffset Timestamp { get; init; }
    public required DiskStats[] Disks { get; init; }
}

public record DiskStats
{
    public string DeviceName { get; init; } = string.Empty;
    public string Model { get; init; } = string.Empty;
    public DiskType Type { get; init; }

    public long ReadBytesPerSec { get; init; }
    public long WriteBytesPerSec { get; init; }
    public float ReadIops { get; init; }
    public float WriteIops { get; init; }
    public float QueueDepth { get; init; }
    public float ActiveTimePercent { get; init; }   // % busy

    public SmartHealth Health { get; init; }
    public required SmartAttribute[] SmartAttributes { get; init; }

    // NVMe-specific (null for SATA)
    public NvmeStats? Nvme { get; init; }
    public float? TemperatureCelsius { get; init; }
}

public record SmartAttribute
{
    public byte Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public long RawValue { get; init; }
    public byte CurrentValue { get; init; }
    public byte ThresholdValue { get; init; }
    public bool IsFailing => CurrentValue <= ThresholdValue;
}

public record NvmeStats
{
    public byte PercentageUsed { get; init; }       // wear level: 0–100
    public long DataUnitsWritten { get; init; }     // 512KB units
    public long DataUnitsRead { get; init; }
    public long MediaErrors { get; init; }
    public long PowerOnHours { get; init; }
}

public enum DiskType { Unknown, Hdd, Sata, Nvme }
public enum SmartHealth { Healthy, Warning, Failing, Unknown }
