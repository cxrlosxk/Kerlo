# Changelog

Changes are summarized by engineering milestone. Some developer previews were distributed as separately named ZIP files while the application assembly still identified as 0.1.0; **0.1.4-beta.1 is the first synchronized beta-stabilization version label**.

## 0.1.4-beta.1 — November 20, 2026
- Double-buffered the GPU 3D performance counter refresh so a successful 30-second rediscovery no longer forces an artificial N/A sample.
- Distinguished initial GPU counter warm-up from genuine unavailable telemetry.
- Added a forced GPU rediscovery integration check and a regression check for misleading warm-up insights.
- Declared the initial technical beta stabilization checkpoint and published the implementation/quality handoff. This is still an **unsigned tester preview**, not a public consumer release.

## 0.1.3 stabilization
- Reconciled grouped and individual process rows in place to improve scroll, sort and selection stability while metrics refresh.
- Added explanatory Windows GPU counter status and dashboard tooltips, plus missing-sensor checks.

## 0.1.2 process measurement
- Enumerated process IDs even when CPU time or working-set counters are inaccessible.
- Added N/A and partial-group accounting instead of silently discarding protected processes.
- Added an independent Windows PID coverage integration check; a user test observed ~399 processes rather than ~198.

## Earlier foundation
- Original WPF .NET 10 desktop shell, live Windows telemetry, dashboard charts and app grouping.
- Process inspector, search, protected-process controls, startup Run-key reader, CSV export, system tray and settings.
- Improved dark theme, contrast, keyboard readability, page scrolling, performance-value alignment and scrollbars.
- Added automated Windows builds, core regression checks, live Windows telemetry checks and executable startup smoke tests.

See [BETA_CLOSEOUT.md](BETA_CLOSEOUT.md) and [ROADMAP.md](ROADMAP.md). No version in this file should be construed as a fully certified public release.
