# Crosshair Alt

A lightweight, pixel-exact crosshair overlay for Windows with a frosted glass / blur UI, customizable global function-key hotkeys, profile management, and Valorant crosshair code import.

## Features

- **Pixel-Exact Overlay**: Transparent, click-through, always-on-top overlay rendered directly in screen pixels with no game injection or memory reading.
- **Frosted Glass / Blur UI**: Frameless glassmorphism interface with Acrylic, Gaussian Blur, and Clear backdrop modes, customizable transparency, and built-in color themes (`Mica Dark`, `Catppuccin Mocha`, `Nord`, `Catppuccin Latte`, `Cloud`).
- **Customizable Function Keys & Hotkeys**:
  - **Toggle Crosshair**: `F6` (default)
  - **Show / Hide App**: `F7` (default)
  - **Open Options**: `F8` (default)
  - **Next Profile**: `F9` (default)
  - **Exit Application**: Optional (unbound by default, configurable in Options)
- **Valorant-Style Customization**: Color presets & hex input, outline thickness/opacity, center dot, inner & outer lines (length, thickness, gap, opacity), T-style, and X/Y screen offset.
- **Profiles & Import**: Save/delete custom profiles and import primary crosshairs from Valorant profile codes (`0;P;c;5;...`).
- **System Tray Integration**: Single-instance support, optional start minimized, and minimize-to-tray on close.

## Building

Double-click `build.bat` or run:

```powershell
dotnet publish CrosshairAlt.csproj -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true -o publish
```

The standalone `CrosshairAlt.exe` will be written to `publish\`.

## Usage

- **Default Hotkeys**:
  - `F6`: Show / hide crosshair overlay
  - `F7`: Show / hide Crosshair Alt window
  - `F8`: Open Options & Hotkeys
  - `F9`: Cycle to the next crosshair profile
- All hotkeys can be rebound to any function key (`F1`–`F12`) or key combination in the **Options** tab.
- Settings are automatically saved to `%APPDATA%\CrosshairAlt\settings.json`.

## Notes

- Exclusive fullscreen games render directly over desktop overlays. Use **Borderless Windowed / Windowed Fullscreen** mode in your game so the overlay stays visible.
- Some anti-cheat systems and tournament rules prohibit third-party crosshair overlays. Always verify your game's and tournament's rules before use.

## License

Licensed under the [MIT License](LICENSE) — Copyright (c) 2026 Bishpop.
