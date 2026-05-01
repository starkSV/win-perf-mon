using System.Diagnostics;
using LibreHardwareMonitor.Hardware;
using WinPerfMon.Shared.Models;

namespace WinPerfMon.Collector.Sensors;

/// <summary>
/// Collects per-disk I/O counters via PDH PerformanceCounter and SMART data via LHM.
/// </summary>
public sealed class StorageSensor : IDisposable
{
    private readonly Computer _computer;
    private readonly IHardware[] _drives;
    private readonly Dictionary<string, (PerformanceCounter read, PerformanceCounter write, PerformanceCounter queue)> _counters = new();

    public StorageSensor()
    {
        _computer = new Computer { IsStorageEnabled = true };
        _computer.Open();
        _drives = [.. _computer.Hardware.Where(h => h.HardwareType == HardwareType.Storage)];

        InitCounters();
    }

    private void InitCounters()
    {
        try
        {
            var category = new PerformanceCounterCategory("PhysicalDisk");
            foreach (var instance in category.GetInstanceNames().Where(n => n != "_Total"))
            {
                _counters[instance] = (
                    new PerformanceCounter("PhysicalDisk", "Disk Read Bytes/sec", instance),
                    new PerformanceCounter("PhysicalDisk", "Disk Write Bytes/sec", instance),
                    new PerformanceCounter("PhysicalDisk", "Current Disk Queue Length", instance)
                );
            }
        }
        catch { /* PDH not available in some contexts */ }
    }

    public StorageMetrics Sample()
    {
        foreach (var d in _drives) d.Update();

        var disks = new List<DiskStats>();

        foreach (var hw in _drives)
        {
            var (readBps, writeBps, queue) = GetCounters(hw.Name);

            disks.Add(new DiskStats
            {
                DeviceName = hw.Identifier.ToString(),
                Model = hw.Name,
                Type = DetectType(hw),
                ReadBytesPerSec  = readBps,
                WriteBytesPerSec = writeBps,
                ReadIops  = 0f,  // IOPS requires two-point delta on reads/writes counter
                WriteIops = 0f,
                QueueDepth = queue,
                ActiveTimePercent = GetSensor(hw, SensorType.Load, "Used Space") ?? 0f,
                Health = DetermineHealth(hw),
                SmartAttributes = GetSmartAttributes(hw),
                TemperatureCelsius = GetSensor(hw, SensorType.Temperature, "Temperature"),
                Nvme = GetNvmeStats(hw),
            });
        }

        return new StorageMetrics
        {
            Timestamp = DateTimeOffset.UtcNow,
            Disks = [.. disks],
        };
    }

    private (long readBps, long writeBps, float queue) GetCounters(string hwName)
    {
        // Match LHM drive name to PDH instance (best-effort substring match)
        var key = _counters.Keys.FirstOrDefault(k =>
            hwName.Contains(k.Split(' ').LastOrDefault() ?? "", StringComparison.OrdinalIgnoreCase));
        if (key is null) return (0, 0, 0f);
        try
        {
            return (
                (long)_counters[key].read.NextValue(),
                (long)_counters[key].write.NextValue(),
                _counters[key].queue.NextValue()
            );
        }
        catch { return (0, 0, 0f); }
    }

    private static DiskType DetectType(IHardware hw)
    {
        var name = hw.Name.ToUpperInvariant();
        if (name.Contains("NVME") || name.Contains("NVM")) return DiskType.Nvme;
        if (name.Contains("SSD") || name.Contains("SATA")) return DiskType.Sata;
        return DiskType.Hdd;
    }

    private static SmartHealth DetermineHealth(IHardware hw)
    {
        var lifeLeft = GetSensor(hw, SensorType.Level, "Remaining Life");
        if (lifeLeft.HasValue)
            return lifeLeft.Value < 10 ? SmartHealth.Failing : lifeLeft.Value < 30 ? SmartHealth.Warning : SmartHealth.Healthy;
        return SmartHealth.Unknown;
    }

    private static SmartAttribute[] GetSmartAttributes(IHardware hw)
        => [.. hw.Sensors
            .Where(s => s.SensorType == SensorType.RawValue)
            .Select(s => new SmartAttribute
            {
                Id = 0, // LHM doesn't expose raw SMART IDs directly in this path
                Name = s.Name,
                RawValue = (long)(s.Value ?? 0),
                CurrentValue = 0,
                ThresholdValue = 0,
            })];

    private static NvmeStats? GetNvmeStats(IHardware hw)
    {
        var percentUsed = GetSensor(hw, SensorType.Level, "Percentage Used");
        if (!percentUsed.HasValue) return null;
        return new NvmeStats
        {
            PercentageUsed = (byte)percentUsed.Value,
            DataUnitsWritten = (long)(GetSensor(hw, SensorType.Data, "Total Bytes Written") ?? 0),
            DataUnitsRead = 0,
            MediaErrors = 0,
            PowerOnHours = (long)(GetSensor(hw, SensorType.RawValue, "Power On Hours") ?? 0),
        };
    }

    private static float? GetSensor(IHardware hw, SensorType type, string name)
        => hw.Sensors.FirstOrDefault(s => s.SensorType == type && s.Name == name)?.Value;

    public void Dispose()
    {
        _computer.Close();
        foreach (var (r, w, q) in _counters.Values) { r.Dispose(); w.Dispose(); q.Dispose(); }
    }
}
