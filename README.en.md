# Firefly Mouse (MouseFx)

English | [简体中文](README.md)

Windows desktop mouse effects: a persistent cursor glow, click ripples and particle sparks that make your mouse easy to find in any scenario. Runs quietly in the system tray with optional autostart.

<!-- TODO: effect GIFs (halo / sparks / fireworks) -->

## Effect Modes

One mode active at a time, each with its own remembered settings:

| Mode | Effect | Adjustable |
|---|---|---|
| **Halo** | A soft glow follows the cursor; clicks ripple outward | Color, glow size/opacity, follow speed, ripple radius, ripple shape (circle/heart/star) |
| **Sparks** | Tiny sparks scatter along the cursor's trail with gravity, flicker, and burst into child sparks as they burn out | Color, particle limit, lifetime |
| **Fireworks** | Starburst sparks bloom from the cursor like a handheld sparkler: spherical depth, white-hot tips, forked endings | Particle limit, burst diameter |

Each mode has an independent **click toggle** (burst/ripple): when off, clicks have zero effect on that effect. Toggles are independent and persisted.

## Features

- **Tray resident**: left/right click opens the same Fluent menu for settings and exit
- **Global settings**: fade out when idle, auto-hide on fullscreen apps, autostart on boot, render FPS cap (30–144Hz)
- **UI language**: 中文 / English, switching takes effect instantly
- **Theme aware**: settings window and tray menu follow the system light/dark theme (WPF-UI Fluent)

## Download & Install

Grab an exe from the [Releases](../../releases) page and run it directly (portable single file, no installer):

| File | Use case |
|---|---|
| `MouseFx-vX.Y.Z-win-x64.exe` | **Recommended**: self-contained, no runtime needed |
| `MouseFx-vX.Y.Z-win-x64-lite.exe` | Lite (~10MB): requires the [.NET Desktop Runtime 10](https://dotnet.microsoft.com/download/dotnet/10.0) |

The app lives in the system tray; enable autostart in settings.

## Technical Highlights

- Effects render in a fullscreen, click-through overlay window; a Win32 low-level mouse hook (`WH_MOUSE_LL`) only observes events, which are coalesced and marshalled to the UI thread
- Particle engine: struct object pools + frozen pen/brush caches — zero heap allocations per frame; a dirty flag skips redraws when nothing moves, so idle CPU usage is near zero
- Frame-rate independent physics (exponential decay / semi-implicit Euler); the FPS cap only saves rendering, never slows motion
- Robustness: graceful fade-out when input stalls (elevated windows in foreground), automatic software-render fallback with reduced particle density, atomic settings writes
- 119 xUnit unit tests (effect physics, settings persistence, theme/language dictionary completeness)

See the [design document](docs/design.md) (Chinese).

## Requirements & Build

- Windows 10+
- [.NET 10 SDK](https://dotnet.microsoft.com/download) (`net10.0-windows`)

```bash
git clone https://github.com/missionixb/MouseFx.git
cd MouseFx
dotnet build
dotnet run --project MouseFx        # run (tray app)
dotnet test                          # run tests
```

## License

[MIT](LICENSE)
