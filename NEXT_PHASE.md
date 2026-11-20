# Kerlo — next development phase

**North star:** Turn the stabilized monitoring foundation into a polished, efficient, credible Windows system utility that can ultimately be distributed publicly. Retain a complete free edition. Do not build subscriptions, licensing, mandatory accounts or a Pro paywall now.

## Priority 0 — acceptance of final stabilization build
1. On the user's Windows PC, run the newest self-contained build, compare total PID count with Task Manager after 30+ seconds, and leave Kerlo open while scrolling/typing/sorting the Processes table for 2–5 minutes.
2. Monitor GPU 3D for at least 3 minutes (six 30-second counter-discovery cycles), including active and idle GPU workloads. Note whether N/A recurs and record the mouseover counter diagnostic. Do not equate GPU 3D engine usage with overall GPU usage.
3. Verify End Process is limited to the selected PID, warns before acting, and does not allow ending protected Windows processes. Do not end important processes just to satisfy a test.
4. Compare telemetry and overhead against Task Manager/Performance Monitor at matched windows with known load. Save dated videos/screenshots and test notes.

If a regression appears, patch and retest **before** expanding features.

## Phase 1 — Claude-led visual and interaction redesign
- Start from the current working native WPF .NET 10 project. Do not replace it with an unmeasured WebView clone or an unreviewed rewrite.
- Research excellent Windows UX, design tokens, app icon, typography, empty/loading/error states, subtle motion, accessibility and crisp, efficient charts.
- Put major UX improvements on a separate design branch and maintain screenshot+perf before/after comparisons.
- Fix interaction rough edges: row filtering/sorting/scroll anchoring, keyboard controls, search clarity, tooltip consistency, responsive detail panels, title-bar finish and tray app icon.
- Consider WinUI 3 migration only after a documented tradeoff/compatibility/maintenance comparison; it is not required to deliver high-quality WPF.

## Phase 2 — monitoring parity and diagnostics
- Add reliable GPU adapter-level telemetry and tested hardware sensors (temperatures, VRAM, clock/power/fans where supported) using approved APIs/libraries with correct licensing and graceful N/A.
- Offer process memory views with explicitly different accounting labels (working set vs private bytes); validate with Windows Task Manager.
- Add broader startup source coverage and clear enabled state. Never auto-disable startup programs.
- Add sustained, bounded local history, evidence-based alerts, and developer-session grouping with privacy safeguards.
- Consider optional FPS/frametime monitoring and game overlays only after validated measurements and acceptable resource overhead.

## Phase 3 — public beta gate
- Complete Windows 10/11 QA across various GPUs/CPUs, scales, displays and user permissions.
- Record reproducible benchmarks for idle CPU, memory, cold start, sampling overhead and long-run stability. Target <1% idle CPU and <150 MB working set but publish actual measured outcomes rather than promises.
- Complete keyboard/screen-reader/high-contrast tests.
- Produce portable and installer packages with version identity, signatures, safe uninstall and update strategy.
- Review security, dependency licensing, privacy claims and crash reporting consent.
- Publish a changelog, known issues, FAQ and versioned beta release only after the gate passes.

## Suggested division of work
- **Claude Code:** UI design concept iterations, native WPF implementation, accessibility and visual screenshots on an actual Windows desktop.
- **Automated CI:** compile, deterministic regression checks, Windows counter integration, artifact packaging and smoke testing.
- **Hands-on Windows QA:** GPU behavior, gaming impact, interaction/scroll verification, compatibility and soak testing.
- **Project owner:** approve aesthetic direction and release readiness based on verified demos and benchmarks.

## Non-goals for the immediate next build
Cloud AI, logins, mandatory accounts, payment infrastructure, automatic system tweaks, disabling Windows services, guaranteed FPS boosts, or falsely precise sensor measurements.
