using System;
using System.IO;
using System.Text.Json;

namespace PerfOverlay;

public sealed class AppSettings
{
    public bool ShowCpu { get; set; } = true;
    public bool ShowRam { get; set; } = true;
    public bool ShowNet { get; set; } = true;
    public bool ShowGpu { get; set; } = true;
    public bool ShowBackground { get; set; }
    public double FontSize { get; set; } = 15;
    public double TextOpacity { get; set; } = 0.9;
    public string FontColor { get; set; } = "#FFFFFF";
    public int UpdateIntervalMs { get; set; } = 1000;
    public bool LockPosition { get; set; } = true;
    public bool RunAtStartup { get; set; }
    public double Left { get; set; } = 24;
    public double Top { get; set; } = 24;

    private static string SettingsDir =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "PerfOverlay");

    private static string SettingsPath => Path.Combine(SettingsDir, "settings.json");

    public static AppSettings Load()
    {
        try
        {
            if (File.Exists(SettingsPath))
            {
                AppSettings? loaded = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(SettingsPath));
                if (loaded != null)
                    return loaded;
            }
        }
        catch
        {
        }
        return new AppSettings();
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(SettingsDir);
            File.WriteAllText(SettingsPath,
                JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch
        {
        }
    }

    public AppSettings Clone() => (AppSettings)MemberwiseClone();

    public void CopyFrom(AppSettings s)
    {
        ShowCpu = s.ShowCpu;
        ShowRam = s.ShowRam;
        ShowNet = s.ShowNet;
        ShowGpu = s.ShowGpu;
        ShowBackground = s.ShowBackground;
        FontSize = s.FontSize;
        TextOpacity = s.TextOpacity;
        FontColor = s.FontColor;
        UpdateIntervalMs = s.UpdateIntervalMs;
        LockPosition = s.LockPosition;
        RunAtStartup = s.RunAtStartup;
    }
}
