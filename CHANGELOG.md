# Changelog

## v9.0

- Generalised the project branding from X9 to MCHOSE Battery Tray.
- Added exact receiver-ID profiles for selected MCHOSE headset families sharing the legacy `FF90:0001` battery protocol.
- Added `×` for disconnected/offline and `?` for present-but-unknown/unsupported or no trustworthy current percentage.
- Added a Windows PnP audio-endpoint check to avoid showing `×` when an unknown MCHOSE headset is still detected by Windows.
- Kept the read-only request policy: unknown receiver IDs are never sent vendor commands.
- Kept the 60-second polling interval, retry, stale-value handling, and Bahnschrift tray icon.
- Added English and Russian documentation and protocol notes.
