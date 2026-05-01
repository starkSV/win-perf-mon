namespace WinPerfMon.Shared.Models;

public record GpuMetrics
{
    public DateTimeOffset Timestamp { get; init; }
    public string Name { get; init; } = string.Empty;
    public GpuVendor Vendor { get; init; }

    public float CoreLoad { get; init; }        // percent — 3D/graphics engine
    public float ComputeLoad { get; init; }     // percent
    public float VideoDecodeLoad { get; init; } // percent
    public float VideoEncodeLoad { get; init; } // percent

    public long VramUsedBytes { get; init; }
    public long VramTotalBytes { get; init; }

    public float CoreClockMhz { get; init; }
    public float MemoryClockMhz { get; init; }

    public float TemperatureCelsius { get; init; }
    public float FanRpm { get; init; }

    public float PcieBandwidthMbps { get; init; }
    public int PcieGeneration { get; init; }
    public int PcieLanes { get; init; }
}

public enum GpuVendor { Unknown, Nvidia, Amd, Intel }
