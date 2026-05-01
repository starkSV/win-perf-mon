using System.Collections.ObjectModel;
using Microsoft.UI.Dispatching;
using WinPerfMon.Shared.Models;
using WinPerfMon.Storage;

namespace WinPerfMon.App.ViewModels;

public sealed class GpuViewModel : BaseViewModel, IDisposable
{
    private readonly MetricStore _store;
    private readonly DispatcherQueue _dispatcher;
    private readonly Timer _timer;

    private float _coreLoad;
    private float _vramUsedGb;
    private float _vramTotalGb;
    private float _temp;
    private float _coreClock;
    private string _gpuName = "—";

    public float  CoreLoad     { get => _coreLoad;    private set => Set(ref _coreLoad, value); }
    public float  VramUsedGb   { get => _vramUsedGb;  private set => Set(ref _vramUsedGb, value); }
    public float  VramTotalGb  { get => _vramTotalGb; private set => Set(ref _vramTotalGb, value); }
    public float  Temperature  { get => _temp;        private set => Set(ref _temp, value); }
    public float  CoreClock    { get => _coreClock;   private set => Set(ref _coreClock, value); }
    public string GpuName      { get => _gpuName;     private set => Set(ref _gpuName, value); }

    public ObservableCollection<float> CoreLoadHistory { get; } = [];
    private const int HistoryLength = 60;

    public GpuViewModel(MetricStore store)
    {
        _store = store;
        _dispatcher = DispatcherQueue.GetForCurrentThread();
        _timer = new Timer(Refresh, null, TimeSpan.Zero, TimeSpan.FromSeconds(1));
    }

    private void Refresh(object? _)
    {
        var now = DateTimeOffset.UtcNow;
        var snapshots = _store.ReadGpu(now.AddSeconds(-2), now);
        if (snapshots.Count == 0) return;

        var latest = snapshots[^1];
        _dispatcher.TryEnqueue(() =>
        {
            GpuName     = latest.Name;
            CoreLoad    = latest.CoreLoad;
            VramUsedGb  = latest.VramUsedBytes  / 1024f / 1024f / 1024f;
            VramTotalGb = latest.VramTotalBytes / 1024f / 1024f / 1024f;
            Temperature = latest.TemperatureCelsius;
            CoreClock   = latest.CoreClockMhz;

            CoreLoadHistory.Add(latest.CoreLoad);
            while (CoreLoadHistory.Count > HistoryLength) CoreLoadHistory.RemoveAt(0);
        });
    }

    public void Dispose() => _timer.Dispose();
}
