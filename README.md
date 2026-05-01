# WinPerfMon

> One window. Every metric. Your machine, finally legible.

A native Windows performance monitor that replaces five tools in one — built with WinUI 3, dark-first, and always alive.

![Build](https://github.com/starkSV/win-perf-mon/actions/workflows/build.yml/badge.svg)
![License](https://img.shields.io/badge/license-MIT-blue.svg)
![Platform](https://img.shields.io/badge/platform-Windows%2010%2022H2%20%7C%20Windows%2011-0078d4)

---

## What it replaces

| Tool | What WinPerfMon covers |
|---|---|
| **Task Manager** | Process list with per-process CPU / memory / disk / network / GPU, kill, priority, affinity |
| **HWiNFO** | Full sensor tree — every temp, voltage, fan RPM, power draw, loggable to CSV |
| **GPU-Z** | GPU architecture details — shaders, memory bus, PCIe gen, hot spot, VRAM temp |
| **Wireshark** | Per-process connections, protocol breakdown, DNS query log, bandwidth per app |
| **Resource Monitor** | Per-process threads / handles, per-file disk I/O, per-connection remote addresses |

---

## Features

- **Processes** — sortable live process table, End Task, Set Priority, Set Affinity, Open file location
- **Dashboard** — arc gauge overview with CPU, GPU, RAM, Network, Storage at a glance
- **Hardware** — full LHM sensor tree, CPU topology, GPU architecture details
- **Network** — connection list, per-app bandwidth, DNS log
- **Storage** — SMART health, NVMe wear, per-disk I/O monitor
- **Onboarding presets** — Gamer / Developer / IT+Sysadmin / Custom, switchable any time
- **Dark-first design** — designed for dark, tested for light; accent color shifts per preset
- **Always alive** — charts scroll in real time, arc gauges animate, critical metrics pulse red
- **History** — SQLite ring buffer, configurable retention up to 30 days

---

## Design

Dark-first, native Windows aesthetic. One accent color per subsystem:

| Subsystem | Color |
|---|---|
| CPU | Blue `#3B82F6` |
| GPU | Green `#22C55E` |
| RAM | Purple `#A855F7` |
| Network | Cyan `#06B6D4` |
| Storage | Amber `#F59E0B` |

Temperature and load use a consistent health scale across all panels:
`neutral → amber (60%) → orange (80%) → red + pulse (90%+)`

---

## Architecture

```
WinPerfMon.Collector  (elevated, runs as admin)
  └─ LHM sensors + NtQuerySystemInformation + PDH + NetworkInterface
  └─ writes to SQLite every 1s

WinPerfMon.App  (WinUI 3)
  └─ reads live + history from SQLite
  └─ Dashboard → Processes → Hardware → Network → Storage
```

### Projects

| Project | Purpose |
|---|---|
| `WinPerfMon.Shared` | Metric record types shared across all projects |
| `WinPerfMon.Storage` | SQLite ring buffer (`MetricStore`) |
| `WinPerfMon.Collector` | Background sensor polling + process collection |
| `WinPerfMon.App` | WinUI 3 frontend |
| `WinPerfMon.Tests` | xUnit tests (cross-platform, runs on macOS/Linux/Windows) |

---

## Requirements

- Windows 10 22H2 or Windows 11
- x64 only
- Must run as administrator (required for hardware sensors and ETW)

---

## Building

**Prerequisites:** Visual Studio 2022 with the following workloads:
- .NET desktop development
- Windows application development (Windows App SDK)

```bash
git clone https://github.com/starkSV/win-perf-mon.git
cd win-perf-mon
```

Open `WinPerfMon.sln`, set platform to **x64**, press **F5**.

### Running tests (macOS / Linux / Windows)

```bash
dotnet test src/WinPerfMon.Tests/WinPerfMon.Tests.csproj
```

---

## Roadmap

- [x] Solution scaffold — 5 projects, CI, shared models
- [x] Collector — CPU, GPU, network, storage sensors
- [x] Processes — full Task Manager replacement
- [ ] Dashboard redesign — arc gauges, dark design system, onboarding
- [ ] Hardware section — HWiNFO-style sensor tree + GPU-Z details
- [ ] Network section — connection list, DNS log, per-app bandwidth
- [ ] Storage depth — full SMART table, per-file I/O
- [ ] Polish — alerts, tray icon, gaming overlay, WinGet packaging

---

## Tech Stack

- [WinUI 3 / Windows App SDK](https://learn.microsoft.com/en-us/windows/apps/winui/winui3/)
- [LibreHardwareMonitor](https://github.com/LibreHardwareMonitor/LibreHardwareMonitor) — MIT
- [LiveChartsCore](https://livecharts.dev/) — MIT
- [Microsoft.Data.Sqlite](https://learn.microsoft.com/en-us/dotnet/standard/data/sqlite/) — MIT
- [xUnit](https://xunit.net/) — Apache 2.0

---

## License

[MIT](LICENSE) — free to use, fork, and build on.
