# Changelog

## v10.0.0

- Replaced the PowerShell-hosted tray script with a native WinForms executable (`MCHOSE.BatteryTray.exe`).
- Added executable product metadata and an explicit Windows AppUserModelID so Windows identifies the tray process as **MCHOSE Battery Tray**, not Windows PowerShell 5.1.
- Added a self-contained .NET 10 Windows x64 build; end users do not need to install .NET separately.
- Added an Inno Setup installer with per-user installation, Start Menu shortcut, optional startup task, and normal Windows Installed Apps / Apps & features uninstall registration.
- Added uninstall cleanup for the app's Run key and removal of the previous PowerShell startup entry.
- Added a continuous muted red → soft white → muted green battery colour gradient.
- Added an optional diagnostic log switch in the tray menu.
- Limited the active log to 500 lines, trimming it after every write; stale log files are cleaned after 14 days.
- Moved logs and settings to `%LOCALAPPDATA%\MCHOSE\Battery Tray`.
- Kept the 60-second polling interval, HID protocol profiles, `F+`, disconnected `×`, unsupported `?`, and retry/stale-value handling.
- Kept unknown receiver product IDs read-only: no vendor battery command is sent to unrecognised IDs.
- Added a GitHub Actions workflow to build the Windows installer and attach it to tagged GitHub Releases.

## v9.0

- Generalised the project branding from X9 to MCHOSE Battery Tray.
- Added exact receiver-ID profiles for selected MCHOSE headset families sharing the legacy `FF90:0001` battery protocol.
- Added `×` for disconnected/offline and `?` for present-but-unknown/unsupported or no trustworthy current percentage.
- Kept the read-only request policy: unknown receiver IDs are never sent vendor commands.
- Added English and Russian documentation and protocol notes.
