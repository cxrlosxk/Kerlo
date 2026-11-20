# Kerlo technical beta closeout

**Milestone:** Technical beta stabilization, November 20, 2026  
**Version:** 0.1.4-beta.1  
**Branch:** main  
**Platform:** Windows desktop, WPF / C# / .NET 10 LTS (not WinUI 3)  
**Release status:** Private/tester preview. NOT approved for unrestricted public release.

## What is implemented and usable

- Native Windows dark desktop app with Overview, Processes, Performance, Startup and Preferences.
- Actual whole-system CPU load, physical memory use, network throughput, and optional physical-disk throughput.
- Windows GPU **3D engine** counters when exposed by Windows; 3D counter activity is not a replacement for Task Manager's overall GPU usage.
- Enumeration of accessible and protected process IDs, with independent CPU/working-set sampling, explicit N/A when denied, and recognizable application group names.
- Live process details, search, sorting, selected-PID termination with warning and protected-process restrictions.
- Session charts, short-lived in-memory history, CSV export, basic evidence-based insights.
- Registry Run startup inspection (read-only and deliberately incomplete relative to Task Manager).
- Local preferences, a system tray, and a portable self-contained Windows x64 executable.
- No logins, cloud analytics, payments, subscriptions, advertising, or Pro paywall.

## What we verified

### GitHub Windows CI
- Native .NET 10 Windows compilation and self-contained win-x64 publishing.
- **24 deterministic core checks** following GPU warm-up regression coverage.
- Live Windows hardware/process integration tests including count validation against independent PID enumeration and forced GPU rediscovery.
- Smoke test: launches Kerlo.exe on a Windows runner for 8 seconds. This is NOT a real GUI interaction test.
- Typical Windows runner startup working set was approximately **167 MB**. Our **<150 MB** aspirational target is not yet met, nor is background CPU overhead established.

### User's Windows testing
- Windows screenshots confirmed corrected dark theme, header contrast, process selection, page navigation, and dropdown legibility.
- Earlier Kerlo builds reported ~198 processes against ~388 in Task Manager. The corrected build later showed **399 Kerlo processes** and approximately **393 in Task Manager**, a substantial reduction of the undercount.
- Side-by-side video snapshots showed about **61.4% Kerlo / 61% Task Manager** physical memory use, and **13.7% / 12%** CPU use at nearby moments. These are indicative snapshots, not a synchronized benchmark.
- A side-by-side recording exposed GPU readings intermittently becoming N/A while Task Manager showed GPU activity. The code intentionally returned null when recreating counters every 30 seconds; the latest fix carries over a previous valid sample during successful rediscovery and has a Windows regression test. **It still needs verification on the user's GPU after this build**.
- Process-table identities are now updated in place to avoid reset on every refresh. **Long-duration scrolling and sorting interaction still need a real-device check**.

## Remaining release blockers

1. **Interactive QA and soak testing:** 1-hour idle and stress runs; chart accuracy, PID churn, process kill safety, process sorting/search/selection, 300–500 processes, high DPI and multiple monitors.
2. **GPU reliability:** verify the 30-second refresh fix on the user's NVIDIA hardware; develop clear engine/adapter/VRAM/temp sensor fallback with truthful labels and license review.
3. **Installer and trust:** decide portable + installer packaging, code signing, clean installation/update/uninstall, SmartScreen prompts, reproducible artifacts, privacy and open-source license audit.
4. **Accessibility and responsiveness:** keyboard-only flow, screen readers, contrast, high-contrast and reduced-motion themes, resized windows at common DPI settings.
5. **Resource budget:** measure idle background CPU, handle counts, startup latency and memory on representative devices; optimize until overhead targets are evidenced, or revise targets transparently.
6. **Incomplete Task Manager parity:** startup enumeration currently covers Run registry entries only; memory is working-set (not Task Manager's default process memory metric); no total GPU/temperature/VRAM telemetry, no FPS overlay, no historical persistence.

No blocker should be concealed or handled by invented metrics. Kerlo should **not** be described as a complete Task Manager replacement or production-ready product at this stage.

## Explicit decision: beta stabilization is complete, public launch is not

This checkpoint freezes the initial monitoring foundation for a dedicated design-and-quality phase. Any public beta launch requires a separate acceptance review of the blockers above. We can keep improving the free edition while exploring optional Pro capabilities later; the free core must remain genuinely useful and fully functional.

## Next handoff

Read [NEXT_PHASE.md](NEXT_PHASE.md) for a prioritized plan and [DESIGN_HANDOFF.md](DESIGN_HANDOFF.md) for a Claude Code design brief. Track issues at https://github.com/cxrlosxk/Kerlo/issues and enforce Windows CI before merging.

The GitHub Actions artifact is **unsigned**, intended for developer testing, and not a signed consumer installer.
