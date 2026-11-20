# MCHOSE Battery Tray


## Editions and downloads

Choose the edition that fits how you want to use the app:

| Edition | Download | Description |
|---|---|---|
| **Windows Installer v10.0.2** (recommended) | [Download from Releases](https://github.com/BlastOff313/MCHOSE_Battery_Tray/releases/latest) | Native .NET Windows app, installer, optional startup, bounded logs, and standard uninstall. |
| **Portable v9.0** (legacy) | [Download Portable ZIP](https://github.com/BlastOff313/MCHOSE_Battery_Tray/releases/latest/download/MCHOSE-Battery-Tray-Portable-v9.0.zip) | No installer. Extract the ZIP and run `start.vbs`. Uses built-in Windows PowerShell 5.1. |

The Portable edition is retained for users who prefer a no-install package. It is a legacy v9.0 codebase and does **not** include all v10.0.2 improvements (including the 500-line log limit and native .NET application identity). For a new installation, use the Windows Installer.

**Important:** do not run Portable and Installer editions at the same time. Close one edition before starting the other to avoid duplicate tray icons and competing HID polls.


## Latest release note — v10.0.2

Hardens the HID request/response path: input reports and output commands now use separate device handles and separate `FileStream` instances. The app watches incoming reports and output-write completion concurrently instead of waiting for `WriteAsync` before examining the input report. This allows a valid battery response to be accepted even if the HID driver delays write completion. The log now marks reports received while output completion is pending. This change still requires validation with a physical MCHOSE X9 receiver. The release also retains the corrected `Graphics.DrawString` tray-icon renderer from v10.0.1.

A lightweight native Windows notification-area app that displays the battery level of supported MCHOSE wireless headsets. It communicates with supported 2.4 GHz USB receivers using read-only HID requests and does not require MCHOSE HUB to stay open.

> **Compatibility note:** this is not a universal driver for every MCHOSE headset. Compatibility depends on the exact receiver hardware and firmware revision. Unknown receiver IDs are never sent vendor battery commands.

## Features

- Battery percentage directly in the Windows notification area.
- Smooth muted **red → soft white → muted green** colour gradient: low charge is red, around 50% is white, high charge is green.
- `F+` in the tray icon when the reported level is 100%.
- `×` when the headset is explicitly offline, the receiver is missing, or two consecutive battery polls time out.
- `?` when a physical MCHOSE device is detected but its battery protocol is not supported, or a trustworthy percentage is not currently available while charging.
- Windows-style compact tray icon using Bahnschrift Bold, with a theme-aware outline for contrast.
- Automatic refresh every 60 seconds and a manual **Refresh now** command.
- Optional start with Windows.
- Optional diagnostic logging, enabled by default on first launch and switchable from the tray menu.
- Log limit: **500 lines**. The active file is trimmed after every write; old log files are cleaned after **14 days**.
- Native executable identity (`MCHOSE Battery Tray`) instead of a `powershell.exe` host process.
- Self-contained Windows installer; no separate .NET runtime installation is required.
- Appears in Windows **Settings → Apps → Installed apps** and can be removed using the standard Windows uninstaller.
- No cloud account, telemetry, or application network connection.

## Compatibility

### Legacy HID protocol implemented

The following receiver IDs are mapped to the same community-documented read-only exchange (`FF90:0001`, output report `55 65 01`, response `55 65 <battery> <state>`):

| Headset family / model | USB receiver VID:PID | Evidence status |
|---|---:|---|
| MCHOSE X9 | `3837:6045` | Tested by the project owner on Windows 11 |
| MCHOSE V9 Pro | `291D:385D` | Community hardware-tested protocol report |
| V9 / V9 Pro family | `41E4:3201`, `41E4:3202` | M HUB-derived / community-documented; not independently tested by this project |
| MCHOSE G9 Pro | `41E4:3502` | M HUB-derived / community-documented; not independently tested by this project |
| MCHOSE S9 Pro | `41E4:3402` | M HUB-derived / community-documented; not independently tested by this project |
| V9 Turbo / V9 Turbo+ family | `3837:600D`, `3837:600A`, `3837:6009` | M HUB-derived / community-documented; not independently tested by this project |

The receiver must be connected over USB and the headset must be in **2.4 GHz receiver mode**. This build does not read battery information through Bluetooth or wired audio.

### Not implemented / requires more protocol data

- **V9 Pro 2 (`3837:6011`)**: uses a different HID collection (`FF07:0222`) and a different `0x55 / 0x0B` battery frame. Its parser is not implemented.
- **R9, S9, G9 (non-Pro), and other variants**: no sufficiently verified receiver mapping/protocol is included yet. If a physical MCHOSE USB/HID/Bluetooth device is detected but has no supported battery profile, the icon shows `?`.
- Bluetooth-only connections and wired audio mode are not supported by the battery reader.

A model name alone is not enough to guarantee support. Contributions should add exact receiver IDs and protocol details only after hardware testing or a trace from a trusted source.

## Installation

1. Close any older PowerShell-based MCHOSE tray version to avoid seeing two icons during the upgrade.
2. Download `MCHOSE-Battery-Tray-Setup-10.0.2.exe` from the repository GitHub **Releases** page.
3. Run the installer. It installs into your user profile and does not require administrator rights.
4. Optionally select **Start MCHOSE Battery Tray when I sign in to Windows** during setup.
5. The app starts in the notification area. If Windows initially hides it behind `^`, open **Settings → Personalization → Taskbar → Other system tray icons** and enable **MCHOSE Battery Tray**, or drag it onto the visible tray area.

The tray process is a native executable with its own product metadata and AppUserModelID. It is no longer hosted by Windows PowerShell 5.1. The executable's product metadata and process AppUserModelID are set to **MCHOSE Battery Tray**, so Windows has an app identity to display in the notification-area settings rather than the generic PowerShell host name. The exact label should still be confirmed on different Windows 11 builds.

The installer is not Authenticode-signed. Windows SmartScreen may show a reputation warning for a newly downloaded community build.

### Uninstall

Open **Settings → Apps → Installed apps**, find **MCHOSE Battery Tray**, and select **Uninstall**. The installer also creates a standard Start Menu entry. Uninstall removes the program and its startup registration. User settings and logs under `%LOCALAPPDATA%\MCHOSE\Battery Tray` are intentionally preserved.

## Tray menu

Right-click the tray icon to:

- Refresh the battery immediately.
- Enable or disable start with Windows.
- Enable or disable diagnostic logging.
- Open or clear the current log.
- Exit the application.

## Logging and cleanup

Logging is enabled by default on first launch. Toggle **Log diagnostics** from the tray menu to disable all further log writes. The setting is saved between launches.

- Current log: `%LOCALAPPDATA%\MCHOSE\Battery Tray\Logs\app.log`
- Maximum active log size: 500 lines. The log is reduced to its newest 500 lines immediately after a write if the limit is exceeded. Individual entries are limited to 4,096 characters.
- Retention: stale `*.log*` files older than 14 days are removed during startup and periodic cleanup. The current log is emptied if it has not been touched for more than 14 days.
- Settings: `%LOCALAPPDATA%\MCHOSE\Battery Tray\settings.json`

When logging is disabled, the app does not append diagnostic events. Startup cleanup and log trimming still run.

## Building from source

Requirements:

- Windows 10/11 x64 build machine.
- .NET 10 SDK.
- Inno Setup 6.

Run PowerShell from the repository root:

```powershell
.\build.ps1
```

The script publishes a self-contained single-file application and compiles the Inno Setup package into `dist\MCHOSE-Battery-Tray-Setup-10.0.2.exe`.

A GitHub Actions workflow at `.github/workflows/build-installer.yml` builds the installer on `workflow_dispatch` or when a `v*` tag is pushed. For a public release, push a tag such as `v10.0.2`; the workflow creates a GitHub Release and attaches the installer.

The project targets **.NET 10 LTS**, which is supported through November 2028. <https://dotnet.microsoft.com/en-us/platform/support/policy>

## Safety and privacy

- The app sends only a read-only battery query to explicitly listed USB receiver IDs.
- Unknown product IDs are not sent vendor commands.
- PnP/WMI is used only to distinguish a present but unsupported physical MCHOSE device from a disconnected receiver; cached software audio endpoints are ignored.
- No headset settings, EQ, firmware, or pairing state are changed.
- No network requests, analytics, or telemetry are implemented.
- Diagnostic logging is local and can be disabled at any time.

## Contributing compatibility reports

If your MCHOSE headset is not supported, open an issue and include:

- Exact headset model and revision.
- Connection mode: 2.4 GHz USB receiver, Bluetooth, or USB cable.
- Receiver VID:PID from Device Manager → device Properties → Details → Hardware Ids.
- The log at `%LOCALAPPDATA%\MCHOSE\Battery Tray\Logs\app.log` (only if logging is enabled).
- Whether MCHOSE HUB detects the headset in 2.4 GHz mode.

Do not post serial numbers or personal information. Protocol additions must be restricted to exact receiver IDs and read-only commands.

## Sources and protocol research

- [MCHOSE Gaming Headset Software Guide](https://support.mchose.store/hc/en-us/articles/52492201202324-Gaming-Headset-Software-Guide)
- [MCHOSE official user manuals](https://www.mchose.store/pages/user-manual)
- [OpenMHub supported-device notes](https://github.com/brayanspagnol/OpenMHub)
- [Device-Battery-Info protocol notes](https://github.com/PyFlat/Device-Battery-Info/blob/main/AGENTS.md)
- [BatteryHub supported-device notes](https://github.com/krisgfx9/Wireless-Device-Battery-Monitor)

This project is independent and is not affiliated with or endorsed by MCHOSE. MCHOSE is a trademark of its respective owner.

## Contributors

See [CONTRIBUTORS.md](CONTRIBUTORS.md). ChatGPT by OpenAI is credited as AI development assistance, not as a human author or copyright holder.

## License

[MIT](LICENSE).

## Russian documentation

See [README.ru.md](README.ru.md).
