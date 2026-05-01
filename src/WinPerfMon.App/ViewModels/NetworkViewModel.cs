using System.Collections.ObjectModel;
using Microsoft.UI.Dispatching;
using WinPerfMon.Shared.Models;
using WinPerfMon.Storage;

namespace WinPerfMon.App.ViewModels;

public sealed class NetworkViewModel : BaseViewModel, IDisposable
{
    private readonly MetricStore _store;
    private readonly DispatcherQueue _dispatcher;
    private readonly Timer _timer;

    private float _gatewayRtt;
    private float _customRtt;

    public float GatewayRttMs { get => _gatewayRtt; private set => Set(ref _gatewayRtt, value); }
    public float CustomRttMs  { get => _customRtt;  private set => Set(ref _customRtt, value); }

    public ObservableCollection<InterfaceStats>      Interfaces  { get; } = [];
    public ObservableCollection<ProcessNetworkStats> TopTalkers  { get; } = [];
    public ObservableCollection<float>               DownHistory { get; } = [];
    public ObservableCollection<float>               UpHistory   { get; } = [];

    private const int HistoryLength = 60;

    public NetworkViewModel(MetricStore store)
    {
        _store = store;
        _dispatcher = DispatcherQueue.GetForCurrentThread();
        _timer = new Timer(Refresh, null, TimeSpan.Zero, TimeSpan.FromSeconds(1));
    }

    private void Refresh(object? _)
    {
        var now = DateTimeOffset.UtcNow;
        var snapshots = _store.ReadNetwork(now.AddSeconds(-2), now);
        if (snapshots.Count == 0) return;

        var latest = snapshots[^1];
        _dispatcher.TryEnqueue(() =>
        {
            GatewayRttMs = latest.GatewayRttMs;
            CustomRttMs  = latest.CustomPingRttMs;

            Interfaces.Clear();
            foreach (var i in latest.Interfaces) Interfaces.Add(i);

            TopTalkers.Clear();
            foreach (var p in latest.TopProcesses.Take(10)) TopTalkers.Add(p);

            long totalDown = latest.Interfaces.Sum(i => i.DownloadBytesPerSec);
            long totalUp   = latest.Interfaces.Sum(i => i.UploadBytesPerSec);

            DownHistory.Add(totalDown / 1024f / 1024f); // MB/s
            UpHistory.Add(totalUp / 1024f / 1024f);
            while (DownHistory.Count > HistoryLength) { DownHistory.RemoveAt(0); UpHistory.RemoveAt(0); }
        });
    }

    public void Dispose() => _timer.Dispose();
}
