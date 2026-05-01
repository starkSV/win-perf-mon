using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using WinPerfMon.Collector.Sensors;
using WinPerfMon.Storage;

namespace WinPerfMon.Collector;

/// <summary>
/// Background worker that polls all sensors on a fixed interval and writes to the MetricStore.
/// Target: &lt;0.5% CPU overhead on an idle system with a 1-second poll interval.
/// </summary>
public sealed class CollectorWorker : BackgroundService
{
    private readonly MetricStore _store;
    private readonly ILogger<CollectorWorker> _log;
    private readonly TimeSpan _interval;

    public CollectorWorker(MetricStore store, ILogger<CollectorWorker> log)
    {
        _store = store;
        _log = log;
        _interval = TimeSpan.FromSeconds(1);
    }

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        _log.LogInformation("Collector starting (interval {Interval}s)", _interval.TotalSeconds);

        using var cpu = new CpuSensor();
        using var gpu = new GpuSensor();
        var network = new NetworkSensor();
        using var storage = new StorageSensor();

        var pruneTimer = DateTime.UtcNow;

        while (!ct.IsCancellationRequested)
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            try
            {
                // Run all sensor reads concurrently to keep total time under 200ms
                var cpuTask     = Task.Run(cpu.Sample, ct);
                var gpuTask     = Task.Run(gpu.Sample, ct);
                var networkTask = Task.Run(network.Sample, ct);
                var storageTask = Task.Run(storage.Sample, ct);

                await Task.WhenAll(cpuTask, gpuTask, networkTask, storageTask);

                _store.WriteCpu(cpuTask.Result);
                foreach (var g in gpuTask.Result) _store.WriteGpu(g);
                _store.WriteNetwork(networkTask.Result);
                _store.WriteStorage(storageTask.Result);

                // Prune old data every hour
                if (DateTime.UtcNow - pruneTimer > TimeSpan.FromHours(1))
                {
                    _store.Prune();
                    pruneTimer = DateTime.UtcNow;
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _log.LogError(ex, "Sensor read failed");
            }

            var elapsed = sw.Elapsed;
            var delay = _interval - elapsed;
            if (delay > TimeSpan.Zero)
                await Task.Delay(delay, ct);
        }

        _log.LogInformation("Collector stopped");
    }
}
