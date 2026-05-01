using System.Net.NetworkInformation;
using System.Diagnostics;
using WinPerfMon.Shared.Models;

namespace WinPerfMon.Collector.Sensors;

/// <summary>
/// Samples per-interface bandwidth via NetworkInterface delta and pings the gateway.
/// Per-process network stats require ETW (added in a follow-up).
/// </summary>
public sealed class NetworkSensor
{
    private Dictionary<string, long> _prevRx = new();
    private Dictionary<string, long> _prevTx = new();
    private DateTimeOffset _prevTime = DateTimeOffset.UtcNow;
    private string? _customPingTarget;

    public void SetCustomPingTarget(string? hostname) => _customPingTarget = hostname;

    public NetworkMetrics Sample()
    {
        var now = DateTimeOffset.UtcNow;
        double elapsedSec = (now - _prevTime).TotalSeconds;
        if (elapsedSec < 0.01) elapsedSec = 1;

        var interfaces = new List<InterfaceStats>();
        var rxNow = new Dictionary<string, long>();
        var txNow = new Dictionary<string, long>();

        foreach (var nic in NetworkInterface.GetAllNetworkInterfaces())
        {
            if (nic.NetworkInterfaceType == NetworkInterfaceType.Loopback) continue;
            var stats = nic.GetIPStatistics();
            rxNow[nic.Id] = stats.BytesReceived;
            txNow[nic.Id] = stats.BytesSent;

            long dRx = _prevRx.TryGetValue(nic.Id, out long pRx) ? Math.Max(0, stats.BytesReceived - pRx) : 0;
            long dTx = _prevTx.TryGetValue(nic.Id, out long pTx) ? Math.Max(0, stats.BytesSent - pTx) : 0;

            interfaces.Add(new InterfaceStats
            {
                Name = nic.Name,
                Description = nic.Description,
                DownloadBytesPerSec = (long)(dRx / elapsedSec),
                UploadBytesPerSec   = (long)(dTx / elapsedSec),
                TotalDownloadBytes = stats.BytesReceived,
                TotalUploadBytes   = stats.BytesSent,
                IsConnected = nic.OperationalStatus == OperationalStatus.Up,
                SpeedBps = nic.Speed,
            });
        }

        _prevRx = rxNow;
        _prevTx = txNow;
        _prevTime = now;

        float gatewayRtt = PingGateway();
        float customRtt = _customPingTarget is not null ? PingHost(_customPingTarget) : 0f;

        return new NetworkMetrics
        {
            Timestamp = now,
            Interfaces = [.. interfaces],
            TopProcesses = [],          // ETW-based per-process data: follow-up
            GatewayRttMs = gatewayRtt,
            CustomPingRttMs = customRtt,
            CustomPingTarget = _customPingTarget,
        };
    }

    private static float PingGateway()
    {
        try
        {
            var gateway = NetworkInterface.GetAllNetworkInterfaces()
                .Where(n => n.OperationalStatus == OperationalStatus.Up)
                .SelectMany(n => n.GetIPProperties().GatewayAddresses)
                .Select(g => g.Address.ToString())
                .FirstOrDefault(a => !string.IsNullOrEmpty(a));

            if (gateway is null) return 0f;
            return PingHost(gateway);
        }
        catch { return 0f; }
    }

    private static float PingHost(string host)
    {
        try
        {
            using var ping = new Ping();
            var reply = ping.Send(host, 1000);
            return reply.Status == IPStatus.Success ? reply.RoundtripTime : -1f;
        }
        catch { return -1f; }
    }
}
