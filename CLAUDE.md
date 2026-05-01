# CLAUDE.md — WinPerfMon Dev Handoff

This file keeps Claude and the developer in sync across machines and sessions.
**Always read this file at the start of a new session before touching any code.**
Update the checklist as work completes.

---

## Project Summary

All-in-one Windows performance monitor replacing Task Manager + HWiNFO + GPU-Z + Wireshark + Resource Monitor.
Single WinUI 3 app + background elevated collector process. Dark-first design. Open source (MIT).
Full architecture + design system in [PLAN.md](PLAN.md).

**Target platform:** Windows 10 22H2 / Windows 11, x64 only.
**Dev environment:** Visual Studio 2022, Windows App SDK + .NET desktop workloads.
**Build:** Open `WinPerfMon.sln` → set platform x64 → run `WinPerfMon.App`.

---

## Current Milestone: #2 — Processes Section

Building the Task Manager replacement:
- Data source: `NtQuerySystemInformation` (SystemProcessInformation) + ETW for disk/network
- UI: sortable DataGrid, columns: Name / PID / CPU% / Memory / Disk / Network / GPU%
- Right-click menu: Kill, Set Priority, Set Affinity, Open File Location

---

## Progress Checklist

### Foundation ✅
- [x] Solution + 5 projects (`Shared`, `Storage`, `Collector`, `App`, `Tests`)
- [x] All `.csproj` files, NuGet references correct
- [x] `app.manifest` — `requireAdministrator`
- [x] `.gitignore`
- [x] GitHub Actions CI — tests on ubuntu-latest, full build on windows-latest
- [x] 9 xUnit tests for MetricStore (all 4 metric types + SMART model + prune)

### Shared Models ✅
- [x] `CpuMetrics` + `CoreMetrics`
- [x] `GpuMetrics` + `GpuVendor`
- [x] `NetworkMetrics` + `InterfaceStats` + `ProcessNetworkStats`
- [x] `StorageMetrics` + `DiskStats` + `SmartAttribute` + `NvmeStats`
- [ ] `ProcessEntry` model — for Processes section (next)

### Storage Layer ✅
- [x] `MetricStore` — SQLite WAL, read/write/prune for all 4 metric types
- [ ] `ProcessStore` — snapshot + query for process list history (next)

### Collector
- [x] `CpuSensor` — LHM: total load, per-core load/clock/temp
- [x] `GpuSensor` — LHM: NVIDIA + AMD, load/VRAM/clock/temp
- [x] `NetworkSensor` — per-interface bandwidth + gateway ping
- [x] `StorageSensor` — LHM SMART + PDH read/write bytes
- [x] `CollectorWorker` — parallel sensor reads every 1s
- [x] `Program.cs` — hosted service wiring
- [ ] `ProcessCollector` — `NtQuerySystemInformation` per-process CPU/memory + ETW disk+network (next)
- [ ] `HardwareInspector` — LHM sensor tree + CPUID details (Milestone 4)
- [ ] `NetworkAnalyzer` — TCP table + ETW DNS (Milestone 5)

### App Shell ✅ (placeholder pages — will be redesigned in Milestone 3)
- [x] `App.xaml` / `App.xaml.cs` — DI container wired
- [x] `MainWindow` — NavigationView, 4 nav items + Settings
- [x] `BaseViewModel` — INotifyPropertyChanged
- [x] `CpuViewModel` / `GpuViewModel` / `NetworkViewModel` / `StorageViewModel`
- [x] All 4 panel pages + SettingsPage (placeholder quality)
- [x] `Converters.cs` — BytesPerSec, HealthColor, CoreIndex (registered in App.xaml ✅)

### Processes Section (Milestone 2 — in progress)
- [x] `ProcessEntry` model — CPU/memory/disk/network/GPU per process + display helpers
- [x] `NativeMethods.cs` — NtQuerySystemInformation + priority/affinity/terminate P/Invoke
- [x] `ProcessCollector` — NtQuery sampling, CPU% delta, disk I/O delta, path+description cache
- [x] `ProcessesViewModel` — 1s refresh, filter by name/PID/desc, sort by any column, diff-update
- [x] `ProcessesPage.xaml` — toolbar, sortable column headers, ListView with per-row CPU color
- [x] `ProcessesPage.xaml.cs` — End Task (confirm dialog), right-click menu, priority, affinity
- [x] `CpuPercentConverter`, `LoadColorConverter`, `StringToVisibilityConverter` — new converters
- [x] All converters registered in `App.xaml`
- [x] Processes nav item added to `MainWindow` (now default landing page)
- [ ] Tests for ProcessCollector data shape (needs Windows to run — write on PC)

