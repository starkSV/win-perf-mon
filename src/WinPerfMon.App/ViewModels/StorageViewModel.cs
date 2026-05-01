using System.Collections.ObjectModel;
using Microsoft.UI.Dispatching;
using WinPerfMon.Shared.Models;
using WinPerfMon.Storage;

namespace WinPerfMon.App.ViewModels;

public sealed class StorageViewModel : BaseViewModel, IDisposable
{
    private readonly MetricStore _store;
    private readonly DispatcherQueue _dispatcher;
    private readonly Timer _timer;

    public ObservableCollection<DiskStats> Disks { get; } = [];

    public StorageViewModel(MetricStore store)
    {
        _store = store;
        _dispatcher = DispatcherQueue.GetForCurrentThread();
        _timer = new Timer(Refresh, null, TimeSpan.Zero, TimeSpan.FromSeconds(2));
    }

    private void Refresh(object? _)
    {
        var now = DateTimeOffset.UtcNow;
        var snapshots = _store.ReadStorage(now.AddSeconds(-3), now);
        if (snapshots.Count == 0) return;

        var latest = snapshots[^1];
        _dispatcher.TryEnqueue(() =>
        {
            Disks.Clear();
            foreach (var d in latest.Disks) Disks.Add(d);
        });
    }

    public void Dispose() => _timer.Dispose();
}
