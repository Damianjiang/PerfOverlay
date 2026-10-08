using System;
using System.Threading;
using System.Windows;
using Microsoft.Win32;

namespace PerfOverlay;

public partial class App : Application
{
    private const string MutexName = @"Local\PerfOverlay_SingleInstance_9D6B4C3E-2A71-4F5C-8E3B-1F4A7C9E5B02";
    private const string StartupRunName = "PerfOverlay";

    private Mutex? _mutex;
    private SystemMonitor? _monitor;
    private OverlayWindow? _overlay;
    private TrayController? _tray;
    private SettingsWindow? _settings;
    private bool _editingPosition;

    public static AppSettings Settings { get; private set; } = new();

    public bool OverlayVisible => _overlay is { IsVisible: true };

    protected override void OnStartup(StartupEventArgs e)
    {
        System.Windows.Forms.Application.EnableVisualStyles();

        _mutex = new Mutex(initiallyOwned: true, MutexName, out bool createdNew);
        if (!createdNew)
        {
            MessageBox.Show(
                "性能监控已经在运行中，请查看系统托盘图标。\n如需重启，请先通过托盘菜单退出程序。",
                "PerfOverlay",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            Shutdown();
            return;
        }

        Settings = AppSettings.Load();
        ApplyStartupRegistry(Settings.RunAtStartup);

        _monitor = new SystemMonitor(Settings.UpdateIntervalMs);
        _overlay = new OverlayWindow(_monitor);
        _overlay.ApplySettings();
        _overlay.Show();
        _monitor.Start();

        _tray = new TrayController(this);
    }

    public void ToggleOverlay()
    {
        if (_overlay == null)
            return;
        if (_overlay.IsVisible)
            _overlay.Hide();
        else
            _overlay.Show();
        _tray?.SyncState();
    }

    public void ShowSettingsWindow()
    {
        if (_settings != null && _settings.IsLoaded)
        {
            _settings.Activate();
            return;
        }
        _settings = new SettingsWindow();
        _settings.Show();
        _settings.Activate();
    }

    public void SetLockPosition(bool locked)
    {
        Settings.LockPosition = locked;
        _overlay?.ApplySettings();
        Settings.Save();
    }

    public void ResetOverlayPosition()
    {
        Settings.Left = 24;
        Settings.Top = 24;
        _overlay?.SetPosition(24, 24);
        Settings.Save();
    }

    public bool IsEditingPosition => _editingPosition;

    public void BeginPositionEdit()
    {
        _editingPosition = true;
        Settings.LockPosition = false;
        _overlay?.ApplySettings();
        _overlay?.SetEditingPosition(true);
        _tray?.SyncState();
    }

    public void FinishPositionEdit()
    {
        _editingPosition = false;
        CommitOverlayPosition();
        Settings.LockPosition = true;
        _overlay?.ApplySettings();
        _overlay?.SetEditingPosition(false);
        _tray?.SyncState();
    }

    public void CommitOverlayPosition()
    {
        if (_overlay != null)
        {
            Settings.Left = _overlay.Left;
            Settings.Top = _overlay.Top;
        }
        Settings.Save();
    }

    public void ApplyAllSettings(AppSettings source)
    {
        Settings.CopyFrom(source);
        Settings.Save();
        _overlay?.ApplySettings();
        _monitor?.SetInterval(Settings.UpdateIntervalMs);
        ApplyStartupRegistry(Settings.RunAtStartup);
        _tray?.SyncState();
    }

    public void ExitApp()
    {
        Settings.Save();
        Shutdown();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _tray?.Dispose();
        _monitor?.Dispose();
        _mutex?.Dispose();
        base.OnExit(e);
    }

    private static void ApplyStartupRegistry(bool enable)
    {
        try
        {
            using RegistryKey? key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", writable: true);
            if (key == null)
                return;
            if (enable)
            {
                string? exe = Environment.ProcessPath;
                if (!string.IsNullOrEmpty(exe))
                    key.SetValue(StartupRunName, $"\"{exe}\"");
            }
            else if (key.GetValue(StartupRunName) != null)
            {
                key.DeleteValue(StartupRunName);
            }
        }
        catch
        {
        }
    }
}
