using System;
using System.Management;

namespace MchoseBatteryTray;

internal static class MchoseDeviceDetector
{
    // Passive PnP lookup only; this method never sends vendor commands to unknown devices.
    public static bool IsPresent()
    {
        try
        {
            using var searcher = new ManagementObjectSearcher(
                "SELECT Name, PNPDeviceID, ConfigManagerErrorCode, PNPClass FROM Win32_PnPEntity");
            using ManagementObjectCollection devices = searcher.Get();
            foreach (ManagementObject device in devices)
            {
                using (device)
                {
                    string name = Convert.ToString(device["Name"]) ?? string.Empty;
                    string id = Convert.ToString(device["PNPDeviceID"]) ?? string.Empty;
                    string pnpClass = Convert.ToString(device["PNPClass"]) ?? string.Empty;
                    int problemCode = 0;
                    try { problemCode = Convert.ToInt32(device["ConfigManagerErrorCode"] ?? 0); }
                    catch { }
                    if (problemCode != 0) continue;

                    // Ignore cached SWD/MMDEVAPI audio endpoints: they can remain in
                    // Windows after the USB receiver is unplugged. Only a present
                    // physical USB/HID/Bluetooth PnP node counts as connected.
                    bool physicalNode = id.StartsWith("USB\\", StringComparison.OrdinalIgnoreCase) ||
                                        id.StartsWith("HID\\", StringComparison.OrdinalIgnoreCase) ||
                                        id.StartsWith("BTHENUM\\", StringComparison.OrdinalIgnoreCase) ||
                                        id.StartsWith("BTHLEDEVICE\\", StringComparison.OrdinalIgnoreCase);
                    if (!physicalNode) continue;
                    bool audioNode = pnpClass.Equals("MEDIA", StringComparison.OrdinalIgnoreCase) ||
                                     pnpClass.Equals("AudioEndpoint", StringComparison.OrdinalIgnoreCase) ||
                                     name.Contains("headset", StringComparison.OrdinalIgnoreCase) ||
                                     name.Contains("headphone", StringComparison.OrdinalIgnoreCase) ||
                                     name.Contains("audio", StringComparison.OrdinalIgnoreCase) ||
                                     name.Contains("науш", StringComparison.OrdinalIgnoreCase) ||
                                     name.Contains("динамик", StringComparison.OrdinalIgnoreCase);
                    if (!audioNode) continue;

                    if (name.Contains("MCHOSE", StringComparison.OrdinalIgnoreCase) ||
                        id.Contains("VID_3837", StringComparison.OrdinalIgnoreCase) ||
                        id.Contains("VID_41E4", StringComparison.OrdinalIgnoreCase) ||
                        id.Contains("VID_291D", StringComparison.OrdinalIgnoreCase)) return true;
                }
            }
        }
        catch (Exception ex) { AppLog.Write("PnP detection warning: " + ex.GetBaseException().Message); }
        return false;
    }
}
