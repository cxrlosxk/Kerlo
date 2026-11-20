# Kerlo roadmap

The initial **technical beta stabilization checkpoint** is documented in [BETA_CLOSEOUT.md](BETA_CLOSEOUT.md). Development now shifts from foundation repair to usability, sensor coverage and release engineering. Public beta distribution is a separate acceptance gate.

## Complete: technical beta foundation
- WPF / C# / .NET 10 native Windows app with a consistent dark theme.
- Real CPU, RAM, network, process, optional disk and GPU 3D engine counters.
- Process grouping, per-process drill-down, filtering, select-and-confirm termination.
- In-place process-row updates and correct enumeration of PIDs even if certain measurements are inaccessible.
- Charts, memory-bounded session history, CSV export, startup Run-key inspection, local preferences and tray.
- Windows GitHub Actions build, 24 deterministic regression checks, Windows live telemetry including GPU refresh/PID coverage, and GUI startup smoke test.
- User-device screenshots and recordings used to confirm process-count and theme fixes. GPU refresh still needs user-device retest.

## Next: visual design and interactive QA
- Claude-led original native Windows design pass with careful typography and motion.
- Real-device checks for GPU refresh over many cycles, process sorting/scrolling, keyboard controls and chart displays.
- Screen readers, high DPI, contrast, multimonitor and resizing tests.
- Resource budget profiling and optimization (targets: <1% average idle CPU, <150 MB working set; not verified).
- Improve app icon, title bar and native light theme if desired.

## Then: deeper Windows monitoring
- Hardware sensors: adapter-level GPU, VRAM, temperatures and power with fallbacks.
- Optional matching private-memory/process accounting and Task Manager validation.
- Broader Windows startup enumeration and transparent control.
- Local persisted history, alerts, developer process trees and diagnostics.
- Optional gaming FPS/frametime telemetry and compact overlay after performance and anti-cheat risk evaluation.

## Final: public beta release readiness
- Cross-machine Windows QA and hour-long soak tests.
- Installer/uninstaller, code signing, versioning and safe updates.
- Privacy, security, accessibility and software-licensing reviews.
- Final release notes, documentation and issue triage.

## Business model
A complete, valuable free core; optional Pro features may be assessed in a later product cycle. **No paywalls, subscriptions, accounts or payment infrastructure in the current codebase.**

Detailed phases and acceptance tests: [NEXT_PHASE.md](NEXT_PHASE.md), [TEST_PLAN.md](TEST_PLAN.md).
