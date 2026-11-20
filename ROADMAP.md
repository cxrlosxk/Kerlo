# Kerlo delivery plan

## Implemented in foundation (Windows compile verification pending CI)
- Native Windows desktop architecture and dark UI.
- Real CPU, RAM, process, network and optional GPU/disk sampling.
- Process grouping, filtering, selection and explicit end-process confirmation.
- History charts, CSV export, startup inspection, local preferences, and system tray.
- Deterministic pure-core regression checks and Windows GitHub Actions publish pipeline.

## Next engineering work required before a public beta
1. Compile and launch on a real Windows 11 workstation; profile memory, startup time, chart accuracy and refresh overhead.
2. Compare CPU, RAM, and process figures against Windows Task Manager and Performance Monitor on several PCs.
3. Add reliable temperature, GPU adapter, VRAM, clocks, and power telemetry with vendor-supported fallbacks and correct licensing.
4. Implement true foreground FPS/frame-time monitoring and a low-overhead optional gaming overlay.
5. Add recorded resource history across restarts, anomaly trends and configurable alerts.
6. Build developer session inspection (CLI child-process trees, optional agent integrations) with privacy protection.
7. Add polished accessibility, keyboard navigation, high-DPI tests, light theme and localization.
8. Package with MSIX or a safe installer, code-sign releases and verify uninstalls/update paths.
9. Evaluate modern design changes from Claude against measured performance and accessible contrast.
10. Add privacy-preserving crash reports only with explicit consent.
11. Explore optional Pro features later. Do not implement payments, subscriptions, mandatory accounts or paywalls in the free beta.

## Benchmarks / acceptance goals (targets, not verified results)
- Background CPU at idle: average under 1% on a modern desktop at a 2 s sampling interval.
- Working set: target under 150 MB while monitoring normal workloads.
- UI remains responsive with 300+ processes and rapid sorting/filtering.
- Never display fabricated temperature or GPU readings, and never end a process without confirmation.
- Windows x64 build workflow succeeds and publishes a self-contained executable.
