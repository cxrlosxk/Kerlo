# Kerlo QA and acceptance plan

## Automated in GitHub Actions
1. Restore and compile the native WPF project on windows-latest.
2. Run deterministic core regression checks (CPU normalization, rate calculations, unit formatting, group aggregation, alerts, history, CSV).
3. On a Windows runner, read real kernel CPU times, GlobalMemoryStatusEx physical memory, process CPU/working sets, network counters, startup registry entries, and optional disk/GPU counters; validate measurement bounds.
4. Produce a self-contained unsigned win-x64 build and upload it as an Actions artifact.

## Manual tests required on actual interactive Windows desktops
- Fresh Windows 11 and Windows 10 launches, including systems with unsupported GPU counters.
- At 1080p, 1440p, 4K, 100%, 125%, 150%, 200% DPI: no clipped cards, no invisible focus, no unreadable columns.
- CPU/RAM compare against Task Manager over 30 seconds at idle, under load, and while a known application launches/exits.
- GPU 3D counter compare to Task Manager's 3D engine view, noting that multi-GPU machines and non-3D workloads may differ.
- Network throughput compare while downloading and uploading test data on a single active interface.
- Process grouping, sort, search, refresh, PID churn, process exit during sampling and protected-process denial.
- Confirm process kill dialogs default to **No**, do not terminate groups, and do not act on stale PIDs.
- Startup entries display reliably without elevation and do not modify any keys.
- Tray show/quit, window minimize/restore, close behavior, CSV export, settings persistence and export failures.
- Idle CPU/RAM/handle counts measured for 60 minutes; stress with 300+ processes.
- Screen reader, high-contrast themes, reduced-motion preference and keyboard-only navigation.
- Security review of process actions, library license notices, unsigned build warnings, code-signing and update process.

These manual tests cannot be certified by GitHub's build-only CI or by a Linux coding environment. Do not call the app production-ready until completed.
