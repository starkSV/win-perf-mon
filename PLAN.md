# WinPerfMon — Project Plan

## Vision

**Tagline:** "One window. Every metric. Your machine, finally legible."

A single native Windows app that fully replaces five tools:

| Replaced tool | What we take from it |
|---|---|
| **Task Manager** | Process list, per-process CPU/memory/disk/network/GPU, services, startup apps |
| **HWiNFO** | Full sensor tree — every voltage, VRM temp, per-core temp, fan curve, power draw, CSV logging |
| **GPU-Z** | GPU architecture details — shader count, TMUs, ROPs, memory bus, PCIe gen/lanes, BIOS, hot spot |
| **Wireshark** | Per-connection view, protocol breakdown, DNS log, bandwidth per app/protocol |
| **Resource Monitor** | Per-process threads/handles, per-file disk I/O, per-connection remote addresses |

---

## Core Design Decisions

| Decision | Choice | Reason |
|---|---|---|
| UI framework | WinUI 3 (Windows App SDK) | Native Windows, Fluent Design, actively maintained by Microsoft |
| Hardware sensors | LibreHardwareMonitor (LHM) | MIT-licensed C# library, covers CPU/GPU/storage temps + clocks + fans |
| Process data | `NtQuerySystemInformation` + ETW | Full per-process CPU/memory/disk/network without WMI overhead |
| Network connections | `GetExtendedTcpTable` / `GetExtendedUdpTable` | Per-process connection table, no packet capture driver needed |
| Supplemental metrics | Windows PDH | Context switches, IOPS, disk queue |
| Data persistence | SQLite (WAL mode) | Zero-server, open source, survives app restarts |
| Charts | LiveChartsCore.SkiaSharpView.WinUI | Good WinUI 3 support, actively maintained |
| License | MIT | Permissive, clean license chain with LHM |
| Elevation | App runs as admin (v1) | LHM, ETW, and NtQuery all require elevated access |
| Distribution | GitHub Releases + WinGet (later) | Open source standard |

---

## Design System

### Color Palette

```
Background       #0F1117   near-black with slight blue tint
Card surface     #1A1D27   one step lighter
Card border      #2A2D3A   subtle separation
Primary text     #E8EAF0   warm white
Muted labels     #5A6070   secondary info

CPU accent       #3B82F6   blue
GPU accent       #22C55E   green
RAM accent       #A855F7   purple
Network accent   #06B6D4   cyan
Storage accent   #F59E0B   amber

Health OK        #22C55E
Health Warning   #F59E0B
Health Hot       #F97316
Health Critical  #EF4444  + pulse animation
```

### Preset Accent Colors

Each preset has its own identity color applied to the title bar pill + active card borders:

| Preset | Accent | Feel |
|---|---|---|
| Gamer | `#06B6D4` cyan | Electric, high-contrast |
| Developer | `#22C55E` green | Terminal, focused |
| IT / Sysadmin | `#A855F7` purple | Professional, calm |
| Custom | Windows system accent | User's own identity |

### Arc Gauge (hero component)

¾ circle (270°), thick track, filled arc with glowing gradient tip in the component accent color.
At 90%+ the tip color shifts to `#EF4444` and pulses every 2s.
Large bold number in center (tabular/monospace variant), small muted label below.

### "Always Alive" principles

- Sparkline charts scroll leftward every second — new data enters right, old exits left
- Arc fill animates with easing on value change (not instant jump)
- Numbers count smoothly when changing by more than 5 units
- Status bar dot pulses green at idle; goes solid red (stops pulsing) on alert
- Card border gets faint accent glow when metric is elevated

---

## Navigation Structure

```
WinPerfMon
│
├── Dashboard              ← preset-based overview (arc gauges, sparklines, status bar)
│
├── Processes              ← Task Manager replacement
│   ├── All processes      (sortable: CPU / memory / disk / network / GPU)
│   ├── Services           (start / stop / restart)
│   └── Startup            (enable / disable, impact rating)
│
├── Performance            ← Resource Monitor + Task Manager Perf tab
│   ├── CPU                (per-core, NUMA, scheduler, history)
│   ├── Memory             (committed, paged pool, non-paged, working set)
│   ├── GPU                (per-engine breakdown, VRAM, PCIe)
│   ├── Disk               (per-disk + per-file I/O, per-process)
│   └── Network            (per-interface, per-protocol)
│
├── Hardware               ← HWiNFO + GPU-Z replacement
│   ├── System summary     (CPU stepping, BIOS version, RAM XMP profile, uptime)
│   ├── CPU details        (cache topology, microcode, P/E core map)
│   ├── GPU details        (shader count, bus width, VRAM type, hot spot, BIOS)
│   └── Sensor tree        (every voltage, temp, fan — loggable to CSV/JSON)
│
├── Network                ← Wireshark-lite
│   ├── Connections        (per-process → remote IP:port:protocol:state)
│   ├── Bandwidth          (per-app + per-protocol breakdown, live chart)
│   ├── DNS log            (live query log with process attribution)
│   └── Packet inspector   (simplified — v2)
│
└── Storage                ← CrystalDiskInfo + more
    ├── Drive health       (SMART full attribute table, NVMe wear)
    └── I/O monitor        (per-disk, per-file, per-process)
```

