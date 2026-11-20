# MCHOSE Battery Tray v10.0.2

This release changes the HID request/response flow after reports showed repeated `HID output report write timed out` failures. Input and output now use separate handles/streams, and the app watches the input response and output-write completion concurrently. It does not discard a valid battery report merely because the HID driver's output completion remains pending. Successful reports received in that state are marked in the diagnostic log as `output completion pending`.

The tray icon renderer, battery gradient, logging controls, installer registration, and 60-second polling interval are retained from v10.0.1.

This is a targeted code-path correction, not a claim of verified hardware compatibility: it still needs testing with the project owner's physical MCHOSE X9 receiver. If no response is received, the updated log provides more specific information about whether the output write remained pending or failed.
