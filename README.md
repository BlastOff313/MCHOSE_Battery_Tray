# MCHOSE Battery Tray

A lightweight Windows notification-area app that displays the battery level of supported MCHOSE wireless headsets. It uses read-only HID requests to the headset's 2.4 GHz USB receiver and does not require MCHOSE HUB to stay open.

**License:** [MIT](LICENSE) — free to use, modify, and redistribute, including commercially, subject to preserving the copyright and license notices.

> **Compatibility note:** this is not a universal driver for every MCHOSE headset. It currently supports a set of receiver IDs that share the legacy `FF90:0001` battery protocol. Other models use different protocols and are shown as unsupported until a separate implementation is added.

## Features

- Battery percentage directly in the Windows notification area.
- `F+` in the tray icon when the reported level is 100%.
- `×` when the headset is explicitly offline, the receiver is missing, or two consecutive battery polls time out.
- `?` when Windows detects a MCHOSE audio/HID device but the app cannot obtain a trustworthy battery percentage (unsupported protocol or charging state without a fresh percentage).
- Windows 11-style compact icon using Bahnschrift Bold, with light/dark taskbar-aware text color.
- Automatic refresh every 60 seconds and a manual **Refresh now** command.
- Optional start with Windows.
- Local diagnostic log; no cloud account, telemetry, or network connection is used by this app.

## Compatibility

### Legacy HID protocol implemented

The following receiver IDs are mapped to the same read-only exchange (`FF90:0001`, output report `55 65 01`, response `55 65 <battery> <state>`):

| Headset family / model | USB receiver VID:PID | Evidence status |
|---|---:|---|
| MCHOSE X9 | `3837:6045` | Tested by the project owner on Windows 11 |
| MCHOSE V9 Pro | `291D:385D` | Community hardware-tested protocol report |
| V9 / V9 Pro family | `41E4:3201`, `41E4:3202` | M HUB-derived / community-documented; not independently tested by this project |
| MCHOSE G9 Pro | `41E4:3502` | M HUB-derived / community-documented; not independently tested by this project |
| MCHOSE S9 Pro | `41E4:3402` | M HUB-derived / community-documented; not independently tested by this project |
| V9 Turbo / V9 Turbo+ family | `3837:600D`, `3837:600A`, `3837:6009` | M HUB-derived / community-documented; not independently tested by this project |

The receiver must be connected over USB and the headset must be in **2.4 GHz receiver mode**. The application does not read battery information through Bluetooth.

### Not implemented / requires more protocol data

- **V9 Pro 2 (`3837:6011`)**: uses a different HID collection (`FF07:0222`) and a different `0x55 / 0x0B` battery frame. It is intentionally not queried by this build because its response parser is not implemented here.
- **R9, S9, G9 (non-Pro), and other variants**: no sufficiently verified receiver mapping/protocol is included yet. If Windows exposes a MCHOSE audio endpoint but the receiver ID/protocol is unknown, the icon shows `?` instead of falsely claiming that the headset is disconnected or inventing a battery value.
- Bluetooth-only connections and wired audio mode are not supported by the vendor HID reader.

Compatibility depends on the exact receiver hardware and firmware revision, not only on the headset name. A model name alone is not enough to guarantee support.

## Download and run

1. Download and extract the ZIP.
2. Run `start.vbs` to launch silently, or `start-debug.cmd` to see PowerShell errors.
3. The icon appears in the Windows notification area. If it is under the `^` overflow menu, open **Settings → Personalization → Taskbar → Other system tray icons** and enable MCHOSE Battery Tray, or drag the icon to the visible area.
4. Right-click the icon to refresh, enable/disable startup, open the log, or exit.

No installer or administrator rights are required. The app uses the Windows PowerShell 5.1 runtime included with Windows 10/11.

## Status icons

| Icon | Meaning |
|---|---|
| `0`–`99` | Last successful battery percentage |
| `F+` | Full; the last reported battery value is 100% |
| `×` | Headset offline / no receiver / two consecutive query timeouts |
| `?` | A MCHOSE audio/HID device is present, but no trustworthy percentage is currently available (unsupported receiver/protocol or charging state) |

A single missed poll does not immediately mark the headset offline: the app keeps the last known value for one failed polling cycle to avoid flickering on a transient USB/HID timeout.

## Safety and privacy

- The app sends only a read-only battery query to explicitly listed USB receiver IDs. Unknown product IDs are not queried; Windows PnP audio names are used only to distinguish a present-but-unsupported MCHOSE headset from no detected headset.
- Unknown product IDs are never sent a vendor command.
- No headset settings, EQ, firmware, or pairing state are changed.
- No network requests, analytics, or telemetry are implemented.
- Logs are stored locally under `%LOCALAPPDATA%\MCHOSE_X9_BatteryTray\tray-v9.log`.

## Contributing compatibility reports

If your MCHOSE headset is not supported, please open an issue and include:

- Exact headset model and revision.
- Whether you used the 2.4 GHz USB receiver, Bluetooth, or USB cable.
- Receiver VID:PID from Windows Device Manager → device Properties → Details → Hardware Ids.
- The log at `%LOCALAPPDATA%\MCHOSE_X9_BatteryTray\tray-v9.log`.
- Whether MCHOSE HUB detects the headset in 2.4 GHz mode.

Do not post serial numbers or personal information. Protocol additions should be restricted to exact receiver IDs and read-only commands.

## Sources and protocol research

- [MCHOSE Gaming Headset Software Guide](https://support.mchose.store/hc/en-us/articles/52492201202324-Gaming-Headset-Software-Guide) — current M HUB headset support families and 2.4 GHz connection requirements.
- [MCHOSE official user manuals](https://www.mchose.store/pages/user-manual) — official headset model list.
- [OpenMHub supported-device notes](https://github.com/brayanspagnol/OpenMHub) — community mapping of MCHOSE V9 / V9 Pro / G9 Pro / S9 Pro / X9 / V9 Turbo receiver IDs and shared legacy protocol; several IDs are marked untested by that project.
- [Device-Battery-Info protocol notes](https://github.com/PyFlat/Device-Battery-Info/blob/main/AGENTS.md) — owner-tested V9 Pro (`291D:385D`) legacy HID transaction.
- [BatteryHub supported-device notes](https://github.com/krisgfx9/Wireless-Device-Battery-Monitor) — separate V9 Pro 2 (`3837:6011`) protocol reference; not implemented in this project.

This project is independent and is not affiliated with or endorsed by MCHOSE. MCHOSE is a trademark of its respective owner.

## Russian documentation

See [README.ru.md](README.ru.md).

## Acknowledgements

- **AI development assistance:** [ChatGPT by OpenAI](https://openai.com/) assisted with implementation, debugging, and documentation. See [CONTRIBUTORS.md](CONTRIBUTORS.md). ChatGPT is credited as a development tool, not as a copyright holder or project maintainer.
- **Hardware testing:** the project maintainer tested the MCHOSE X9 receiver on Windows 11.

## License

This project is distributed under the [MIT License](LICENSE). The license permits use, modification, and redistribution, including in commercial projects, provided the copyright and license notices are preserved. It is provided without warranty.
