using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

namespace MchoseBatteryTray;

internal static class Program
{
    private const string AppUserModelId = "MCHOSE.BatteryTray";
    private const string InstanceMutexName = @"Local\MCHOSE.BatteryTray.Instance";

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern int SetCurrentProcessExplicitAppUserModelID(string appId);

    [STAThread]
    private static void Main(string[] args)
    {
        try { SetCurrentProcessExplicitAppUserModelID(AppUserModelId); } catch { }
        using var mutex = new Mutex(true, InstanceMutexName, out bool createdNew);
        if (!createdNew) return;
        try
        {
            ApplicationConfiguration.Initialize();
            Application.Run(new TrayApplicationContext());
        }
        catch (Exception ex)
        {
            try
            {
                MessageBox.Show("MCHOSE Battery Tray could not start:\r\n\r\n" + ex,
                    "MCHOSE Battery Tray", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            catch { }
        }
        finally { try { mutex.ReleaseMutex(); } catch { } }
    }
}
