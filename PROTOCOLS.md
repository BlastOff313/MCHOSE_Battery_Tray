# Protocol notes

## Legacy MCHOSE headset family (`FF90:0001`)

Known receiver IDs mapped by this project:

- `3837:6045` — X9 (owner-tested)
- `291D:385D` — V9 Pro (community hardware-tested)
- `41E4:3201`, `41E4:3202`, `41E4:3502`, `41E4:3402` — mapped from M HUB/community research; project owner has not tested each receiver
- `3837:600D`, `3837:600A`, `3837:6009` — V9 Turbo/Turbo+ family, M HUB/community-derived; not owner-tested

The read-only transaction implemented here is:

- HID top-level collection: usage page `0xFF90`, usage `0x0001`
- 64-byte output report beginning `55 65 01`
- Input report beginning `55 65`; battery level at byte 2 and state at byte 3
- State `0x02`: on battery; `0x00` / `0x03`: charging/powered state where percentage may be stale; `0x1A` and unsolicited `00 1A`: headset offline indication

The implementation retries once after a timeout and tolerates unrelated notifications before the battery response.

## Known separate family: V9 Pro 2

Community reports identify receiver `3837:6011`, HID collection `FF07:0222` and a different read-only `0x55 / 0x0B` battery frame. This project does not send that request yet because the response layout and state parsing have not been independently established here. The receiver is intentionally treated as recognized-but-unsupported (`?`).

## Safety rules

- Match exact VID:PID pairs and the expected HID top-level collection before sending any vendor report.
- Never send writes to unknown USB devices.
- Add a profile only with a trace or reliable protocol source and a hardware test report where possible.
- A missed query alone is not proof of disconnection. The UI preserves the last successful value for one missed poll and marks offline after two consecutive failed polls, unless an explicit offline event arrives.
