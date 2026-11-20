﻿$ErrorActionPreference = 'Stop'

$script:AppDir = $PSScriptRoot
$script:DataDir = Join-Path $env:LOCALAPPDATA 'MCHOSE_X9_BatteryTray'
$script:LogPath = Join-Path $script:DataDir 'tray-v9.log'
New-Item -ItemType Directory -Path $script:DataDir -Force | Out-Null

function Write-Log {
    param([string]$Message)
    try {
        $stamp = Get-Date -Format 'yyyy-MM-dd HH:mm:ss'
        Add-Content -LiteralPath $script:LogPath -Encoding UTF8 -Value ('[{0}] {1}' -f $stamp, $Message)
    } catch { }
}

$mutex = New-Object System.Threading.Mutex($false, 'Local\MCHOSE_X9_BatteryTray')
if (-not $mutex.WaitOne(0)) { exit }

try {
    Add-Type -AssemblyName System.Windows.Forms
    Add-Type -AssemblyName System.Drawing

    $source = @'
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Microsoft.Win32.SafeHandles;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;

namespace MchoseX9Tray
{
    public sealed class BatteryResult
    {
        public bool Success;
        public bool ResponseReceived;
        public bool HasPercent;
        public bool IsDisconnected;
        public bool IsUnsupportedDevice;
        public int Percent;
        public byte State;
        public string DeviceName;
        public string Detail;
    }

    public static class HidBatteryReader
    {
        private const ushort LegacyUsagePage = 0xFF90;
        private const ushort LegacyUsage = 0x0001;
        private const byte LegacyReportId = 0x55;
        private const byte LegacyBatteryCommand = 0x65;
        private const byte LegacyQueryCode = 0x01;

        private sealed class DeviceProfile
        {
            public ushort Vid;
            public ushort Pid;
            public ushort UsagePage;
            public ushort Usage;
            public byte ReportId;
            public byte Command;
            public byte QueryCode;
            public string Name;
        }

        private static DeviceProfile GetProfile(ushort vid, ushort pid)
        {
            // All entries below use the same community-documented legacy headset
            // battery exchange: HID FF90:0001, output 55 65 01, reply 55 65 <level> <state>.
            // Keep the match strict: never query an unknown USB product ID.
            string name = null;

            if (vid == 0x3837 && pid == 0x6045) name = "MCHOSE X9";
            else if (vid == 0x291D && pid == 0x385D) name = "MCHOSE V9 Pro";
            else if (vid == 0x41E4 && pid == 0x3201) name = "MCHOSE V9 family";
            else if (vid == 0x41E4 && pid == 0x3202) name = "MCHOSE V9 family";
            else if (vid == 0x41E4 && pid == 0x3502) name = "MCHOSE G9 Pro";
            else if (vid == 0x41E4 && pid == 0x3402) name = "MCHOSE S9 Pro";
            else if (vid == 0x3837 && (pid == 0x600D || pid == 0x600A || pid == 0x6009)) name = "MCHOSE V9 Turbo family";

            if (name == null) return null;
            return new DeviceProfile {
                Vid = vid, Pid = pid,
                UsagePage = LegacyUsagePage, Usage = LegacyUsage,
                ReportId = LegacyReportId, Command = LegacyBatteryCommand,
                QueryCode = LegacyQueryCode, Name = name
            };
        }

        private static bool IsMchoseVendor(ushort vid)
        {
            // VIDs observed in the documented MCHOSE headset receiver families.
            return vid == 0x3837 || vid == 0x41E4 || vid == 0x291D;
        }
        private const int DigcfPresent = 0x00000002;
        private const int DigcfDeviceInterface = 0x00000010;
        private const uint GenericRead = 0x80000000;
        private const uint GenericWrite = 0x40000000;
        private const uint ShareRead = 0x00000001;
        private const uint ShareWrite = 0x00000002;
        private const uint OpenExisting = 3;
        private const uint FileFlagOverlapped = 0x40000000;

        [StructLayout(LayoutKind.Sequential)]
        private struct SpDeviceInterfaceData
        {
            public int cbSize;
            public Guid InterfaceClassGuid;
            public int Flags;
            public IntPtr Reserved;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct HidAttributes
        {
            public int Size;
            public ushort VendorID;
            public ushort ProductID;
            public ushort VersionNumber;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct HidpCaps
        {
            public ushort Usage;
            public ushort UsagePage;
            public ushort InputReportByteLength;
            public ushort OutputReportByteLength;
            public ushort FeatureReportByteLength;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 17)]
            public ushort[] Reserved;
            public ushort NumberLinkCollectionNodes;
            public ushort NumberInputButtonCaps;
            public ushort NumberInputValueCaps;
            public ushort NumberInputDataIndices;
            public ushort NumberOutputButtonCaps;
            public ushort NumberOutputValueCaps;
            public ushort NumberOutputDataIndices;
            public ushort NumberFeatureButtonCaps;
            public ushort NumberFeatureValueCaps;
            public ushort NumberFeatureDataIndices;
        }

        [DllImport("hid.dll")]
        private static extern void HidD_GetHidGuid(out Guid hidGuid);
        [DllImport("hid.dll", SetLastError = true)]
        private static extern bool HidD_GetAttributes(SafeFileHandle handle, ref HidAttributes attributes);
        [DllImport("hid.dll", SetLastError = true)]
        private static extern bool HidD_GetPreparsedData(SafeFileHandle handle, out IntPtr preparsedData);
        [DllImport("hid.dll", SetLastError = true)]
        private static extern bool HidD_FreePreparsedData(IntPtr preparsedData);
        [DllImport("hid.dll")]
        private static extern int HidP_GetCaps(IntPtr preparsedData, out HidpCaps capabilities);

        [DllImport("setupapi.dll", SetLastError = true)]
        private static extern IntPtr SetupDiGetClassDevs(ref Guid classGuid, IntPtr enumerator, IntPtr hwndParent, int flags);
        [DllImport("setupapi.dll", SetLastError = true)]
        private static extern bool SetupDiEnumDeviceInterfaces(IntPtr deviceInfoSet, IntPtr deviceInfoData, ref Guid interfaceClassGuid, int memberIndex, ref SpDeviceInterfaceData deviceInterfaceData);
        [DllImport("setupapi.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool SetupDiGetDeviceInterfaceDetail(IntPtr deviceInfoSet, ref SpDeviceInterfaceData deviceInterfaceData, IntPtr detailData, int detailDataSize, ref int requiredSize, IntPtr deviceInfoData);
        [DllImport("setupapi.dll", SetLastError = true)]
        private static extern bool SetupDiDestroyDeviceInfoList(IntPtr deviceInfoSet);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern SafeFileHandle CreateFile(string fileName, uint desiredAccess, uint shareMode, IntPtr securityAttributes, uint creationDisposition, uint flagsAndAttributes, IntPtr templateFile);

        private sealed class DeviceCandidate
        {
            public string Path;
            public DeviceProfile Profile;
            public ushort InputLength;
            public ushort OutputLength;
        }

        private static List<DeviceCandidate> FindCandidates(out bool sawMchoseHeadsetCollection)
        {
            List<DeviceCandidate> found = new List<DeviceCandidate>();
            sawMchoseHeadsetCollection = false;
            Guid hidGuid;
            HidD_GetHidGuid(out hidGuid);
            IntPtr set = SetupDiGetClassDevs(ref hidGuid, IntPtr.Zero, IntPtr.Zero, DigcfPresent | DigcfDeviceInterface);
            if (set == new IntPtr(-1)) return found;
            try
            {
                for (int i = 0; i < 512; i++)
                {
                    SpDeviceInterfaceData data = new SpDeviceInterfaceData();
                    data.cbSize = Marshal.SizeOf(typeof(SpDeviceInterfaceData));
                    if (!SetupDiEnumDeviceInterfaces(set, IntPtr.Zero, ref hidGuid, i, ref data)) break;
                    int required = 0;
                    SetupDiGetDeviceInterfaceDetail(set, ref data, IntPtr.Zero, 0, ref required, IntPtr.Zero);
                    if (required <= 0) continue;
                    IntPtr detail = Marshal.AllocHGlobal(required);
                    try
                    {
                        Marshal.WriteInt32(detail, IntPtr.Size == 8 ? 8 : 6);
                        if (!SetupDiGetDeviceInterfaceDetail(set, ref data, detail, required, ref required, IntPtr.Zero)) continue;
                        string path = Marshal.PtrToStringUni(IntPtr.Add(detail, 4));
                        if (String.IsNullOrEmpty(path)) continue;
                        SafeFileHandle handle = CreateFile(path, GenericRead | GenericWrite, ShareRead | ShareWrite, IntPtr.Zero, OpenExisting, FileFlagOverlapped, IntPtr.Zero);
                        if (handle == null || handle.IsInvalid) { if (handle != null) handle.Dispose(); continue; }
                        try
                        {
                            HidAttributes attr = new HidAttributes();
                            attr.Size = Marshal.SizeOf(typeof(HidAttributes));
                            if (!HidD_GetAttributes(handle, ref attr)) continue;
                            DeviceProfile profile = GetProfile(attr.VendorID, attr.ProductID);
                            if (profile == null && !IsMchoseVendor(attr.VendorID)) continue;

                            IntPtr pp;
                            if (!HidD_GetPreparsedData(handle, out pp)) continue;
                            try
                            {
                                HidpCaps caps;
                                int status = HidP_GetCaps(pp, out caps);
                                if (status != 0x00110000) continue;

                                // FF90:0001 is the legacy MCHOSE headset battery collection.
                                // FF07:0222 is used by V9 Pro 2, but its separate parser is not
                                // implemented here yet; flag it as recognised but unsupported.
                                if (IsMchoseVendor(attr.VendorID) &&
                                    (caps.UsagePage == 0xFF90 || caps.UsagePage == 0xFF07))
                                    sawMchoseHeadsetCollection = true;

                                if (profile == null) continue;
                                if (caps.UsagePage != profile.UsagePage || caps.Usage != profile.Usage) continue;
                                if (caps.InputReportByteLength < 4 || caps.OutputReportByteLength < 3) continue;
                                found.Add(new DeviceCandidate { Path = path, Profile = profile, InputLength = caps.InputReportByteLength, OutputLength = caps.OutputReportByteLength });
                            }
                            finally { HidD_FreePreparsedData(pp); }
                        }
                        finally { handle.Dispose(); }
                    }
                    finally { Marshal.FreeHGlobal(detail); }
                }
            }
            finally { SetupDiDestroyDeviceInfoList(set); }
            return found;
        }

        private static bool TryQueryCandidate(DeviceCandidate candidate, out BatteryResult result, int timeoutMs)
        {
            result = new BatteryResult();
            result.Success = false;
            result.ResponseReceived = false;
            result.HasPercent = false;
            result.Detail = "No battery response.";

            SafeFileHandle handle = CreateFile(candidate.Path, GenericRead | GenericWrite, ShareRead | ShareWrite, IntPtr.Zero, OpenExisting, FileFlagOverlapped, IntPtr.Zero);
            if (handle == null || handle.IsInvalid)
            {
                if (handle != null) handle.Dispose();
                result.Detail = "Unable to open matching HID collection.";
                return false;
            }

            try
            {
                using (FileStream stream = new FileStream(handle, FileAccess.ReadWrite, 64, true))
                {
                    byte[] output = new byte[candidate.OutputLength];
                    output[0] = candidate.Profile.ReportId;
                    output[1] = candidate.Profile.Command;
                    output[2] = candidate.Profile.QueryCode;

                    // Start listening before sending the request so an immediate receiver reply is not missed.
                    byte[] input = new byte[candidate.InputLength];
                    Task<int> readTask = stream.ReadAsync(input, 0, input.Length);
                    Task writeTask = stream.WriteAsync(output, 0, output.Length);

                    try
                    {
                        if (!writeTask.Wait(1200))
                        {
                            result.Detail = "HID output report write timed out.";
                            try { stream.Dispose(); } catch { }
                            return false;
                        }
                    }
                    catch (Exception ex)
                    {
                        result.Detail = "HID output report failed: " + ex.GetBaseException().Message;
                        return false;
                    }

                    DateTime deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
                    while (true)
                    {
                        int remaining = (int)Math.Max(0, (deadline - DateTime.UtcNow).TotalMilliseconds);
                        if (remaining <= 0)
                        {
                            result.Detail = "No battery reply from MCHOSE headset (timeout).";
                            try { stream.Dispose(); } catch { }
                            return false;
                        }

                        bool completed;
                        try { completed = readTask.Wait(remaining); }
                        catch (Exception ex)
                        {
                            result.Detail = "HID input report failed: " + ex.GetBaseException().Message;
                            return false;
                        }

                        if (!completed)
                        {
                            result.Detail = "No battery reply from MCHOSE headset (timeout).";
                            try { stream.Dispose(); } catch { }
                            return false;
                        }

                        if (readTask.IsFaulted)
                        {
                            result.Detail = "HID input report failed: " + readTask.Exception.GetBaseException().Message;
                            return false;
                        }

                        int count = readTask.Result;
                        if (count <= 0)
                        {
                            result.Detail = "HID input stream closed before a battery response.";
                            return false;
                        }

                        // Some legacy receivers push 00 1A when the headset powers off.
                        // Treat that documented event as an explicit offline indication.
                        if (count >= 2 && input[0] == 0x00 && input[1] == 0x1A)
                        {
                            result.IsDisconnected = true;
                            result.ResponseReceived = true;
                            result.Detail = "Receiver reported that the headset is offline (00 1A).";
                            return true;
                        }

                        // The receiver may send a connection notification (55 11) or another event
                        // before the requested battery response. Ignore unrelated reports.
                        if (count >= 4 && input[0] == candidate.Profile.ReportId && input[1] == candidate.Profile.Command)
                        {
                            int level = input[2];
                            byte state = input[3];
                            result.ResponseReceived = true;
                            result.State = state;
                            result.DeviceName = candidate.Profile.Name;

                            if (state == 0x1A)
                            {
                                result.IsDisconnected = true;
                                result.Detail = "The headset is off or asleep; the receiver reports it as offline.";
                                return true;
                            }

                            // The MCHOSE protocol reports the percent reliably while on battery (state 0x02).
                            // On cable/charging (states 0x00/0x03), the level may be stale, so do not publish it.
                            if (state == 0x02 && level <= 100)
                            {
                                result.Success = true;
                                result.HasPercent = true;
                                result.Percent = level;
                                result.Detail = "Battery report received.";
                                return true;
                            }

                            if (state == 0x00 || state == 0x03)
                            {
                                result.Success = true;
                                result.HasPercent = false;
                                result.Detail = "Headset is connected to power; its reported percentage may be stale.";
                                return true;
                            }

                            result.Detail = "Unexpected battery state: " + state + ", level: " + level + ".";
                            return true;
                        }

                        string reportHex = "";
                        int dumpLength = Math.Min(count, 8);
                        for (int i = 0; i < dumpLength; i++)
                            reportHex += input[i].ToString("X2") + (i + 1 < dumpLength ? " " : "");
                        result.Detail = "Ignored unrelated HID report (" + reportHex + "); waiting for battery reply.";

                        input = new byte[candidate.InputLength];
                        readTask = stream.ReadAsync(input, 0, input.Length);
                    }
                }
            }
            catch (Exception ex)
            {
                result.Detail = ex.GetBaseException().Message;
                return false;
            }
            finally
            {
                handle.Dispose();
            }
        }

        public static BatteryResult Query()
        {
            BatteryResult result = new BatteryResult();
            result.Success = false;
            result.ResponseReceived = false;
            result.HasPercent = false;
            result.Detail = "No matching supported MCHOSE headset receiver found.";

            bool sawMchoseHeadsetCollection = false;
            List<DeviceCandidate> candidates = FindCandidates(out sawMchoseHeadsetCollection);
            if (candidates.Count == 0)
            {
                result.IsUnsupportedDevice = sawMchoseHeadsetCollection;
                result.IsDisconnected = !sawMchoseHeadsetCollection;
                result.Detail = sawMchoseHeadsetCollection
                    ? "A MCHOSE headset HID collection was detected, but its receiver ID/protocol is not supported yet."
                    : "No supported MCHOSE headset receiver is connected.";
                return result;
            }

            foreach (DeviceCandidate candidate in candidates)
            {
                // A single retry helps with an occasional missed USB/HID transaction.
                for (int attempt = 1; attempt <= 2; attempt++)
                {
                    BatteryResult current;
                    bool gotResponse = TryQueryCandidate(candidate, out current, 1400);
                    current.DeviceName = candidate.Profile.Name;
                    result = current;
                    if (gotResponse && (current.ResponseReceived || current.Success)) return current;
                }
            }
            return result;
        }
    }

    public static class IconFactory
    {
        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool DestroyIcon(IntPtr handle);

        private static Font CreateClockFont(float size)
        {
            // Bahnschrift is included with Windows 11. Its DIN-inspired,
            // geometric numerals are designed for quick recognition at small sizes.
            // Use a firm but not extra-heavy weight; fall back to the Windows UI font.
            try
            {
                return new Font("Bahnschrift", size, FontStyle.Bold, GraphicsUnit.Pixel);
            }
            catch
            {
                try
                {
                    return new Font("Segoe UI Semibold", size, FontStyle.Regular, GraphicsUnit.Pixel);
                }
                catch
                {
                    return new Font("Segoe UI", size, FontStyle.Bold, GraphicsUnit.Pixel);
                }
            }
        }

        public static Icon Create(int percent, bool connected, bool unsupported, bool lightTaskbar)
        {
            using (Bitmap bitmap = new Bitmap(32, 32, System.Drawing.Imaging.PixelFormat.Format32bppArgb))
            using (Graphics g = Graphics.FromImage(bitmap))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                g.Clear(Color.Transparent);

                // Keep ordinary percentages numeric. At exactly 100%, use F+
                // as a compact two-character "Full" marker so the icon never
                // has to shrink three digits into the 32x32 notification area.
                string digits = unsupported ? "?" : "×";
                if (connected)
                {
                    int safePercent = Math.Max(0, Math.Min(100, percent));
                    digits = (safePercent == 100) ? "F+" : safePercent.ToString();
                }
                // Bahnschrift is naturally clear and geometric. F+ and two-digit
                // values use nearly the full available area; a single digit gets
                // extra size while remaining vertically centered.
                float numberSize = digits.Length == 1 ? 30.5f : 28.5f;
                Color ink = lightTaskbar ? Color.FromArgb(24, 24, 24) : Color.FromArgb(245, 245, 245);

                using (Brush textBrush = new SolidBrush(ink))
                using (StringFormat format = (StringFormat)StringFormat.GenericTypographic.Clone())
                {
                    format.Alignment = StringAlignment.Center;
                    format.LineAlignment = StringAlignment.Center;
                    format.FormatFlags |= StringFormatFlags.NoWrap;

                    using (Font numberFont = CreateClockFont(numberSize))
                    {
                        SizeF extent = g.MeasureString(digits, numberFont, 1000, format);
                        float scale = 1.0f;
                        if (extent.Width > 30.0f) scale = Math.Min(scale, 30.0f / extent.Width);
                        if (extent.Height > 30.0f) scale = Math.Min(scale, 30.0f / extent.Height);

                        if (scale < 0.999f)
                        {
                            using (Font fittedFont = CreateClockFont(numberSize * scale))
                            {
                                RectangleF rect = new RectangleF(0.0f, 0.0f, 32.0f, 32.0f);
                                g.DrawString(digits, fittedFont, textBrush, rect, format);
                            }
                        }
                        else
                        {
                            RectangleF rect = new RectangleF(0.0f, 0.0f, 32.0f, 32.0f);
                            g.DrawString(digits, numberFont, textBrush, rect, format);
                        }
                    }
                }

                IntPtr hIcon = bitmap.GetHicon();
                try
                {
                    using (Icon temp = Icon.FromHandle(hIcon))
                        return (Icon)temp.Clone();
                }
                finally { DestroyIcon(hIcon); }
            }
        }
    }
}
'@

    Add-Type -TypeDefinition $source -Language CSharp -ReferencedAssemblies @('System.dll', 'System.Core.dll', 'System.Drawing.dll')

    function Get-LightTaskbarTheme {
        try {
            $theme = Get-ItemProperty -LiteralPath 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize' -Name SystemUsesLightTheme -ErrorAction Stop
            return ([int]$theme.SystemUsesLightTheme -eq 1)
        } catch {
            # Dark foreground is the safer default for the usual dark Windows taskbar.
            return $false
        }
    }

    $script:notify = New-Object System.Windows.Forms.NotifyIcon
    $script:currentIcon = [MchoseX9Tray.IconFactory]::Create(0, $false, $false, [bool](Get-LightTaskbarTheme))
    $script:notify.Icon = $script:currentIcon
    $script:notify.Visible = $true
    $script:notify.Text = 'MCHOSE: поиск гарнитуры'

    $script:menu = New-Object System.Windows.Forms.ContextMenuStrip
    $script:statusItem = New-Object System.Windows.Forms.ToolStripMenuItem('MCHOSE: поиск гарнитуры')
    $script:statusItem.Enabled = $false
    [void]$script:menu.Items.Add($script:statusItem)
    [void]$script:menu.Items.Add((New-Object System.Windows.Forms.ToolStripSeparator))

    $refreshItem = New-Object System.Windows.Forms.ToolStripMenuItem('Обновить сейчас')
    $refreshItem.add_Click({ Update-BatteryStatus })
    [void]$script:menu.Items.Add($refreshItem)

    $runKeyPath = 'Software\Microsoft\Windows\CurrentVersion\Run'
    $runValueName = 'MCHOSE_X9_BatteryTray'
    $autoItem = New-Object System.Windows.Forms.ToolStripMenuItem('Запускать вместе с Windows')
    $autoItem.CheckOnClick = $true
    try {
        $runKey = [Microsoft.Win32.Registry]::CurrentUser.OpenSubKey($runKeyPath, $false)
        if ($runKey) {
            $autoItem.Checked = ($null -ne $runKey.GetValue($runValueName, $null))
            $runKey.Close()
        }
    } catch { }
    $autoItem.add_Click({
        try {
            $key = [Microsoft.Win32.Registry]::CurrentUser.CreateSubKey($runKeyPath)
            if ($autoItem.Checked) {
                $wscript = Join-Path $env:WINDIR 'System32\wscript.exe'
                $vbs = Join-Path $script:AppDir 'start.vbs'
                $command = '"{0}" "{1}"' -f $wscript, $vbs
                $key.SetValue($runValueName, $command, [Microsoft.Win32.RegistryValueKind]::String)
                Write-Log 'Automatic startup enabled.'
            } else {
                $key.DeleteValue($runValueName, $false)
                Write-Log 'Automatic startup disabled.'
            }
            $key.Close()
        } catch {
            Write-Log ('Autostart error: ' + $_.Exception.Message)
            [System.Windows.Forms.MessageBox]::Show('Не удалось изменить автозапуск: ' + $_.Exception.Message, 'MCHOSE Battery Tray', 'OK', 'Warning') | Out-Null
        }
    })
    [void]$script:menu.Items.Add($autoItem)

    $openLogItem = New-Object System.Windows.Forms.ToolStripMenuItem('Открыть журнал')
    $openLogItem.add_Click({
        if (-not (Test-Path $script:LogPath)) { New-Item -ItemType File -Path $script:LogPath -Force | Out-Null }
        Start-Process -FilePath 'notepad.exe' -ArgumentList @($script:LogPath)
    })
    [void]$script:menu.Items.Add($openLogItem)

    [void]$script:menu.Items.Add((New-Object System.Windows.Forms.ToolStripSeparator))
    $exitItem = New-Object System.Windows.Forms.ToolStripMenuItem('Выход')
    $exitItem.add_Click({
        $script:timer.Stop()
        $script:notify.Visible = $false
        $script:notify.Dispose()
        if ($script:currentIcon) { $script:currentIcon.Dispose() }
        [System.Windows.Forms.Application]::Exit()
    })
    [void]$script:menu.Items.Add($exitItem)
    $script:notify.ContextMenuStrip = $script:menu

    $script:lastGoodPercent = -1
    $script:lastGoodAt = $null
    $script:consecutiveMisses = 0

    function Test-MchoseAudioEndpointPresent {
        # Some MCHOSE receivers use product IDs that are not yet mapped by this app.
        # If Windows still exposes an MCHOSE audio endpoint, report 'unknown/unsupported'
        # instead of incorrectly showing the disconnected symbol.
        try {
            $devices = @(Get-PnpDevice -PresentOnly -ErrorAction Stop | Where-Object {
                $_.FriendlyName -match '(?i)MCHOSE' -and
                ($_.Class -eq 'MEDIA' -or $_.Class -eq 'AudioEndpoint')
            })
            return ($devices.Count -gt 0)
        } catch {
            return $false
        }
    }

    function Update-BatteryStatus {
        try {
            $result = [MchoseX9Tray.HidBatteryReader]::Query()
            if ($result.IsDisconnected -and (Test-MchoseAudioEndpointPresent)) {
                $result.IsDisconnected = $false
                $result.IsUnsupportedDevice = $true
                $result.Detail = 'A MCHOSE audio endpoint is present, but no supported battery HID profile was found.'
            }
            if ($result.IsDisconnected) {
                $script:consecutiveMisses = 0
                $text = 'MCHOSE: не подключены'
                $status = 'Наушники не подключены'
                $logState = 'DISCONNECTED:' + $result.Detail
                $newIcon = [MchoseX9Tray.IconFactory]::Create(0, $false, $false, [bool](Get-LightTaskbarTheme))
            } elseif ($result.IsUnsupportedDevice) {
                $script:consecutiveMisses = 0
                $text = 'MCHOSE: не поддерживается'
                $status = 'Обнаружено устройство MCHOSE, но его протокол пока не поддерживается'
                $logState = 'UNSUPPORTED:' + $result.Detail
                $newIcon = [MchoseX9Tray.IconFactory]::Create(0, $false, $true, [bool](Get-LightTaskbarTheme))
            } elseif ($result.Success -and $result.HasPercent) {
                $script:consecutiveMisses = 0
                $percent = [int]$result.Percent
                $script:lastGoodPercent = $percent
                $script:lastGoodAt = Get-Date
                $deviceLabel = if ($result.DeviceName) { $result.DeviceName } else { 'MCHOSE headset' }
                $text = '{0}: {1}%' -f $deviceLabel, $percent
                $status = 'Заряд: {0}% (работает от батареи)' -f $percent
                $logState = 'OK:' + $percent + ':' + $result.State + ':' + $deviceLabel
                $newIcon = [MchoseX9Tray.IconFactory]::Create($percent, $true, $false, [bool](Get-LightTaskbarTheme))
            } elseif ($result.Success -and -not $result.HasPercent) {
                $script:consecutiveMisses = 0
                if ($script:lastGoodPercent -ge 0) {
                    $text = 'MCHOSE: зарядка (посл. {0}%)' -f $script:lastGoodPercent
                    $status = 'Заряжается; последнее подтверждённое значение: {0}%' -f $script:lastGoodPercent
                    $newIcon = [MchoseX9Tray.IconFactory]::Create($script:lastGoodPercent, $true, $false, [bool](Get-LightTaskbarTheme))
                } else {
                    $text = 'MCHOSE: зарядка'
                    $status = 'На зарядке; процент сейчас не передаётся'
                    $newIcon = [MchoseX9Tray.IconFactory]::Create(0, $false, $true, [bool](Get-LightTaskbarTheme))
                }
                $logState = 'CHARGING:' + $result.State
            } else {
                $script:consecutiveMisses++
                if ($script:consecutiveMisses -ge 2) {
                    $text = 'MCHOSE: нет связи'
                    $status = 'Нет ответа от гарнитуры в двух последовательных проверках; возможно, она выключена или вне зоны действия'
                    $newIcon = [MchoseX9Tray.IconFactory]::Create(0, $false, $false, [bool](Get-LightTaskbarTheme))
                    $logState = 'OFFLINE_AFTER_TIMEOUTS:' + $result.Detail
                } elseif ($script:lastGoodPercent -ge 0) {
                    $ageMinutes = [int][Math]::Floor(((Get-Date) - $script:lastGoodAt).TotalMinutes)
                    $text = 'MCHOSE: {0}% (последнее значение)' -f $script:lastGoodPercent
                    $status = 'Нет ответа; последнее значение: {0}% ({1} мин назад)' -f $script:lastGoodPercent, $ageMinutes
                    $newIcon = [MchoseX9Tray.IconFactory]::Create($script:lastGoodPercent, $true, $false, [bool](Get-LightTaskbarTheme))
                    $logState = 'STALE:' + $result.Detail
                } else {
                    $text = 'MCHOSE: ожидаю ответа'
                    $status = 'Приёмник найден; ожидаю ответа от наушников'
                    $newIcon = [MchoseX9Tray.IconFactory]::Create(0, $false, $true, [bool](Get-LightTaskbarTheme))
                    $logState = 'WAITING:' + $result.Detail
                }
            }

            if ($text.Length -gt 63) { $text = $text.Substring(0, 63) }
            $oldIcon = $script:currentIcon
            $script:currentIcon = $newIcon
            $script:notify.Icon = $newIcon
            $script:notify.Text = $text
            $script:statusItem.Text = $status
            if ($oldIcon) { $oldIcon.Dispose() }
            Write-Log ('POLL: ' + $logState)
        } catch {
            Write-Log ('Update error: ' + $_.Exception.ToString())
            $script:statusItem.Text = 'Ошибка чтения заряда — см. журнал'
            $script:notify.Text = 'MCHOSE: ошибка чтения'
        }
    }

    Write-Log 'MCHOSE Battery Tray v9 started; poll interval 60 seconds.'
    Update-BatteryStatus
    $script:timer = New-Object System.Windows.Forms.Timer
    $script:timer.Interval = 60000
    $script:timer.add_Tick({ Update-BatteryStatus })
    $script:timer.Start()
    [System.Windows.Forms.Application]::Run()
}
catch {
    Write-Log ('FATAL: ' + $_.Exception.ToString())
    throw
}
finally {
    try { if ($script:notify) { $script:notify.Visible = $false; $script:notify.Dispose() } } catch { }
    try { if ($script:currentIcon) { $script:currentIcon.Dispose() } } catch { }
    try { $mutex.ReleaseMutex() } catch { }
    try { $mutex.Dispose() } catch { }
}
