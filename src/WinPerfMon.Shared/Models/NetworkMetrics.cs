namespace WinPerfMon.Shared.Models;

public record NetworkMetrics
{
    public DateTimeOffset Timestamp { get; init; }
    public required InterfaceStats[] Interfaces { get; init; }
    public required ProcessNetworkStats[] TopProcesses { get; init; }
    public float GatewayRttMs { get; init; }
    public float CustomPingRttMs { get; init; }
    public string? CustomPingTarget { get; init; }
}

public record InterfaceStats
{
    public string Name { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public long DownloadBytesPerSec { get; init; }
    public long UploadBytesPerSec { get; init; }
    public long TotalDownloadBytes { get; init; }
    public long TotalUploadBytes { get; init; }
    public bool IsConnected { get; init; }
    public long SpeedBps { get; init; }
}

public record ProcessNetworkStats
{
    public int Pid { get; init; }
    public string ProcessName { get; init; } = string.Empty;
    public long DownloadBytesPerSec { get; init; }
    public long UploadBytesPerSec { get; init; }
}
