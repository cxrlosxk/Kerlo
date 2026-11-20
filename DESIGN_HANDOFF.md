# Kerlo design handoff — Claude Code

Kerlo is a Windows Activity Monitor whose free edition must be complete, useful, visually refined, fast, and private. The repo already contains functional native WPF code, telemetry, and automated Windows checks. Preserve all existing functionality while improving the interface; do not replace real metrics with mock data or ship a web wrapper as the Windows app.

## Design direction
- Understated, precise, spacious, and calm. Favor charcoal, nuanced slate, mineral greens, selective warmth, carefully spaced typography, and crisp live graphs.
- Original visual language. Vitals for Mac is a reference for clarity, **not** a template to copy.
- Kerlo should feel like thoughtful Windows software, including proper window behavior, per-monitor DPI scaling, keyboard navigation and system tray integration.
- Make it attractive on a 1440p desktop without sacrificing good layouts at 1080p, 125–200% scaling, laptop screens and resized windows.
- Reduce visual density through progressive disclosure. Show aggregate application groups first; offer drill-down to individual PIDs.
- Keep explanatory text conversational and connected, rather than telegraphic fragments, decorative eyebrow headings, or marketing filler.
- Always distinguish CPU percent, memory working set, disk throughput, GPU 3D engine usage, and unavailable sensor readings.

## Required workflow
Before making design edits, inspect and select the smallest *verified installed* set of Claude skills and integrations relevant to native Windows UI design, implementation, testing, and accessibility. Candidate tools include frontend-design, Impeccable, Unslop, agent-browser, Superpowers, Context7, Chrome DevTools MCP, and other user-installed skills **only if actually present and applicable**. Browser-focused design skills do not supersede WPF/Windows-specific APIs.

Review existing files and current CI logs before refactoring. Preserve the dark premium look while adding accessible motion, hover states, empty states, focus rings, a coherent icon language, and a polished Kerlo app icon. Add a native light theme only when properly implemented and tested. Avoid animation loops that increase idle CPU load.

## Do not compromise
- The free core stays functional without accounts, payment, subscriptions or cloud features.
- Process termination must require confirmation and must not target a group implicitly.
- Never promise a reading unsupported by the operating system, driver, hardware or chosen telemetry library.
- Keep CPU and memory overhead very low; benchmark before and after visual changes.
- Do not delete or weaken regression tests to make CI green.
- Do not claim Windows UI screenshots or visual QA occurred unless a real Windows session was inspected.
- Obtain explicit authorization before installing system-wide components or modifying protected Windows settings.

## Next design acceptance criteria
- Keyboard-only navigation through sidebar, process table, context actions and preferences.
- Legible charts at narrow widths; strong contrast and accessible text sizing.
- Consistent typography, 8/12/16-point spacing rhythm, and polished loading/unavailable states.
- Grouped process row disclosure and actionable, concise explanations.
- Screenshot capture on an actual Windows machine, plus a comparison of app CPU/RAM overhead against the pre-design baseline.

## Stabilization checkpoint to preserve

Before design changes, read [BETA_CLOSEOUT.md](BETA_CLOSEOUT.md), [NEXT_PHASE.md](NEXT_PHASE.md), [CHANGELOG.md](CHANGELOG.md) and existing issue/CI history. The Nov 20, 2026 beta stabilization fixes are non-negotiable: do not reintroduce the ~50% process-count undercount, reset process tables on each timer tick, treat working set as Task Manager's private memory column, or force GPU N/A every 30 seconds. Check the current beta version and update labels consistently.

Ask Claude Code to first inspect verified installed skills/plugins before editing; choose the smallest relevant set and **do not assume** any specific tool is installed. WPF desktop screenshot tests, Windows telemetry correctness and CPU/memory benchmarks outrank purely browser-focused visuals. Do not start payments, Pro entitlements or a login system.

**Handoff outcome:** visually polished, functionally equivalent or better, accessible native Windows preview with real screenshots and performance measurements, plus a change log. Do not claim public release readiness without separate QA approval.
