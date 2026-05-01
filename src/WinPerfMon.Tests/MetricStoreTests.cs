using FluentAssertions;
using WinPerfMon.Shared.Models;
using WinPerfMon.Storage;

namespace WinPerfMon.Tests;

public sealed class MetricStoreTests : IDisposable
{
    private readonly string _dbPath;
    private readonly MetricStore _store;

    public MetricStoreTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"wpm_test_{Guid.NewGuid():N}.db");
        _store = new MetricStore(_dbPath);
    }

    // ── CPU ────────────────────────────────────────────────────────────────

    [Fact]
    public void WriteCpu_ThenReadBack_ReturnsSameSnapshot()
    {
        var ts = DateTimeOffset.UtcNow;
        var metric = MakeCpu(ts, totalLoad: 42.5f, packageTemp: 65f);

        _store.WriteCpu(metric);

        var results = _store.ReadCpu(ts.AddSeconds(-1), ts.AddSeconds(1));
        results.Should().HaveCount(1);
        results[0].TotalLoad.Should().BeApproximately(42.5f, 0.01f);
        results[0].PackageTemperature.Should().BeApproximately(65f, 0.01f);
        results[0].Cores.Should().HaveCount(4);
    }

    [Fact]
    public void ReadCpu_OutsideTimeRange_ReturnsEmpty()
    {
        var ts = DateTimeOffset.UtcNow;
        _store.WriteCpu(MakeCpu(ts));

        var results = _store.ReadCpu(ts.AddHours(-2), ts.AddHours(-1));
        results.Should().BeEmpty();
    }

    [Fact]
    public void ReadCpu_MultipleSnapshots_ReturnedInOrder()
    {
        var base_ = DateTimeOffset.UtcNow;
        _store.WriteCpu(MakeCpu(base_,            totalLoad: 10f));
        _store.WriteCpu(MakeCpu(base_.AddSeconds(1), totalLoad: 20f));
        _store.WriteCpu(MakeCpu(base_.AddSeconds(2), totalLoad: 30f));

        var results = _store.ReadCpu(base_.AddSeconds(-1), base_.AddSeconds(3));
        results.Should().HaveCount(3);
        results.Select(r => r.TotalLoad).Should().BeInAscendingOrder();
    }

    // ── GPU ────────────────────────────────────────────────────────────────

    [Fact]
    public void WriteGpu_ThenReadBack_PreservesVendorAndVram()
    {
        var ts = DateTimeOffset.UtcNow;
        var metric = new GpuMetrics
        {
            Timestamp      = ts,
            Name           = "NVIDIA GeForce RTX 4090",
            Vendor         = GpuVendor.Nvidia,
            CoreLoad       = 87f,
            VramUsedBytes  = 8L * 1024 * 1024 * 1024,
            VramTotalBytes = 24L * 1024 * 1024 * 1024,
            TemperatureCelsius = 72f,
            CoreClockMhz   = 2520f,
        };

        _store.WriteGpu(metric);

        var results = _store.ReadGpu(ts.AddSeconds(-1), ts.AddSeconds(1));
        results.Should().HaveCount(1);
        results[0].Vendor.Should().Be(GpuVendor.Nvidia);
        results[0].Name.Should().Be("NVIDIA GeForce RTX 4090");
        results[0].VramUsedBytes.Should().Be(8L * 1024 * 1024 * 1024);
        results[0].CoreLoad.Should().BeApproximately(87f, 0.01f);
    }

    // ── Network ────────────────────────────────────────────────────────────

    [Fact]
    public void WriteNetwork_ThenReadBack_PreservesInterfacesAndRtt()
    {
        var ts = DateTimeOffset.UtcNow;
        var metric = new NetworkMetrics
        {
            Timestamp = ts,
            GatewayRttMs = 3.5f,
            CustomPingRttMs = 14f,
            CustomPingTarget = "8.8.8.8",
            Interfaces =
            [
                new InterfaceStats
                {
                    Name = "Ethernet",
                    Description = "Intel I225-V",
                    DownloadBytesPerSec = 5_000_000,
                    UploadBytesPerSec   = 500_000,
                    IsConnected = true,
                    SpeedBps = 1_000_000_000,
                }
            ],
            TopProcesses = [],
        };

        _store.WriteNetwork(metric);

        var results = _store.ReadNetwork(ts.AddSeconds(-1), ts.AddSeconds(1));
        results.Should().HaveCount(1);
        results[0].GatewayRttMs.Should().BeApproximately(3.5f, 0.01f);
        results[0].CustomPingTarget.Should().Be("8.8.8.8");
        results[0].Interfaces.Should().HaveCount(1);
        results[0].Interfaces[0].Name.Should().Be("Ethernet");
        results[0].Interfaces[0].DownloadBytesPerSec.Should().Be(5_000_000);
    }

    // ── Storage ────────────────────────────────────────────────────────────

    [Fact]
    public void WriteStorage_ThenReadBack_PreservesHealthAndNvme()
    {
        var ts = DateTimeOffset.UtcNow;
        var metric = new StorageMetrics
        {
            Timestamp = ts,
            Disks =
            [
                new DiskStats
                {
                    DeviceName = @"\\.\PhysicalDrive0",
                    Model = "Samsung 990 Pro 2TB",
                    Type  = DiskType.Nvme,
                    ReadBytesPerSec  = 3_000_000_000L,
                    WriteBytesPerSec = 2_500_000_000L,
                    QueueDepth = 1.2f,
                    ActiveTimePercent = 23f,
                    Health = SmartHealth.Healthy,
                    TemperatureCelsius = 41f,
                    SmartAttributes = [],
                    Nvme = new NvmeStats
                    {
                        PercentageUsed = 3,
                        DataUnitsWritten = 500_000,
                        MediaErrors = 0,
                        PowerOnHours = 2400,
                    },
                }
            ],
        };

        _store.WriteStorage(metric);

        var results = _store.ReadStorage(ts.AddSeconds(-1), ts.AddSeconds(1));
        results.Should().HaveCount(1);
        results[0].Disks.Should().HaveCount(1);
        results[0].Disks[0].Health.Should().Be(SmartHealth.Healthy);
        results[0].Disks[0].Type.Should().Be(DiskType.Nvme);
        results[0].Disks[0].Nvme.Should().NotBeNull();
        results[0].Disks[0].Nvme!.PercentageUsed.Should().Be(3);
        results[0].Disks[0].ReadBytesPerSec.Should().Be(3_000_000_000L);
    }

    // ── Prune ──────────────────────────────────────────────────────────────

    [Fact]
    public void Prune_RemovesSnapshotsOlderThanRetention()
    {
        // store with 1-second retention for testing
        using var shortStore = new MetricStore(_dbPath + ".prune", TimeSpan.FromSeconds(1));

        var old = DateTimeOffset.UtcNow.AddSeconds(-10);
        var recent = DateTimeOffset.UtcNow;

        shortStore.WriteCpu(MakeCpu(old,    totalLoad: 1f));
        shortStore.WriteCpu(MakeCpu(recent, totalLoad: 2f));

        shortStore.Prune();

        var all = shortStore.ReadCpu(old.AddSeconds(-1), recent.AddSeconds(1));
        all.Should().HaveCount(1);
        all[0].TotalLoad.Should().BeApproximately(2f, 0.01f);
    }

    [Fact]
    public void Prune_WhenAllDataFresh_RemovesNothing()
    {
        var ts = DateTimeOffset.UtcNow;
        _store.WriteCpu(MakeCpu(ts));
        _store.WriteCpu(MakeCpu(ts.AddSeconds(1)));

        _store.Prune(); // default 7-day retention — nothing should be removed

        var results = _store.ReadCpu(ts.AddSeconds(-1), ts.AddSeconds(2));
        results.Should().HaveCount(2);
    }

    // ── SMART model ────────────────────────────────────────────────────────

    [Fact]
    public void SmartAttribute_IsFailing_WhenCurrentBelowThreshold()
    {
        var attr = new SmartAttribute
        {
            Id = 5,
            Name = "Reallocated Sectors Count",
            RawValue = 10,
            CurrentValue = 90,
            ThresholdValue = 140,
        };

        attr.IsFailing.Should().BeTrue();
    }

    [Fact]
    public void SmartAttribute_IsNotFailing_WhenCurrentAboveThreshold()
    {
        var attr = new SmartAttribute
        {
            Id = 5,
            Name = "Reallocated Sectors Count",
            RawValue = 0,
            CurrentValue = 200,
            ThresholdValue = 140,
        };

        attr.IsFailing.Should().BeFalse();
    }

    // ── Helpers ────────────────────────────────────────────────────────────

    private static CpuMetrics MakeCpu(DateTimeOffset ts, float totalLoad = 0f, float packageTemp = 0f) =>
        new()
        {
            Timestamp = ts,
            TotalLoad = totalLoad,
            PackageTemperature = packageTemp,
            IsThermallyThrottled = false,
            ProcessCount = 120,
            ThreadCount = 1800,
            ContextSwitchesPerSec = 0,
            Cores =
            [
                new CoreMetrics { Index = 0, Load = 10f, FrequencyMhz = 3600f },
                new CoreMetrics { Index = 1, Load = 20f, FrequencyMhz = 3600f },
                new CoreMetrics { Index = 2, Load = 30f, FrequencyMhz = 3400f },
                new CoreMetrics { Index = 3, Load = 40f, FrequencyMhz = 3400f },
            ],
        };

    public void Dispose()
    {
        _store.Dispose();
        foreach (var f in Directory.GetFiles(Path.GetTempPath(), "wpm_test_*"))
            try { File.Delete(f); } catch { /* best-effort cleanup */ }
    }
}
