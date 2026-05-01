using System.Collections.ObjectModel;
using Microsoft.UI.Dispatching;
using WinPerfMon.Collector.Process;
using WinPerfMon.Shared.Models;

namespace WinPerfMon.App.ViewModels;

public sealed class ProcessesViewModel : BaseViewModel, IDisposable
{
    private readonly ProcessCollector _collector = new();
    private readonly DispatcherQueue  _dispatcher;
    private readonly Timer            _timer;

    private ProcessSortColumn _sortColumn = ProcessSortColumn.Cpu;
    private bool              _sortAscending = false;
    private string            _filter = string.Empty;
    private int               _totalProcesses;
    private int               _totalThreads;

    public ObservableCollection<ProcessEntry> Processes { get; } = [];

    public int TotalProcesses { get => _totalProcesses; private set => Set(ref _totalProcesses, value); }
    public int TotalThreads   { get => _totalThreads;   private set => Set(ref _totalThreads, value); }

    public string Filter
    {
        get => _filter;
        set { Set(ref _filter, value); ApplyFilterAndSort(_lastSnapshot); }
    }

    public ProcessSortColumn SortColumn
    {
        get => _sortColumn;
        set { Set(ref _sortColumn, value); ApplyFilterAndSort(_lastSnapshot); }
    }

    public bool SortAscending
    {
        get => _sortAscending;
        set { Set(ref _sortAscending, value); ApplyFilterAndSort(_lastSnapshot); }
    }

    private IReadOnlyList<ProcessEntry> _lastSnapshot = [];

    public ProcessesViewModel()
    {
        _dispatcher = DispatcherQueue.GetForCurrentThread();
        _timer = new Timer(Refresh, null, TimeSpan.Zero, TimeSpan.FromSeconds(1));
    }

    private void Refresh(object? _)
    {
        IReadOnlyList<ProcessEntry> snapshot;
        try   { snapshot = _collector.Sample(); }
        catch { return; }

        _dispatcher.TryEnqueue(() =>
        {
            _lastSnapshot = snapshot;
            TotalProcesses = snapshot.Count;
            TotalThreads   = snapshot.Sum(p => p.ThreadCount);
            ApplyFilterAndSort(snapshot);
        });
    }

    private void ApplyFilterAndSort(IReadOnlyList<ProcessEntry> source)
    {
        var filtered = string.IsNullOrWhiteSpace(_filter)
            ? source
            : source.Where(p =>
                p.Name.Contains(_filter, StringComparison.OrdinalIgnoreCase) ||
                p.Pid.ToString().Contains(_filter) ||
                p.Description.Contains(_filter, StringComparison.OrdinalIgnoreCase));

        var sorted = (_sortColumn, _sortAscending) switch
        {
            (ProcessSortColumn.Name,    true)  => filtered.OrderBy(p => p.Name),
            (ProcessSortColumn.Name,    false) => filtered.OrderByDescending(p => p.Name),
            (ProcessSortColumn.Pid,     true)  => filtered.OrderBy(p => p.Pid),
            (ProcessSortColumn.Pid,     false) => filtered.OrderByDescending(p => p.Pid),
            (ProcessSortColumn.Cpu,     true)  => filtered.OrderBy(p => p.CpuPercent),
            (ProcessSortColumn.Cpu,     false) => filtered.OrderByDescending(p => p.CpuPercent),
            (ProcessSortColumn.Memory,  true)  => filtered.OrderBy(p => p.WorkingSetBytes),
            (ProcessSortColumn.Memory,  false) => filtered.OrderByDescending(p => p.WorkingSetBytes),
            (ProcessSortColumn.Disk,    true)  => filtered.OrderBy(p => p.DiskReadBytesPerSec + p.DiskWriteBytesPerSec),
            (ProcessSortColumn.Disk,    false) => filtered.OrderByDescending(p => p.DiskReadBytesPerSec + p.DiskWriteBytesPerSec),
            (ProcessSortColumn.Network, true)  => filtered.OrderBy(p => p.NetSendBytesPerSec + p.NetRecvBytesPerSec),
            (ProcessSortColumn.Network, false) => filtered.OrderByDescending(p => p.NetSendBytesPerSec + p.NetRecvBytesPerSec),
            (ProcessSortColumn.Gpu,     true)  => filtered.OrderBy(p => p.GpuPercent),
            (ProcessSortColumn.Gpu,     false) => filtered.OrderByDescending(p => p.GpuPercent),
            _ => filtered.OrderByDescending(p => p.CpuPercent),
        };

        // Diff-update: avoid full clear/re-add to reduce flicker
        var newList = sorted.ToList();
        for (int i = 0; i < newList.Count; i++)
        {
            if (i < Processes.Count)
            {
                if (Processes[i].Pid != newList[i].Pid || !Equals(Processes[i], newList[i]))
                    Processes[i] = newList[i];
            }
            else { Processes.Add(newList[i]); }
        }
        while (Processes.Count > newList.Count) Processes.RemoveAt(Processes.Count - 1);
    }

    // ── Process control (called from page code-behind) ─────────────────────

    public bool KillProcess(int pid)         => ProcessCollector.Kill(pid);
    public bool SetPriority(int pid, System.Diagnostics.ProcessPriorityClass p)
                                             => ProcessCollector.SetPriority(pid, p);
    public bool SetAffinity(int pid, long mask) => ProcessCollector.SetAffinity(pid, mask);

    public void ToggleSort(ProcessSortColumn column)
    {
        if (SortColumn == column) SortAscending = !SortAscending;
        else { SortColumn = column; SortAscending = false; }
    }

    public void Dispose() => _timer.Dispose();
}
