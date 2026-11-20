using System;
using System.ComponentModel;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Microsoft.Win32.SafeHandles;

namespace MchoseBatteryTray
{
    public sealed class BatteryResult
    {
        public bool Success;
        public bool ResponseReceived;
        public bool HasPercent;
        public bool IsDisconnected;
        public bool IsUnsupportedDevice;
        public bool OutputWritePending;
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

        private static string GetLastWin32ErrorText(string operation)
        {
            int error = Marshal.GetLastWin32Error();
            return operation + " (Win32 " + error + "): " + new Win32Exception(error).Message;
        }

        private static void ObserveAbandonedRead(Task<int> readTask)
        {
            if (readTask == null) return;
            _ = readTask.ContinueWith(
                task => { var ignored = task.Exception; },
                TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously);
        }

        private static bool TryQueryCandidate(DeviceCandidate candidate, out BatteryResult result, int timeoutMs)
        {
            result = new BatteryResult
            {
                Success = false,
                ResponseReceived = false,
                HasPercent = false,
                Detail = "No battery response."
            };

            // Open separate input and output handles for the same HID collection.
            // Keep report reading independent from output-write completion: some HID
            // drivers may delay completion of a WriteFile operation. A matching input
            // report must still be processed while the output operation is pending.
            SafeFileHandle readHandle = CreateFile(
                candidate.Path, GenericRead, ShareRead | ShareWrite, IntPtr.Zero,
                OpenExisting, FileFlagOverlapped, IntPtr.Zero);
            if (readHandle == null || readHandle.IsInvalid)
            {
                string error = GetLastWin32ErrorText("Unable to open HID input handle");
                if (readHandle != null) readHandle.Dispose();
                result.Detail = error;
                return false;
            }

            SafeFileHandle writeHandle = CreateFile(
                candidate.Path, GenericWrite, ShareRead | ShareWrite, IntPtr.Zero,
                OpenExisting, FileFlagOverlapped, IntPtr.Zero);
            if (writeHandle == null || writeHandle.IsInvalid)
            {
                string error = GetLastWin32ErrorText("Unable to open HID output handle");
                if (writeHandle != null) writeHandle.Dispose();
                readHandle.Dispose();
                result.Detail = error;
                return false;
            }

            Task<int> readTask = null;
            Task writeTask = null;
            try
            {
                using (FileStream inputStream = new FileStream(readHandle, FileAccess.Read, 64, true))
                using (FileStream outputStream = new FileStream(writeHandle, FileAccess.Write, 64, true))
                {
                    byte[] output = new byte[candidate.OutputLength];
                    output[0] = candidate.Profile.ReportId;
                    output[1] = candidate.Profile.Command;
                    output[2] = candidate.Profile.QueryCode;

                    // Queue an input read first, then send the request through the separate
                    // output handle. Never wait for WriteAsync before checking the input side:
                    // some HID drivers can leave write completion pending even after the
                    // receiver has already returned a valid input report.
                    byte[] input = new byte[candidate.InputLength];
                    readTask = inputStream.ReadAsync(input, 0, input.Length);
                    writeTask = outputStream.WriteAsync(output, 0, output.Length);

                    DateTime deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
                    bool writeFinished = false;
                    string writeFailure = null;

                    while (true)
                    {
                        int remaining = (int)Math.Max(0, (deadline - DateTime.UtcNow).TotalMilliseconds);
                        if (remaining <= 0)
                        {
                            ObserveAbandonedRead(readTask);
                            ObserveAbandonedWrite(writeTask);
                            if (!writeFinished)
                                result.Detail = "HID output write remained pending and no battery response arrived before timeout.";
                            else if (writeFailure != null)
                                result.Detail = "HID output report failed: " + writeFailure + "; no battery response arrived.";
                            else
                                result.Detail = "No battery reply from MCHOSE headset (timeout).";
                            return false;
                        }

                        Task delayTask = Task.Delay(remaining);
                        Task completedTask = writeFinished
                            ? Task.WhenAny(readTask, delayTask).GetAwaiter().GetResult()
                            : Task.WhenAny(readTask, writeTask, delayTask).GetAwaiter().GetResult();

                        if (!writeFinished && completedTask == writeTask)
                        {
                            writeFinished = true;
                            if (writeTask.IsFaulted)
                                writeFailure = writeTask.Exception.GetBaseException().Message;
                            else if (writeTask.IsCanceled)
                                writeFailure = "The output report write was cancelled.";
                            // Continue listening even if the write reports an error: a valid
                            // receiver response may already be queued on the input handle.
                            continue;
                        }

                        if (completedTask == delayTask)
                            continue; // The deadline check above sets the final diagnostic.

                        if (completedTask != readTask)
                            continue;

                        if (readTask.IsFaulted)
                        {
                            result.Detail = "HID input report failed: " + readTask.Exception.GetBaseException().Message;
                            ObserveAbandonedWrite(writeTask);
                            return false;
                        }

                        if (readTask.IsCanceled)
                        {
                            result.Detail = "HID input report read was cancelled.";
                            ObserveAbandonedWrite(writeTask);
                            return false;
                        }

                        int count = readTask.Result;
                        if (count <= 0)
                        {
                            result.Detail = "HID input stream closed before a battery response.";
                            ObserveAbandonedWrite(writeTask);
                            return false;
                        }

                        // Some legacy receivers push 00 1A when the headset powers off.
                        if (count >= 2 && input[0] == 0x00 && input[1] == 0x1A)
                        {
                            result.IsDisconnected = true;
                            result.ResponseReceived = true;
                            result.Detail = "Receiver reported that the headset is offline (00 1A).";
                            ObserveAbandonedWrite(writeTask);
                            return true;
                        }

                        // Ignore connection notifications and unrelated HID reports.
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
                                ObserveAbandonedWrite(writeTask);
                                return true;
                            }

                            if (state == 0x02 && level <= 100)
                            {
                                result.Success = true;
                                result.HasPercent = true;
                                result.Percent = level;
                                result.OutputWritePending = !writeFinished;
                                result.Detail = writeFinished
                                    ? "Battery report received."
                                    : "Battery report received while HID output completion was still pending.";
                                ObserveAbandonedWrite(writeTask);
                                return true;
                            }

                            if (state == 0x00 || state == 0x03)
                            {
                                result.Success = true;
                                result.HasPercent = false;
                                result.OutputWritePending = !writeFinished;
                                result.Detail = writeFinished
                                    ? "Headset is connected to power; its reported percentage may be stale."
                                    : "Charging state received while HID output completion was still pending.";
                                ObserveAbandonedWrite(writeTask);
                                return true;
                            }

                            result.Detail = "Unexpected battery state: " + state + ", level: " + level + ".";
                            ObserveAbandonedWrite(writeTask);
                            return true;
                        }

                        string reportHex = "";
                        int dumpLength = Math.Min(count, 8);
                        for (int i = 0; i < dumpLength; i++)
                            reportHex += input[i].ToString("X2") + (i + 1 < dumpLength ? " " : "");
                        result.Detail = "Ignored unrelated HID report (" + reportHex + "); waiting for battery reply.";

                        input = new byte[candidate.InputLength];
                        readTask = inputStream.ReadAsync(input, 0, input.Length);
                    }
                }
            }
            catch (Exception ex)
            {
                ObserveAbandonedRead(readTask);
                ObserveAbandonedWrite(writeTask);
                result.Detail = ex.GetBaseException().Message;
                return false;
            }
            finally
            {
                // FileStream disposes the SafeFileHandles. Explicit Dispose is also safe
                // and covers failures before stream construction completes.
                try { readHandle.Dispose(); } catch { }
                try { writeHandle.Dispose(); } catch { }
            }
        }

        private static void ObserveAbandonedWrite(Task writeTask)
        {
            if (writeTask == null) return;
            _ = writeTask.ContinueWith(
                task => { var ignored = task.Exception; },
                TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously);
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
}
