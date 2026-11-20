# MCHOSE Battery Tray v10.0.1

This is a bug-fix release for v10.0.0. It restores the previously proven `Graphics.DrawString` icon renderer, which fixes the blank notification-area icon reported on Windows 11. A startup guard now detects an empty rendered bitmap instead of silently creating an invisible tray icon.


## What's new

- Native Windows tray application; no PowerShell host process.
- App identity and executable metadata use the name **MCHOSE Battery Tray**.
- Inno Setup installer for per-user installation and standard Windows uninstall registration.
- Red → soft-white → green battery percentage gradient.
- Diagnostic logging can be disabled from the tray menu.
- Active log is capped at 500 lines and 4,096 characters per entry; stale logs are cleaned after 14 days.
- Settings and logs are stored in the current user's LocalAppData folder and preserved after uninstall.
- .NET 10 LTS self-contained win-x64 publish; no separate runtime installation required.

## Compatibility

The supported VID:PID and protocol matrix is documented in [README.md](README.md) and [PROTOCOLS.md](PROTOCOLS.md). MCHOSE X9 (`3837:6045`) is the receiver tested by the project owner. Other receiver profiles are based on published community/M HUB research and still need more owner testing.

## Installer

The Windows installer is built by GitHub Actions. Run the workflow manually for a test artifact, or push tag `v10.0.1` to create a GitHub Release with the installer attached.
