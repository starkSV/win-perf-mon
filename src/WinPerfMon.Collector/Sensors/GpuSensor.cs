using LibreHardwareMonitor.Hardware;
using WinPerfMon.Shared.Models;

namespace WinPerfMon.Collector.Sensors;

public sealed class GpuSensor : IDisposable
{
    private readonly Computer _computer;
    private readonly IHardware[] _gpus;

    public GpuSensor()
    {
        _computer = new Computer { IsGpuEnabled = true };
        _computer.Open();
        _gpus = [.. _computer.Hardware.Where(h =>
            h.HardwareType is HardwareType.GpuNvidia or HardwareType.GpuAmd or HardwareType.GpuIntel)];
    }

    public GpuMetrics[] Sample()
    {
        foreach (var gpu in _gpus) gpu.Update();

        return [.. _gpus.Select(gpu => new GpuMetrics
        {
            Timestamp = DateTimeOffset.UtcNow,
            Name = gpu.Name,
            Vendor = gpu.HardwareType switch
            {
                HardwareType.GpuNvidia => GpuVendor.Nvidia,
                HardwareType.GpuAmd    => GpuVendor.Amd,
                HardwareType.GpuIntel  => GpuVendor.Intel,
                _                      => GpuVendor.Unknown,
            },
            CoreLoad        = GetSensor(gpu, SensorType.Load, "GPU Core")          ?? 0f,
            ComputeLoad     = GetSensor(gpu, SensorType.Load, "GPU Video Engine")  ?? 0f,
            VideoDecodeLoad = GetSensor(gpu, SensorType.Load, "GPU Video Decode")  ?? 0f,
            VideoEncodeLoad = GetSensor(gpu, SensorType.Load, "GPU Video Encode")  ?? 0f,

            VramUsedBytes  = (long)((GetSensor(gpu, SensorType.SmallData, "GPU Memory Used") ?? 0f) * 1024 * 1024),
            VramTotalBytes = (long)((GetSensor(gpu, SensorType.SmallData, "GPU Memory Total") ?? 0f) * 1024 * 1024),

            CoreClockMhz   = GetSensor(gpu, SensorType.Clock, "GPU Core")   ?? 0f,
            MemoryClockMhz = GetSensor(gpu, SensorType.Clock, "GPU Memory") ?? 0f,

            TemperatureCelsius = GetSensor(gpu, SensorType.Temperature, "GPU Core") ?? 0f,
            FanRpm             = GetSensor(gpu, SensorType.Fan, "GPU Fan")          ?? 0f,

            PcieBandwidthMbps = 0f, // requires NVML for NVIDIA; leave 0 until NVML wrapper is added
            PcieGeneration = 0,
            PcieLanes = 0,
        })];
    }

    private static float? GetSensor(IHardware hw, SensorType type, string name)
        => hw.Sensors.FirstOrDefault(s => s.SensorType == type && s.Name == name)?.Value;

    public void Dispose() => _computer.Close();
}
