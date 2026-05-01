using LibreHardwareMonitor.Hardware;
using WinPerfMon.Shared.Models;

namespace WinPerfMon.Collector.Sensors;

public sealed class CpuSensor : IDisposable
{
    private readonly Computer _computer;
    private readonly IHardware[] _cpus;

    public CpuSensor()
    {
        _computer = new Computer
        {
            IsCpuEnabled = true,
            IsMotherboardEnabled = true,
        };
        _computer.Open();
        _computer.Accept(new UpdateVisitor());
        _cpus = [.. _computer.Hardware.Where(h => h.HardwareType == HardwareType.Cpu)];
    }

    public CpuMetrics Sample()
    {
        foreach (var cpu in _cpus)
        {
            cpu.Update();
            foreach (var sub in cpu.SubHardware) sub.Update();
        }

        var cores = new List<CoreMetrics>();
        float totalLoad = 0f;
        float packageTemp = 0f;
        bool throttled = false;

        foreach (var cpu in _cpus)
        {
            foreach (var sensor in cpu.Sensors)
            {
                switch (sensor.SensorType)
                {
                    case SensorType.Load when sensor.Name == "CPU Total":
                        totalLoad = sensor.Value ?? 0f;
                        break;

                    case SensorType.Temperature when sensor.Name.Contains("Package"):
                        packageTemp = sensor.Value ?? 0f;
                        break;

                    case SensorType.Load when sensor.Name.StartsWith("CPU Core #"):
                        if (int.TryParse(sensor.Name.Replace("CPU Core #", ""), out int idx))
                        {
                            var load = sensor.Value ?? 0f;
                            var freq = GetCoreFreq(cpu, idx);
                            var temp = GetCoreTemp(cpu, idx);
                            cores.Add(new CoreMetrics
                            {
                                Index = idx - 1,
                                Load = load,
                                FrequencyMhz = freq,
                                TemperatureCelsius = temp,
                                IsEfficiencyCore = IsECore(cpu, idx),
                            });
                        }
                        break;
                }
            }
        }

        return new CpuMetrics
        {
            Timestamp = DateTimeOffset.UtcNow,
            TotalLoad = totalLoad,
            PackageTemperature = packageTemp,
            IsThermallyThrottled = throttled,
            Cores = [.. cores.OrderBy(c => c.Index)],
            ProcessCount = GetProcessCount(),
            ThreadCount = GetThreadCount(),
            ContextSwitchesPerSec = 0, // populated via PDH in a future pass
        };
    }

    private static float GetCoreFreq(IHardware cpu, int coreIdx)
        => cpu.Sensors
            .FirstOrDefault(s => s.SensorType == SensorType.Clock && s.Name == $"CPU Core #{coreIdx}")
            ?.Value ?? 0f;

    private static float GetCoreTemp(IHardware cpu, int coreIdx)
        => cpu.Sensors
            .FirstOrDefault(s => s.SensorType == SensorType.Temperature && s.Name == $"CPU Core #{coreIdx}")
            ?.Value ?? 0f;

    private static bool IsECore(IHardware cpu, int coreIdx)
    {
        // Intel 12th gen+ reports efficiency cores with "(E)" suffix in some LHM builds
        var name = cpu.Sensors
            .FirstOrDefault(s => s.SensorType == SensorType.Load && s.Name == $"CPU Core #{coreIdx}")
            ?.Name ?? "";
        return name.Contains("(E)", StringComparison.OrdinalIgnoreCase);
    }

    private static int GetProcessCount()
    {
        try { return System.Diagnostics.Process.GetProcesses().Length; }
        catch { return 0; }
    }

    private static int GetThreadCount()
    {
        try { return System.Diagnostics.Process.GetProcesses().Sum(p => { try { return p.Threads.Count; } catch { return 0; } }); }
        catch { return 0; }
    }

    public void Dispose() => _computer.Close();
}

internal sealed class UpdateVisitor : IVisitor
{
    public void VisitComputer(IComputer computer) { computer.Traverse(this); }
    public void VisitHardware(IHardware hardware) { hardware.Update(); hardware.Traverse(this); }
    public void VisitParameter(IParameter parameter) { }
    public void VisitSensor(ISensor sensor) { }
}
