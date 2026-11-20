using System;
using System.Diagnostics;
using System.Drawing;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Win32;

namespace MchoseBatteryTray;

internal sealed class TrayApplicationContext : ApplicationContext
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string RunValueName = "MCHOSE_BatteryTray";
    private const string LegacyRunValueName = "MCHOSE_X9_BatteryTray";

    private readonly AppSettings _settings;
    private readonly NotifyIcon _notifyIcon;
    private readonly ToolStripMenuItem _statusItem;
    private readonly ToolStripMenuItem _refreshItem;
    private readonly ToolStripMenuItem _autoStartItem;
    private readonly ToolStripMenuItem _loggingItem;
    private readonly System.Windows.Forms.Timer _timer;
    private Icon _currentIcon;
    private int _lastGoodPercent = -1;
    private DateTime? _lastGoodAt;
    private int _consecutiveMisses;
    private int _polling;
    private bool _isExiting;

    public TrayApplicationContext()
    {
        _settings = SettingsStore.Load();
        AppLog.Initialize(_settings.LoggingEnabled);
        RemoveLegacyPowerShellStartup();

        _statusItem = new ToolStripMenuItem("Получение данных…") { Enabled = false };
        _refreshItem = new ToolStripMenuItem("Обновить сейчас");
        _autoStartItem = new ToolStripMenuItem("Запускать вместе с Windows") { CheckOnClick = true };
        _loggingItem = new ToolStripMenuItem("Вести диагностический журнал")
        {
            CheckOnClick = true,
            Checked = _settings.LoggingEnabled
        };

        var menu = new ContextMenuStrip();
        menu.Items.Add(_statusItem);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(_refreshItem);
        menu.Items.Add(_autoStartItem);
        menu.Items.Add(_loggingItem);
        var openLogItem = new ToolStripMenuItem("Открыть журнал");
        var clearLogItem = new ToolStripMenuItem("Очистить журнал");
        menu.Items.Add(openLogItem);
        menu.Items.Add(clearLogItem);
        menu.Items.Add(new ToolStripSeparator());
        var exitItem = new ToolStripMenuItem("Выход");
        menu.Items.Add(exitItem);

        _currentIcon = IconFactory.CreateDisconnected(false, IsLightTaskbar());
        _notifyIcon = new NotifyIcon
        {
            Icon = _currentIcon,
            Visible = true,
            Text = "MCHOSE Battery Tray — поиск гарнитуры",
            ContextMenuStrip = menu
        };

        _refreshItem.Click += async (_, _) => await RefreshAsync();
        _autoStartItem.Checked = IsAutoStartEnabled();
        _autoStartItem.Click += (_, _) => ToggleAutoStart(_autoStartItem.Checked);
        _loggingItem.Click += (_, _) => ToggleLogging(_loggingItem.Checked);
        openLogItem.Click += (_, _) => OpenLog();
        clearLogItem.Click += (_, _) => ClearLog();
        exitItem.Click += (_, _) => ExitApplication();
        _notifyIcon.DoubleClick += async (_, _) => await RefreshAsync();

        _timer = new System.Windows.Forms.Timer { Interval = 60_000 };
        _timer.Tick += async (_, _) => await RefreshAsync();
        _timer.Start();

        AppLog.Write("MCHOSE Battery Tray 10.0.1 started; poll interval 60 seconds.");
        // Start the first asynchronous HID query only after the WinForms message loop
        // is active, so its continuation returns to the UI synchronization context.
        Application.Idle += StartInitialPoll;
    }

    private void StartInitialPoll(object sender, EventArgs e)
    {
        Application.Idle -= StartInitialPoll;
        _ = RefreshAsync();
    }

    private async Task RefreshAsync()
    {
        if (_isExiting || Interlocked.Exchange(ref _polling, 1) == 1) return;
        _refreshItem.Enabled = false;
        try
        {
            AppLog.Maintain();
            BatteryResult result = await Task.Run(() =>
            {
                BatteryResult query = HidBatteryReader.Query();
                // An explicit 00 1A offline response takes precedence: audio PnP can linger.
                if (query.IsDisconnected && !query.ResponseReceived && MchoseDeviceDetector.IsPresent())
                {
                    query.IsDisconnected = false;
                    query.IsUnsupportedDevice = true;
                    query.Detail = "A MCHOSE device is present, but its battery protocol is not supported.";
                }
                return query;
            });
            if (_isExiting) return;
            ApplyResult(result);
        }
        catch (Exception ex)
        {
            if (!_isExiting)
            {
                AppLog.Write("Update error: " + ex.GetBaseException());
                _statusItem.Text = "Ошибка чтения заряда — см. журнал";
                _notifyIcon.Text = "MCHOSE Battery Tray: ошибка";
            }
        }
        finally
        {
            Interlocked.Exchange(ref _polling, 0);
            if (!_isExiting) _refreshItem.Enabled = true;
        }
    }

    private void ApplyResult(BatteryResult result)
    {
        string tooltip, status, logState;
        Icon newIcon;
        bool lightTaskbar = IsLightTaskbar();

        if (result.IsDisconnected)
        {
            _consecutiveMisses = 0;
            tooltip = "MCHOSE Battery Tray: не подключены";
            status = "Наушники не подключены";
            logState = "DISCONNECTED: " + result.Detail;
            newIcon = IconFactory.CreateDisconnected(false, lightTaskbar);
        }
        else if (result.IsUnsupportedDevice)
        {
            _consecutiveMisses = 0;
            tooltip = "MCHOSE Battery Tray: протокол не поддерживается";
            status = "Устройство MCHOSE обнаружено, но его протокол пока не поддерживается";
            logState = "UNSUPPORTED: " + result.Detail;
            newIcon = IconFactory.CreateDisconnected(true, lightTaskbar);
        }
        else if (result.Success && result.HasPercent)
        {
            _consecutiveMisses = 0;
            _lastGoodPercent = Math.Clamp(result.Percent, 0, 100);
            _lastGoodAt = DateTime.Now;
            tooltip = $"MCHOSE {result.DeviceName ?? "headset"}: {_lastGoodPercent}%";
            status = $"Заряд: {_lastGoodPercent}% (работает от батареи)";
            logState = $"OK: {_lastGoodPercent}% / state {result.State} / {result.DeviceName}";
            newIcon = IconFactory.CreateBattery(_lastGoodPercent, lightTaskbar);
        }
        else if (result.Success && !result.HasPercent)
        {
            _consecutiveMisses = 0;
            if (_lastGoodPercent >= 0)
            {
                tooltip = $"MCHOSE: зарядка (последнее значение {_lastGoodPercent}%)";
                status = $"Заряжается; последнее подтверждённое значение: {_lastGoodPercent}%";
                newIcon = IconFactory.CreateBattery(_lastGoodPercent, lightTaskbar);
            }
            else
            {
                tooltip = "MCHOSE: зарядка";
                status = "На зарядке; процент сейчас не передаётся";
                newIcon = IconFactory.CreateDisconnected(true, lightTaskbar);
            }
            logState = "CHARGING: state " + result.State;
        }
        else
        {
            _consecutiveMisses++;
            if (_consecutiveMisses >= 2)
            {
                tooltip = "MCHOSE: нет связи";
                status = "Два последовательных запроса без ответа; гарнитура выключена или вне зоны действия";
                newIcon = IconFactory.CreateDisconnected(false, lightTaskbar);
                logState = "OFFLINE_AFTER_TIMEOUTS: " + result.Detail;
            }
            else if (_lastGoodPercent >= 0)
            {
                int ageMinutes = _lastGoodAt.HasValue
                    ? Math.Max(0, (int)(DateTime.Now - _lastGoodAt.Value).TotalMinutes) : 0;
                tooltip = $"MCHOSE: {_lastGoodPercent}% (последнее значение)";
                status = $"Нет ответа; последнее значение: {_lastGoodPercent}% ({ageMinutes} мин назад)";
                newIcon = IconFactory.CreateBattery(_lastGoodPercent, lightTaskbar);
                logState = "STALE: " + result.Detail;
            }
            else
            {
                tooltip = "MCHOSE: ожидание ответа";
                status = "Приёмник найден; ожидаю ответа от наушников";
                newIcon = IconFactory.CreateDisconnected(true, lightTaskbar);
                logState = "WAITING: " + result.Detail;
            }
        }

        if (tooltip.Length > 63) tooltip = tooltip[..63];
        Icon oldIcon = _currentIcon;
        _currentIcon = newIcon;
        _notifyIcon.Icon = newIcon;
        _notifyIcon.Text = tooltip;
        _statusItem.Text = status;
        oldIcon?.Dispose();
        AppLog.Write("POLL: " + logState);
    }

    private void ToggleLogging(bool enabled)
    {
        _settings.LoggingEnabled = enabled;
        try
        {
            SettingsStore.Save(_settings);
            AppLog.SetEnabled(enabled);
            if (enabled) AppLog.Write("Diagnostic logging enabled.");
        }
        catch (Exception ex)
        {
            _loggingItem.Checked = !enabled;
            MessageBox.Show("Не удалось сохранить настройку журнала: " + ex.Message,
                "MCHOSE Battery Tray", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private static bool IsAutoStartEnabled()
    {
        try
        {
            using RegistryKey key = Registry.CurrentUser.OpenSubKey(RunKeyPath, false);
            return key?.GetValue(RunValueName) != null;
        }
        catch { return false; }
    }

    private void ToggleAutoStart(bool enabled)
    {
        try
        {
            using RegistryKey key = Registry.CurrentUser.CreateSubKey(RunKeyPath);
            if (enabled)
            {
                string exe = Application.ExecutablePath;
                key.SetValue(RunValueName, $"\"{exe}\" --background", RegistryValueKind.String);
                AppLog.Write("Automatic startup enabled.");
            }
            else
            {
                key.DeleteValue(RunValueName, false);
                AppLog.Write("Automatic startup disabled.");
            }
        }
        catch (Exception ex)
        {
            _autoStartItem.Checked = !enabled;
            AppLog.Write("Autostart error: " + ex.GetBaseException().Message);
            MessageBox.Show("Не удалось изменить автозапуск: " + ex.Message,
                "MCHOSE Battery Tray", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private static void RemoveLegacyPowerShellStartup()
    {
        try
        {
            using RegistryKey key = Registry.CurrentUser.OpenSubKey(RunKeyPath, true);
            key?.DeleteValue(LegacyRunValueName, false);
        }
        catch { }
    }

    private static void OpenLog()
    {
        try
        {
            System.IO.Directory.CreateDirectory(SettingsStore.LogDirectory);
            if (!System.IO.File.Exists(SettingsStore.LogPath))
                System.IO.File.WriteAllText(SettingsStore.LogPath, string.Empty);
            Process.Start(new ProcessStartInfo("notepad.exe", $"\"{SettingsStore.LogPath}\"")
            { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            MessageBox.Show("Не удалось открыть журнал: " + ex.Message,
                "MCHOSE Battery Tray", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private static void ClearLog() => AppLog.Clear();

    private static bool IsLightTaskbar()
    {
        try
        {
            using RegistryKey key = Registry.CurrentUser.OpenSubKey(
                @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize", false);
            return Convert.ToInt32(key?.GetValue("SystemUsesLightTheme", 0) ?? 0) == 1;
        }
        catch { return false; }
    }

    private void ExitApplication()
    {
        if (_isExiting) return;
        _isExiting = true;
        _timer.Stop();
        _timer.Dispose();
        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
        _currentIcon?.Dispose();
        ExitThread();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing && !_isExiting) ExitApplication();
        base.Dispose(disposing);
    }
}
