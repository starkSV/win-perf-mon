using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using WinPerfMon.Shared.Models;

namespace WinPerfMon.Collector.Process;

/// <summary>
/// Samples the full process list via NtQuerySystemInformation.
/// CPU % is computed from kernel+user time deltas between two consecutive calls.
/// Disk/network I/O deltas are populated here when ETW is not available,
/// using Process.TotalProcessorTime as a fallback.
/// </summary>
public sealed class ProcessCollector
{
    // Previous cycle state for delta calculations
    private Dictionary<int, (long kernelTime, long userTime, long readBytes, long writeBytes)> _prev = new();
    private DateTimeOffset _prevTime = DateTimeOffset.UtcNow;
    private readonly int _logicalCoreCount = Environment.ProcessorCount;

    // Cache: PID → exe path (expensive to query every cycle)
    private readonly Dictionary<int, string> _pathCache = new();
    private readonly Dictionary<int, string> _descCache  = new();

    public IReadOnlyList<ProcessEntry> Sample()
    {
        var now = DateTimeOffset.UtcNow;
        double elapsedMs = (now - _prevTime).TotalMilliseconds;
        if (elapsedMs < 1) elapsedMs = 1000;

        var rawList = QueryNt();
        var entries = new List<ProcessEntry>(rawList.Count);
        var newPrev  = new Dictionary<int, (long, long, long, long)>(rawList.Count);

        foreach (var raw in rawList)
        {
            int pid = raw.Pid;

            // ── CPU % ──────────────────────────────────────────────────────
            float cpuPct = 0f;
            if (_prev.TryGetValue(pid, out var prev))
            {
                long dKernel = raw.KernelTime - prev.kernelTime;
                long dUser   = raw.UserTime   - prev.userTime;
                // Times are in 100-ns units; elapsedMs * 10000 = elapsed in 100-ns
                double elapsedTicks = elapsedMs * 10_000.0 * _logicalCoreCount;
                cpuPct = (float)Math.Min(100.0, (dKernel + dUser) / elapsedTicks * 100.0);
            }

            // ── Disk I/O delta ─────────────────────────────────────────────
            long readDelta  = 0;
            long writeDelta = 0;
            if (_prev.TryGetValue(pid, out var prevIo))
            {
                readDelta  = Math.Max(0, raw.ReadTransferCount  - prevIo.readBytes);
                writeDelta = Math.Max(0, raw.WriteTransferCount - prevIo.writeBytes);
            }

            newPrev[pid] = (raw.KernelTime, raw.UserTime, raw.ReadTransferCount, raw.WriteTransferCount);

            entries.Add(new ProcessEntry
            {
                Pid               = pid,
                ParentPid         = raw.ParentPid,
                Name              = raw.Name,
                FullPath          = GetPath(pid),
                Description       = GetDescription(pid),
                CpuPercent        = cpuPct,
                ThreadCount       = raw.ThreadCount,
                HandleCount       = raw.HandleCount,
                WorkingSetBytes   = raw.WorkingSetSize,
                PrivateBytesBytes = raw.PrivateBytes,
                PagedPoolBytes    = raw.PagedPoolUsage,
                NonPagedPoolBytes = raw.NonPagedPoolUsage,
                DiskReadBytesPerSec  = (long)(readDelta  / (elapsedMs / 1000.0)),
                DiskWriteBytesPerSec = (long)(writeDelta / (elapsedMs / 1000.0)),
                NetSendBytesPerSec   = 0,  // populated by ETW in NetworkAnalyzer
                NetRecvBytesPerSec   = 0,
                GpuPercent        = 0f,    // populated by DXGI/ETW later
                Status            = ProcessStatus.Running,
                StartTime         = raw.CreateTime > 0 ? DateTimeOffset.FromFileTime(raw.CreateTime) : DateTimeOffset.MinValue,
            });
        }

        _prev     = newPrev;
        _prevTime = now;

        return entries;
    }

    // ── Process control ────────────────────────────────────────────────────

    public static bool Kill(int pid)
    {
        var hProcess = NativeMethods.OpenProcess(NativeMethods.PROCESS_TERMINATE, false, pid);
        if (hProcess == IntPtr.Zero) return false;
        try   { return NativeMethods.TerminateProcess(hProcess, 1); }
        finally { NativeMethods.CloseHandle(hProcess); }
    }

    public static bool SetPriority(int pid, ProcessPriorityClass priority)
    {
        var hProcess = NativeMethods.OpenProcess(NativeMethods.PROCESS_SET_INFORMATION, false, pid);
        if (hProcess == IntPtr.Zero) return false;
        uint flag = priority switch
        {
            ProcessPriorityClass.RealTime    => NativeMethods.REALTIME_PRIORITY_CLASS,
            ProcessPriorityClass.High        => NativeMethods.HIGH_PRIORITY_CLASS,
            ProcessPriorityClass.AboveNormal => NativeMethods.ABOVE_NORMAL_PRIORITY_CLASS,
            ProcessPriorityClass.Normal      => NativeMethods.NORMAL_PRIORITY_CLASS,
            ProcessPriorityClass.BelowNormal => NativeMethods.BELOW_NORMAL_PRIORITY_CLASS,
            ProcessPriorityClass.Idle        => NativeMethods.IDLE_PRIORITY_CLASS,
            _ => NativeMethods.NORMAL_PRIORITY_CLASS,
        };
        try   { return NativeMethods.SetPriorityClass(hProcess, flag); }
        finally { NativeMethods.CloseHandle(hProcess); }
    }

