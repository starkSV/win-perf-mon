using System.Collections.ObjectModel;
using Microsoft.UI.Dispatching;
using WinPerfMon.Shared.Models;
using WinPerfMon.Storage;

namespace WinPerfMon.App.ViewModels;

public sealed class CpuViewModel : BaseViewModel, IDisposable
{
    private readonly MetricStore _store;
    private readonly DispatcherQueue _dispatcher;
    private readonly Timer _timer;

    private float _totalLoad;
    private float _packageTemp;
    private bool _throttled;
    private int _processCount;
    private int _threadCount;

    public float TotalLoad         { get => _totalLoad;      private set => Set(ref _totalLoad, value); }
    public float PackageTemp       { get => _packageTemp;    private set => Set(ref _packageTemp, value); }
    public bool  IsThrottled       { get => _throttled;      private set => Set(ref _throttled, value); }
    public int   ProcessCount      { get => _processCount;   private set => Set(ref _processCount, value); }
    public int   ThreadCount       { get => _threadCount;    private set => Set(ref _threadCount, value); }

    public ObservableCollection<CoreMetrics> Cores { get; } = [];
    public ObservableCollection<float> TotalLoadHistory { get; } = [];

    private const int HistoryLength = 60; // 60 seconds of live data

    public CpuViewModel(MetricStore store)
    {
        _store = store;
        _dispatcher = DispatcherQueue.GetForCurrentThread();
        _timer = new Timer(Refresh, null, TimeSpan.Zero, TimeSpan.FromSeconds(1));
    }

    private void Refresh(object? _)
    {
        var now = DateTimeOffset.UtcNow;
        var snapshots = _store.ReadCpu(now.AddSeconds(-2), now);
        if (snapshots.Count == 0) return;

        var latest = snapshots[^1];
        _dispatcher.TryEnqueue(() =>
        {
            TotalLoad    = latest.TotalLoad;
            PackageTemp  = latest.PackageTemperature;
            IsThrottled  = latest.IsThermallyThrottled;
            ProcessCount = latest.ProcessCount;
            ThreadCount  = latest.ThreadCount;

            Cores.Clear();
            foreach (var c in latest.Cores) Cores.Add(c);

            TotalLoadHistory.Add(latest.TotalLoad);
            while (TotalLoadHistory.Count > HistoryLength) TotalLoadHistory.RemoveAt(0);
        });
    }

    public void Dispose() => _timer.Dispose();
}