### Dashboard Redesign (Milestone 3 — not started)
- [ ] Arc gauge `UserControl` (custom WinUI control, 270° sweep)
- [ ] Dark design system tokens applied globally
- [ ] Preset switcher pill in title bar
- [ ] Onboarding screen (first launch, pick preset)
- [ ] Dashboard page rebuilt with arc gauges + sparklines

### Hardware Section (Milestone 4 — not started)
- [ ] LHM sensor tree rendered as grouped expandable list
- [ ] CPU details (CPUID: cache topology, microcode, P/E core map)
- [ ] GPU details (shader count, bus width, VRAM type, hot spot)
- [ ] Sensor CSV/JSON logging

### Network Section (Milestone 5 — not started)
- [ ] Connection list (`GetExtendedTcpTable` per-process)
- [ ] Per-process bandwidth (ETW)
- [ ] DNS query log (ETW `Microsoft-Windows-DNS-Client`)

### Storage Depth (Milestone 6 — not started)
- [ ] Full SMART attribute table
- [ ] Per-file I/O (ETW `Microsoft-Windows-Kernel-FileIO`)

### Polish (Milestone 7 — not started)
- [ ] Settings persistence (`%LOCALAPPDATA%\WinPerfMon\settings.json`)
- [ ] Alert rules engine
- [ ] Tray icon + hide-to-tray
- [ ] Gaming overlay (detachable HUD)
- [ ] CSV/JSON export
- [ ] WinGet packaging

---

## Known Gaps (pre-first-run blockers)

- [ ] `NetworkSensor.TopProcesses` always empty — ETW not wired yet
- [ ] `GpuMetrics.PcieBandwidthMbps` always 0 — needs NVML wrapper
- [ ] `DiskStats.ReadIops/WriteIops` always 0 — PDH two-sample delta missing
- [ ] `CpuMetrics.ContextSwitchesPerSec` always 0 — PDH counter missing
- [ ] App shows blank 1-2s on start — no "waiting for data" state yet

---

## File Map

```
win-perf-mon/
├── WinPerfMon.sln
├── PLAN.md
├── CLAUDE.md                        ← this file
├── .gitignore
├── reference-design.png             ← UI reference screenshot
├── .github/workflows/build.yml
└── src/
    ├── WinPerfMon.Shared/
    │   └── Models/
    │       ├── CpuMetrics.cs
    │       ├── GpuMetrics.cs
    │       ├── NetworkMetrics.cs
    │       └── StorageMetrics.cs
    ├── WinPerfMon.Storage/
    │   └── MetricStore.cs
    ├── WinPerfMon.Collector/
    │   ├── Program.cs
    │   ├── CollectorWorker.cs
    │   └── Sensors/
    │       ├── CpuSensor.cs
    │       ├── GpuSensor.cs
    │       ├── NetworkSensor.cs
    │       └── StorageSensor.cs
    ├── WinPerfMon.Tests/
    │   └── MetricStoreTests.cs
    └── WinPerfMon.App/
        ├── app.manifest
        ├── App.xaml / App.xaml.cs
        ├── MainWindow.xaml / .cs
        ├── Controls/
        │   └── Converters.cs
        ├── Pages/
        │   ├── CpuPage.xaml / .cs
        │   ├── GpuPage.xaml / .cs
        │   ├── NetworkPage.xaml / .cs
        │   ├── StoragePage.xaml / .cs
        │   └── SettingsPage.xaml / .cs
        └── ViewModels/
            ├── BaseViewModel.cs
            ├── CpuViewModel.cs
            ├── GpuViewModel.cs
            ├── NetworkViewModel.cs
            └── StorageViewModel.cs
```

---

## Session Log

| Date | Machine | What was done |
|---|---|---|
| 2026-05-01 | macOS | Brainstorming, stack decision, full scaffold (4 projects, 38 files) |
| 2026-05-01 | macOS | Converter fix, net8.0 TFM fix, Tests project (9 tests), GitHub Actions CI |
| 2026-05-01 | macOS | Expanded scope to full 5-tool replacement; updated PLAN.md + CLAUDE.md; starting Processes section |
| 2026-05-01 | macOS | Processes section complete: ProcessEntry model, NativeMethods P/Invoke, ProcessCollector, ViewModel (sort/filter/diff-update), ProcessesPage (End Task, right-click, priority, affinity) |

---

## Next Session Start (Windows PC)

1. `git pull` → open `WinPerfMon.sln` in VS 2022 → restore NuGet → build
2. Processes section is code-complete — first task is **build + run** and fix any compile errors
3. Write ProcessCollector tests (needs Windows to actually run NtQuery)
4. Then move to **Milestone 3: Dashboard redesign** — arc gauge UserControl is the first piece
5. Key gap still open: ETW per-process network (disk I/O works via NtQuery, network is stubbed at 0)
