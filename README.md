# Kerlo

**See what your PC is doing. Understand why.**

Kerlo is a Windows desktop Activity Monitor, built to be calm, fast, and useful. Its free core contains real process monitoring, live CPU and memory charts, app grouping, network and disk readings where supported, session history, startup inspection, and safe process actions.

## Development status

**0.1.4-beta.1 — technical stabilization checkpoint.** Kerlo is an unsigned Windows **tester preview**, not a publicly signed, fully validated release. We have corrected major process undercounting on the user's PC and improved GPU counter refresh behavior; some usability and hardware checks still need real-device verification.

- [What we built and actually verified](BETA_CLOSEOUT.md)
- [Prioritized next phase and public beta gates](NEXT_PHASE.md)
- [Release history](CHANGELOG.md)
- [Design and QA brief](DESIGN_HANDOFF.md)
- [Windows verification plan](TEST_PLAN.md)

## Building

Requires Windows 10/11, the .NET 10 SDK, and Visual Studio 2022 with the desktop .NET workload (or the SDK command line).

```powershell
dotnet run --project src/Kerlo/Kerlo.csproj
dotnet run --project tests/Kerlo.SelfTests/Kerlo.SelfTests.csproj
```

A GitHub Actions workflow compiles the Windows application, executes core regression checks, and generates an unsigned, self-contained x64 ZIP artifact. Download the artifact from the latest successful **Windows build and verification** run. This is a developer build, not a signed public release.

## What the current code does

- Measures system CPU load using Windows system times, and physical RAM usage via GlobalMemoryStatusEx.
- Samples running processes and groups common application helper processes by recognizable application name.
- Reports aggregate working-set bytes, relative CPU capacity usage, and a per-process inspector.
- Estimates network traffic from Windows network-interface byte counters and reads disk activity with performance counters where supported.
- Reads GPU 3D Engine performance counters where supported; **this is 3D engine utilization, not a universal total GPU utilization or temperature reading**.
- Displays live graphs, session insights, locally stored preferences, and session CSV export.
- Lists standard Windows Run registry startup entries without modifying them.
- Offers opt-in process termination with an explicit confirmation; terminating critical processes can still be disruptive.
- Keeps everything local and does not transmit telemetry.

## Important limits

- No temperature, clock, power, VRAM, gaming FPS or frametime measurements yet; do not assume unsupported values are zero.
- No overlay or AI cloud integration yet. A Pro tier is not implemented and nothing is paywalled.
- GPU counters depend on Windows, drivers, and permissions; unavailable readings show as unavailable.
- Process working-set totals share pages across processes and should not be interpreted as unique physical RAM.
- Network interface counters represent machine-wide traffic and may include virtual interfaces; duplicate interfaces are filtered where feasible.
- This repository is in development. It is **not** yet a completed, independently verified public release.

See [ROADMAP.md](ROADMAP.md) for the outstanding verification and feature plan. No installer or code signing is supplied in this first iteration.
