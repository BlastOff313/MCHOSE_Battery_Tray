# MCHOSE Battery Tray — Portable Edition (Legacy v9.0)

This is the last PowerShell-based portable edition. Extract the ZIP and run `start.vbs`. It uses Windows PowerShell 5.1 included with Windows 10/11. No installer, administrator rights, .NET SDK, or third-party runtime is required.

For the current native application, use **Windows Installer v10.0.2** from the main release. Portable v9.0 is retained for users who specifically prefer a no-install ZIP and does not include all v10 improvements (including bounded log retention and native .NET app identity).

## Run
1. Extract the ZIP to a normal folder.
2. Double-click `start.vbs` for a silent launch.
3. Run `start-debug.cmd` to see errors in a console.
4. The icon appears in the Windows notification area. If hidden, enable it in Windows Taskbar notification-area settings.
5. Right-click the icon to refresh, toggle startup, open the log, or exit.

## Compatibility
Requires a USB receiver and 2.4 GHz mode. Bluetooth-only and wired audio are not supported. Compatibility is tied to exact receiver hardware IDs; unknown receiver IDs are never sent vendor commands. See the main [README](../README.md) for the currently documented receiver compatibility table.

## Notes
- Do not run Portable and Installer editions simultaneously.
- Log: `%LOCALAPPDATA%\MCHOSE_X9_BatteryTray\tray-v9.log`.
- This legacy edition does not implement the v10 log limit/retention controls.

MIT License. ChatGPT by OpenAI is credited as AI development assistance, not as a human author or copyright holder. MCHOSE is a trademark of its respective owner.
