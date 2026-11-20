# MCHOSE Battery Tray v10.0.2 — Release notes

This release is distributed in two editions:

- **Windows Installer v10.0.2** — recommended native Windows application. Install with the setup executable. Supports optional startup with Windows, bounded/optional logs, and uninstall from Windows Installed apps.
- **Portable v9.0 (legacy)** — no-install PowerShell edition. Extract the ZIP and run `start.vbs`. It is preserved for users who prefer a portable package, but it does not include all v10.0.2 improvements.

Do not run both editions at the same time.

## v10.0.2 changes

- Separate HID input and output handles/streams.
- Process incoming receiver reports while output-write completion is pending.
- Clearer HID diagnostics.
- Retains the corrected tray icon renderer and red–white–green battery gradient.
- 60-second battery polling interval.
- Optional bounded diagnostics log (500 lines, 14-day retention) in the native installer edition.

## Downloads

Both files are attached to this GitHub Release:

- `MCHOSE-Battery-Tray-Setup-10.0.2.exe`
- `MCHOSE-Battery-Tray-Portable-v9.0.zip`

For supported receiver IDs, connection-mode requirements, safety notes, and detailed instructions, see [README.md](README.md) or [README.ru.md](README.ru.md).