    public static bool SetAffinity(int pid, long affinityMask)
    {
        var hProcess = NativeMethods.OpenProcess(NativeMethods.PROCESS_SET_INFORMATION, false, pid);
        if (hProcess == IntPtr.Zero) return false;
        try   { return NativeMethods.SetProcessAffinityMask(hProcess, (UIntPtr)affinityMask); }
        finally { NativeMethods.CloseHandle(hProcess); }
    }

    // ── Helpers ────────────────────────────────────────────────────────────

    private string GetPath(int pid)
    {
        if (pid <= 4) return string.Empty;
        if (_pathCache.TryGetValue(pid, out var cached)) return cached;
        try
        {
            var hProcess = NativeMethods.OpenProcess(
                NativeMethods.PROCESS_QUERY_INFORMATION | NativeMethods.PROCESS_VM_READ, false, pid);
            if (hProcess == IntPtr.Zero) return string.Empty;
            try
            {
                var sb = new StringBuilder(1024);
                uint size = (uint)sb.Capacity;
                NativeMethods.QueryFullProcessImageName(hProcess, 0, sb, ref size);
                var path = sb.ToString();
                _pathCache[pid] = path;
                return path;
            }
            finally { NativeMethods.CloseHandle(hProcess); }
        }
        catch { return string.Empty; }
    }

    private string GetDescription(int pid)
    {
        if (pid <= 4) return string.Empty;
        if (_descCache.TryGetValue(pid, out var cached)) return cached;
        try
        {
            var path = GetPath(pid);
            if (string.IsNullOrEmpty(path)) return string.Empty;
            var desc = FileVersionInfo.GetVersionInfo(path).FileDescription ?? string.Empty;
            _descCache[pid] = desc;
            return desc;
        }
        catch { return string.Empty; }
    }

    // ── NtQuerySystemInformation parsing ──────────────────────────────────

    private sealed record RawProcess(
        int Pid, int ParentPid, string Name, int ThreadCount, int HandleCount,
        long KernelTime, long UserTime, long CreateTime,
        long WorkingSetSize, long PrivateBytes, long PagedPoolUsage, long NonPagedPoolUsage,
        long ReadTransferCount, long WriteTransferCount);

    private static List<RawProcess> QueryNt()
    {
        int bufferSize = 1024 * 1024; // start 1MB, grow if needed
        IntPtr buffer = Marshal.AllocHGlobal(bufferSize);
        try
        {
            int status;
            int returnLength;
            while (true)
            {
                status = NativeMethods.NtQuerySystemInformation(
                    NativeMethods.SystemProcessInformation, buffer, bufferSize, out returnLength);

                if (status == 0) break;                // STATUS_SUCCESS
                if (status != unchecked((int)0xC0000004)) // STATUS_INFO_LENGTH_MISMATCH
                    return [];

                bufferSize = returnLength + 4096;
                Marshal.FreeHGlobal(buffer);
                buffer = Marshal.AllocHGlobal(bufferSize);
            }

            var results = new List<RawProcess>(256);
            IntPtr ptr = buffer;

            while (true)
            {
                var info = Marshal.PtrToStructure<NativeMethods.SYSTEM_PROCESS_INFORMATION>(ptr);

                string name = info.ImageName.Buffer != IntPtr.Zero
                    ? Marshal.PtrToStringUni(info.ImageName.Buffer, info.ImageName.Length / 2) ?? ""
                    : "(System Idle)";

                // IO counters are at a fixed offset after the main struct
                // SYSTEM_PROCESS_INFORMATION is followed by SYSTEM_THREAD_INFORMATION entries,
                // but IO counters are embedded in the struct itself at these offsets.
                // We read them via separate offset calculation for safety.
                long readBytes  = ReadIoCounter(ptr, ioReadOffset:  true);
                long writeBytes = ReadIoCounter(ptr, ioReadOffset: false);

                results.Add(new RawProcess(
                    Pid:            (int)info.UniqueProcessId,
                    ParentPid:      (int)info.InheritedFromUniqueProcessId,
                    Name:           name,
                    ThreadCount:    (int)info.NumberOfThreads,
                    HandleCount:    (int)info.HandleCount,
                    KernelTime:     info.KernelTime,
                    UserTime:       info.UserTime,
                    CreateTime:     info.CreateTime,
                    WorkingSetSize: (long)info.WorkingSetSize,
                    PrivateBytes:   (long)info.PagefileUsage,
                    PagedPoolUsage: (long)info.QuotaPagedPoolUsage,
                    NonPagedPoolUsage: (long)info.QuotaNonPagedPoolUsage,
                    ReadTransferCount:  readBytes,
                    WriteTransferCount: writeBytes
                ));

                if (info.NextEntryOffset == 0) break;
                ptr = IntPtr.Add(ptr, (int)info.NextEntryOffset);
            }

            return results;
        }
        finally { Marshal.FreeHGlobal(buffer); }
    }

    // IO_COUNTERS is embedded in SYSTEM_PROCESS_INFORMATION at a known offset (varies by arch)
    // On x64: ReadOperationCount at offset 0x58, ReadTransferCount at 0x68, WriteTransferCount at 0x78
    private static long ReadIoCounter(IntPtr basePtr, bool ioReadOffset)
    {
        // Offsets for x64: IO_COUNTERS.ReadTransferCount = +0x68, WriteTransferCount = +0x78
        int offset = ioReadOffset ? 0x68 : 0x78;
        try { return Marshal.ReadInt64(IntPtr.Add(basePtr, offset)); }
        catch { return 0; }
    }
}
