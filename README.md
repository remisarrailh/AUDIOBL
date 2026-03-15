# AUDIOBL

A minimal Windows 11 system tray app that shows your Bluetooth headphone battery level as a live percentage in the tray icon and as a floating overlay.

Works with any Bluetooth headphone that supports the **standard GATT Battery Service** (UUID `0x180F`). Tested on Audio-Technica ATH-M50xBT; compatible with most modern wireless headphones from Sony, Bose, Jabra, Sennheiser, and others.

## Features

- **Live battery %** in the system tray icon (updates every 30 s)
- **Floating overlay** — transparent pill showing the battery level with a color indicator
  - Green ≥ 50 %, yellow ≥ 20 %, red < 20 %
  - Draggable, stays on top, no taskbar entry
- **Auto-detect** — finds the first connected Bluetooth device; or filter by name in settings
- **Bilingual** — French / English, auto-detected from Windows, switchable in settings
- **Auto-start with Windows** (configurable)
- Single instance — only one copy runs at a time
- Zero UI on launch — lives entirely in the tray

## Compatibility

Any Bluetooth headphone that exposes the **GATT Battery Service** (Bluetooth SIG standard, UUID `0x180F`) should work. This includes most wireless headphones released after 2018.

Confirmed working: **Audio-Technica ATH-M50xBT**. Should work with Sony WH/WF series, Bose QC/NC series, Jabra Evolve/Elite, Sennheiser Momentum/PXC, and many others.

> If your headphone isn't detected automatically, enter a fragment of its Bluetooth name in Settings (e.g. `WH-1000` for Sony WH-1000XM5).

## Requirements

- Windows 11 (22H2 or later)
- Bluetooth headphone paired and connected
- [.NET 8 Runtime](https://dotnet.microsoft.com/download/dotnet/8.0) — included when installing via MSIX

## Installation

### Pre-built MSIX (recommended)

1. Download `AUDIOBL.msix` from the [Releases](../../releases) page
2. Double-click to install — Windows will prompt to trust the publisher on first install
3. Launch **AUDIOBL** from the Start menu

### Build from source

Requirements: [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0), Windows 10 SDK (makeappx + signtool)

```powershell
# First run — creates a self-signed certificate and installs it (UAC prompt)
.\build-msix.ps1 -Install

# Subsequent builds
.\build-msix.ps1 -Install
```

## Usage

| Action | Result |
|--------|--------|
| App starts | Battery level appears in tray icon |
| Left-click overlay | Drag to reposition |
| Double-click tray icon | Show / hide overlay |
| Right-click tray icon → Afficher / Masquer | Toggle overlay |
| Right-click tray icon → Paramètres | Open settings |
| Right-click tray icon → Quitter | Exit |

## How it works

Battery reading uses the standard **GATT Battery Service** (UUID `0x180F` / characteristic `0x2A19`) over BLE, which the ATH-M50xBT supports. The app scans BLE advertisements to find the device address automatically — no hardcoded MAC address required.

Connection state is tracked via the Windows **Device Information** watcher (`DeviceInformation.CreateWatcher` with a Classic Bluetooth AQS selector and the `AssociationEndpoint` kind).

## Project structure

```
AUDIOBL/
├── AUDIOBL/                  # WPF application
│   ├── Services/
│   │   ├── BluetoothService.cs   # BT connection + GATT battery
│   │   └── SettingsService.cs    # JSON settings + autostart registry
│   ├── Views/
│   │   ├── OverlayWindow.xaml    # Floating battery pill
│   │   └── SettingsWindow.xaml   # Settings dialog
│   ├── Helpers/NativeInterop.cs  # P/Invoke declarations
│   └── Models/AppSettings.cs     # Settings model
├── AUDIOBL.Package/          # MSIX packaging assets
│   ├── Package.appxmanifest
│   └── Assets/               # Store logos, splash screen
└── build-msix.ps1            # Build + sign + install script
```

## License

MIT
