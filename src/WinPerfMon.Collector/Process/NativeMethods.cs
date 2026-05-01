using System.Runtime.InteropServices;

namespace WinPerfMon.Collector.Process;

/// <summary>
/// P/Invoke declarations for NtQuerySystemInformation and related Win32 APIs.
/// </summary>
internal static class NativeMethods
{
    // ── NtQuerySystemInformation ──────────────────────────────────────────

    [DllImport("ntdll.dll")]
    internal static extern int NtQuerySystemInformation(
        int systemInformationClass,
        IntPtr systemInformation,
        int systemInformationLength,
        out int returnLength);

    internal const int SystemProcessInformation = 5;

    [StructLayout(LayoutKind.Sequential)]
    internal struct SYSTEM_PROCESS_INFORMATION
    {
        public uint  NextEntryOffset;
        public uint  NumberOfThreads;
        public long  WorkingSetPrivateSize;
        public uint  HardFaultCount;
        public uint  NumberOfThreadsHighWatermark;
        public ulong CycleTime;
        public long  CreateTime;
        public long  UserTime;
        public long  KernelTime;
        public UNICODE_STRING ImageName;
        public int   BasePriority;
        public IntPtr UniqueProcessId;
        public IntPtr InheritedFromUniqueProcessId;
        public uint  HandleCount;
        public uint  SessionId;
        public IntPtr UniqueProcessKey;
        public IntPtr PeakVirtualSize;
        public IntPtr VirtualSize;
        public uint  PageFaultCount;
        public IntPtr PeakWorkingSetSize;
        public IntPtr WorkingSetSize;
        public IntPtr QuotaPeakPagedPoolUsage;
        public IntPtr QuotaPagedPoolUsage;
        public IntPtr QuotaPeakNonPagedPoolUsage;
        public IntPtr QuotaNonPagedPoolUsage;
        public IntPtr PagefileUsage;         // PrivateBytes
        public IntPtr PeakPagefileUsage;
        public IntPtr PrivatePageCount;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct UNICODE_STRING
    {
        public ushort Length;
        public ushort MaximumLength;
        public IntPtr Buffer;
    }

    // ── Process priority ──────────────────────────────────────────────────

    [DllImport("kernel32.dll", SetLastError = true)]
    internal static extern IntPtr OpenProcess(
        uint dwDesiredAccess, bool bInheritHandle, int dwProcessId);

    [DllImport("kernel32.dll", SetLastError = true)]
    internal static extern bool SetPriorityClass(IntPtr hProcess, uint dwPriorityClass);

    [DllImport("kernel32.dll", SetLastError = true)]
    internal static extern bool CloseHandle(IntPtr hObject);

    [DllImport("kernel32.dll", SetLastError = true)]
    internal static extern bool TerminateProcess(IntPtr hProcess, uint uExitCode);

    [DllImport("kernel32.dll")]
    internal static extern bool QueryFullProcessImageName(
        IntPtr hProcess, uint dwFlags, System.Text.StringBuilder lpExeName, ref uint lpdwSize);

    internal const uint PROCESS_TERMINATE          = 0x0001;
    internal const uint PROCESS_SET_INFORMATION    = 0x0200;
    internal const uint PROCESS_QUERY_INFORMATION  = 0x0400;
    internal const uint PROCESS_VM_READ            = 0x0010;
    internal const uint PROCESS_ALL_ACCESS         = 0x1F0FFF;

    internal const uint ABOVE_NORMAL_PRIORITY_CLASS = 0x00008000;
    internal const uint BELOW_NORMAL_PRIORITY_CLASS = 0x00004000;
    internal const uint HIGH_PRIORITY_CLASS         = 0x00000080;
    internal const uint IDLE_PRIORITY_CLASS         = 0x00000040;
    internal const uint NORMAL_PRIORITY_CLASS       = 0x00000020;
    internal const uint REALTIME_PRIORITY_CLASS     = 0x00000100;

    // ── Processor affinity ────────────────────────────────────────────────

    [DllImport("kernel32.dll", SetLastError = true)]
    internal static extern bool SetProcessAffinityMask(IntPtr hProcess, UIntPtr dwProcessAffinityMask);

    // ── File version info ─────────────────────────────────────────────────

    [DllImport("version.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    internal static extern uint GetFileVersionInfoSize(string lptstrFilename, out uint lpdwHandle);

    [DllImport("version.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    internal static extern bool GetFileVersionInfo(
        string lptstrFilename, uint dwHandle, uint dwLen, byte[] lpData);

    [DllImport("version.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    internal static extern bool VerQueryValue(
        byte[] pBlock, string lpSubBlock, out IntPtr lplpBuffer, out uint puLen);
}