### Preset → Default landing

| Preset | Opens on | Dashboard emphasis |
|---|---|---|
| Gamer | Dashboard | GPU hero (large arc), CPU, latency ping |
| Developer | Processes | Process list + CPU arc |
| IT / Sysadmin | Hardware → Sensor tree | SMART, uptime, all temps |
| Custom | Dashboard | User-configured |

Presets never hide navigation — all sections always accessible.
Switching preset animates the dashboard layout in ~300ms.

---

## Architecture

```
┌─────────────────────────────────────────────────────────┐
│  WinPerfMon.Collector  (elevated, runs as admin)         │
│  ┌──────────┐ ┌──────────┐ ┌──────────┐ ┌───────────┐  │
│  │ CpuSensor│ │ GpuSensor│ │ Network  │ │ Storage   │  │
│  │  (LHM)   │ │  (LHM)   │ │ Sensor   │ │ Sensor    │  │
│  └──────────┘ └──────────┘ └──────────┘ └───────────┘  │
│  ┌─────────────────────────────────────────────────┐    │
│  │ ProcessCollector  (NtQuerySystemInformation +   │    │
│  │                    ETW disk/network per-process) │    │
│  └─────────────────────────────────────────────────┘    │
│  ┌─────────────────────────────────────────────────┐    │
│  │ HardwareInspector  (LHM sensor tree + CPUID +   │    │
│  │                     GPU-Z style detail queries)  │    │
│  └─────────────────────────────────────────────────┘    │
│  ┌─────────────────────────────────────────────────┐    │
│  │ NetworkAnalyzer  (GetExtendedTcpTable + ETW +   │    │
│  │                   DNS ETW provider)              │    │
│  └─────────────────────────────────────────────────┘    │
│                    writes every 1s                       │
└──────────────────────────┬──────────────────────────────┘
                           │
                    SQLite (WAL mode)
              %LOCALAPPDATA%\WinPerfMon\metrics.db
                           │
┌──────────────────────────┴──────────────────────────────┐
│  WinPerfMon.App  (WinUI 3)                               │
│  Dashboard → Processes → Performance → Hardware →        │
│             Network → Storage                            │
└──────────────────────────────────────────────────────────┘
```

---

## Tech Stack — NuGet Packages

| Package | Version | Used In |
|---|---|---|
| `LibreHardwareMonitorLib` | 0.9.3 | Collector |
| `Microsoft.Data.Sqlite` | 8.0.10 | Storage |
| `Microsoft.Extensions.Hosting` | 8.0.1 | Collector |
| `Microsoft.Extensions.Hosting.WindowsServices` | 8.0.1 | Collector |
| `Microsoft.WindowsAppSDK` | 1.6.250205002 | App |
| `Microsoft.Windows.SDK.BuildTools` | 10.0.26100.1742 | App |
| `CommunityToolkit.WinUI.UI.Controls` | 7.1.2 | App |
| `LiveChartsCore.SkiaSharpView.WinUI` | 2.0.0-rc3.5 | App |
| `Microsoft.Extensions.DependencyInjection` | 8.0.1 | App |

---

## Build Order / Milestones

### Milestone 1 — Core scaffold ✅
Solution structure, models, SQLite storage, basic sensor collector, WinUI 3 shell.

### Milestone 2 — Processes section (current)
`NtQuerySystemInformation` + ETW for full per-process data.
Sortable DataGrid — CPU / memory / disk / network / GPU columns.
Right-click context menu: kill, set priority, set affinity, open file location.

### Milestone 3 — Dashboard redesign
Arc gauge control (custom WinUI UserControl).
Dark theme design system applied globally.
Preset switcher pill in title bar.
Onboarding screen (first launch).

### Milestone 4 — Hardware / Sensor Tree
LHM full sensor tree rendered as grouped expandable list.
CPU details (CPUID: cache, topology, microcode).
GPU details (shader count, bus width, VRAM type, hot spot).
CSV/JSON sensor logging with configurable interval.

### Milestone 5 — Network section
`GetExtendedTcpTable` connection list (per-process).
ETW-based per-process bandwidth.
DNS query log (ETW `Microsoft-Windows-DNS-Client`).

### Milestone 6 — Storage / Performance depth
Per-file I/O (ETW `Microsoft-Windows-Kernel-FileIO`).
Full SMART attribute table.
Performance section (per-NUMA CPU, committed memory breakdown).

### Milestone 7 — Polish & ship
Settings persistence, alert rules, tray icon, WinGet packaging.
Gaming overlay (detachable mini-HUD).
Export CSV/JSON.

---

## Known Gaps / TODOs

- [ ] ETW per-process network — `NetworkSensor.TopProcesses` always empty
- [ ] NVML wrapper — PCIe bandwidth, GPU hot spot always 0
- [ ] IOPS counters — two-sample PDH delta not yet implemented
- [ ] Context switches/sec — PDH counter not yet wired
- [ ] Startup order — app shows blank for first 1-2s before collector writes first row
- [ ] Settings persistence — SettingsPage UI exists, values not saved yet
- [ ] Tray icon — not implemented
- [ ] Onboarding preset screen — goes straight to CPU page currently
- [ ] Dashboard redesign — current pages are placeholder; full design system not applied yet
