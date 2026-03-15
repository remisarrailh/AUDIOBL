# AUDIOBL — Build Instructions

## Prerequisites
- .NET 8 SDK (Windows)
- Windows 11 SDK 10.0.22621.0

## Steps

### 1. Generate the tray icon (once)
```powershell
cd AUDIOBL
powershell -ExecutionPolicy Bypass -File generate-icon.ps1
```

### 2. Restore and build
```
dotnet restore AUDIOBL/AUDIOBL.csproj
dotnet build AUDIOBL/AUDIOBL.csproj -c Release
```

### 3. Run
```
dotnet run --project AUDIOBL/AUDIOBL.csproj
```

## Notes
- The app starts minimized to the system tray (no window in taskbar)
- Double-click the tray icon to toggle the battery overlay
- Right-click the tray icon for the context menu
- Settings are stored in `%APPDATA%\AUDIOBL\settings.json`
- Single-instance: only one copy can run at a time (mutex `Global\AUDIOBL`)

## Button Interception
The ATH-M50xBT touch button is registered via Raw Input (HID Consumer Controls, Usage Page 0x0C).
If the button sends AVRCP events instead of raw HID, a WMI media key hook may be needed.
