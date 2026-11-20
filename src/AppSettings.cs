using System;
using System.IO;
using System.Text.Json;

namespace MchoseBatteryTray;

internal sealed class AppSettings
{
    public bool LoggingEnabled { get; set; } = true;
}

internal static class SettingsStore
{
    public static readonly string RootDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "MCHOSE", "Battery Tray");
    public static readonly string LogDirectory = Path.Combine(RootDirectory, "Logs");
    public static readonly string LogPath = Path.Combine(LogDirectory, "app.log");
    private static readonly string SettingsPath = Path.Combine(RootDirectory, "settings.json");

    public static AppSettings Load()
    {
        try
        {
            Directory.CreateDirectory(RootDirectory);
            if (File.Exists(SettingsPath))
            {
                var loaded = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(SettingsPath));
                if (loaded != null) return loaded;
            }
        }
        catch { }
        return new AppSettings();
    }

    public static void Save(AppSettings settings)
    {
        Directory.CreateDirectory(RootDirectory);
        string temporaryPath = SettingsPath + ".tmp";
        string json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(temporaryPath, json);
        if (File.Exists(SettingsPath))
        {
            try { File.Replace(temporaryPath, SettingsPath, null); }
            catch
            {
                File.Delete(SettingsPath);
                File.Move(temporaryPath, SettingsPath);
            }
        }
        else File.Move(temporaryPath, SettingsPath);
    }
}

internal static class AppLog
{
    public const int MaximumLines = 500;
    public const int MaximumMessageCharacters = 4096;
    public const int RetentionDays = 14;
    private static readonly object Sync = new();
    private static bool _enabled = true;
    private static int _writesSinceCleanup;
    private static DateTime _lastMaintenance = DateTime.MinValue;
    private static readonly System.Text.UTF8Encoding Utf8NoBom = new(false);

    public static bool Enabled { get { lock (Sync) return _enabled; } }

    public static void Initialize(bool enabled)
    {
        lock (Sync)
        {
            _enabled = enabled;
            Directory.CreateDirectory(SettingsStore.LogDirectory);
            CleanupOldLogs();
            TrimCurrentLog();
            _lastMaintenance = DateTime.Now;
        }
    }

    public static void Maintain()
    {
        lock (Sync)
        {
            if ((DateTime.Now - _lastMaintenance).TotalHours < 12) return;
            CleanupOldLogs();
            TrimCurrentLog();
            _lastMaintenance = DateTime.Now;
        }
    }

    public static void SetEnabled(bool enabled)
    {
        lock (Sync) _enabled = enabled;
    }

    public static void Write(string message)
    {
        lock (Sync)
        {
            if (!_enabled) return;
            try
            {
                Directory.CreateDirectory(SettingsStore.LogDirectory);
                string safeMessage = (message ?? string.Empty).Replace('\r', ' ').Replace('\n', ' ');
                if (safeMessage.Length > MaximumMessageCharacters)
                    safeMessage = safeMessage.Substring(0, MaximumMessageCharacters) + "… [truncated]";
                string line = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {safeMessage}{Environment.NewLine}";
                File.AppendAllText(SettingsStore.LogPath, line, Utf8NoBom);
                // Keep the active file within the exact configured line limit.
                TrimCurrentLog();
                _writesSinceCleanup++;
                if (_writesSinceCleanup >= 20)
                {
                    CleanupOldLogs();
                    _writesSinceCleanup = 0;
                }
            }
            catch { }
        }
    }

    public static void Clear()
    {
        lock (Sync)
        {
            try
            {
                Directory.CreateDirectory(SettingsStore.LogDirectory);
                File.WriteAllText(SettingsStore.LogPath, string.Empty, Utf8NoBom);
                _writesSinceCleanup = 0;
            }
            catch { }
        }
    }

    private static void TrimCurrentLog()
    {
        try
        {
            if (!File.Exists(SettingsStore.LogPath)) return;
            var retained = new System.Collections.Generic.Queue<string>(MaximumLines);
            int totalLines = 0;
            foreach (string line in File.ReadLines(SettingsStore.LogPath))
            {
                totalLines++;
                if (retained.Count == MaximumLines) retained.Dequeue();
                retained.Enqueue(line);
            }
            if (totalLines <= MaximumLines) return;
            File.WriteAllLines(SettingsStore.LogPath, retained, Utf8NoBom);
        }
        catch { }
    }

    private static void CleanupOldLogs()
    {
        try
        {
            if (!Directory.Exists(SettingsStore.LogDirectory)) return;
            DateTime cutoff = DateTime.Now.AddDays(-RetentionDays);
            foreach (string path in Directory.EnumerateFiles(SettingsStore.LogDirectory, "*.log*"))
            {
                if (string.Equals(path, SettingsStore.LogPath, StringComparison.OrdinalIgnoreCase))
                {
                    var active = new FileInfo(path);
                    if (active.Exists && active.LastWriteTime < cutoff)
                        File.WriteAllText(path, string.Empty, Utf8NoBom);
                    continue;
                }
                try { if (File.GetLastWriteTime(path) < cutoff) File.Delete(path); }
                catch { }
            }
        }
        catch { }
    }
}
