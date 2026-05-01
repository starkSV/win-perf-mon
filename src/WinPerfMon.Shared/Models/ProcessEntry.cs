namespace WinPerfMon.Shared.Models;

public record ProcessEntry
{
    public int     Pid              { get; init; }
    public int     ParentPid        { get; init; }
    public string  Name             { get; init; } = string.Empty;
    public string  FullPath         { get; init; } = string.Empty;
    public string  Description      { get; init; } = string.Empty;

    // CPU
    public float   CpuPercent       { get; init; }   // 0–100 across all cores
    public int     ThreadCount      { get; init; }
    public int     HandleCount      { get; init; }

    // Memory
    public long    WorkingSetBytes  { get; init; }   // physical RAM in use
    public long    PrivateBytesBytes{ get; init; }   // committed private memory
    public long    PagedPoolBytes   { get; init; }
    public long    NonPagedPoolBytes{ get; init; }

    // Disk I/O
    public long    DiskReadBytesPerSec  { get; init; }
    public long    DiskWriteBytesPerSec { get; init; }

    // Network I/O
    public long    NetSendBytesPerSec   { get; init; }
    public long    NetRecvBytesPerSec   { get; init; }

    // GPU (best-effort — requires DXGI/ETW)
    public float   GpuPercent       { get; init; }

    // Status
    public ProcessStatus Status     { get; init; }
    public DateTimeOffset StartTime { get; init; }

    // Derived display helpers (not stored)
    public string WorkingSetDisplay  => FormatBytes(WorkingSetBytes);
    public string DiskDisplay        => DiskReadBytesPerSec + DiskWriteBytesPerSec > 0
        ? $"{FormatBytes(DiskReadBytesPerSec + DiskWriteBytesPerSec)}/s"
        : "—";
    public string NetworkDisplay     => NetSendBytesPerSec + NetRecvBytesPerSec > 0
        ? $"{FormatBytes(NetSendBytesPerSec + NetRecvBytesPerSec)}/s"
        : "—";

    private static string FormatBytes(long b) => b switch
    {
        >= 1_073_741_824 => $"{b / 1_073_741_824.0:F1} GB",
        >= 1_048_576     => $"{b / 1_048_576.0:F1} MB",
        >= 1_024         => $"{b / 1_024.0:F0} KB",
        _                => $"{b} B",
    };
}

public enum ProcessStatus { Running, Suspended, NotResponding }

public enum ProcessSortColumn
{
    Name, Pid, Cpu, Memory, Disk, Network, Gpu
}
