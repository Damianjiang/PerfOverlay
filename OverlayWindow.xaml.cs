using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;

namespace PerfOverlay;

public partial class OverlayWindow : Window
{
    private readonly SystemMonitor _monitor;
    private IntPtr _hwnd;
    private bool _clickThrough;
    private bool _keepTopmost = true;

    private const int GwlExStyle = -20;
    private const int WsExTransparent = 0x00000020;
    private const int WsExToolWindow = 0x00000080;
    private const int WsExNoActivate = 0x08000000;

    private static readonly IntPtr HwndTopmost = new IntPtr(-1);
    private const uint SwpNoSize = 0x0001;
    private const uint SwpNoMove = 0x0002;
    private const uint SwpNoActivate = 0x0010;

    public OverlayWindow(SystemMonitor monitor)
    {
        InitializeComponent();
        _monitor = monitor;

        Left = App.Settings.Left;
        Top = App.Settings.Top;

        SourceInitialized += OnSourceInitialized;
        LocationChanged += OnLocationChanged;
        MouseLeftButtonDown += OnMouseLeftButtonDown;
        Closed += OnClosed;
        _monitor.SnapshotReady += OnSnapshotReady;
    }

    private void OnSourceInitialized(object? sender, EventArgs e)
    {
        _hwnd = new WindowInteropHelper(this).Handle;
        ApplyClickThrough();
        KeepTopmost();
    }

    private void OnClosed(object? sender, EventArgs e)
    {
        _monitor.SnapshotReady -= OnSnapshotReady;
    }

    private void OnLocationChanged(object? sender, EventArgs e)
    {
        App.Settings.Left = Left;
        App.Settings.Top = Top;
    }

    private void OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (_clickThrough)
            return;
        try
        {
            DragMove();
        }
        catch (InvalidOperationException)
        {
        }
    }

    private void OnSnapshotReady(SystemSnapshot snapshot)
    {
        Dispatcher.InvokeAsync(() =>
        {
            CpuText.Text = $"CPU  {snapshot.CpuPercent,3:0}%";
            RamText.Text = $"内存 {snapshot.RamPercent,3:0}%  {snapshot.RamUsedGb:0.0}/{snapshot.RamTotalGb:0.0}GB";
            NetText.Text = $"↓ {FormatBytes(snapshot.DownloadBytesPerSec)}/s    ↑ {FormatBytes(snapshot.UploadBytesPerSec)}/s";
            if (!snapshot.GpuSupported)
            {
                GpuText.Text = "GPU  不支持";
            }
            else if (snapshot.GpuDedicatedGb >= 0)
            {
                GpuText.Text = $"GPU  {snapshot.GpuPercent,3:0}%  显存 {snapshot.GpuDedicatedGb:0.0}GB";
            }
            else
            {
                GpuText.Text = $"GPU  {snapshot.GpuPercent,3:0}%";
            }
            KeepTopmost();
        });
    }

    public void SetPosition(double left, double top)
    {
        Left = left;
        Top = top;
    }

    public void ApplySettings()
    {
        AppSettings s = App.Settings;

        CpuText.Visibility = s.ShowCpu ? Visibility.Visible : Visibility.Collapsed;
        RamText.Visibility = s.ShowRam ? Visibility.Visible : Visibility.Collapsed;
        NetText.Visibility = s.ShowNet ? Visibility.Visible : Visibility.Collapsed;
        GpuText.Visibility = s.ShowGpu ? Visibility.Visible : Visibility.Collapsed;

        double fontSize = Math.Clamp(s.FontSize, 8, 72);
        Color textColor = ParseColor(s.FontColor, s.TextOpacity);
        Brush foreground = new SolidColorBrush(textColor);
        foreground.Freeze();

        foreach (object child in TextPanel.Children)
        {
            if (child is TextBlock textBlock)
            {
                textBlock.FontSize = fontSize;
                textBlock.Foreground = foreground;
            }
        }

        _clickThrough = s.LockPosition;
        if (_clickThrough)
        {
            RootBorder.Background = s.ShowBackground
                ? new SolidColorBrush(Color.FromArgb(0x99, 0x14, 0x14, 0x18))
                : null;
            RootBorder.BorderThickness = new Thickness(0);
            RootBorder.BorderBrush = null;
            ToolTip = null;
        }
        else
        {
            RootBorder.Background = new SolidColorBrush(Color.FromArgb(0x70, 0x1E, 0x88, 0xE5));
            RootBorder.BorderThickness = new Thickness(1);
            RootBorder.BorderBrush = new SolidColorBrush(Color.FromArgb(0xFF, 0xFF, 0xB7, 0x4D));
            ToolTip = "按住鼠标左键拖动，调整后在设置中重新锁定位置";
        }

        ApplyClickThrough();
        KeepTopmost();
    }

    private static Color ParseColor(string hex, double opacity)
    {
        Color color = Colors.White;
        try
        {
            color = (Color)ColorConverter.ConvertFromString(hex);
        }
        catch
        {
        }
        color.A = (byte)Math.Clamp(opacity * 255.0, 25, 255);
        return color;
    }

    private static string FormatBytes(double bytes)
    {
        const double Kb = 1024.0;
        const double Mb = 1024.0 * 1024.0;
        const double Gb = 1024.0 * 1024.0 * 1024.0;
        if (bytes >= Gb)
            return $"{bytes / Gb:0.00}GB";
        if (bytes >= Mb)
            return $"{bytes / Mb:0.0}MB";
        if (bytes >= Kb)
            return $"{bytes / Kb:0.0}KB";
        return $"{bytes:0}B";
    }

    private void ApplyClickThrough()
    {
        if (_hwnd == IntPtr.Zero)
            return;
        int style = GetWindowLong(_hwnd, GwlExStyle);
        style |= WsExToolWindow;
        if (_clickThrough)
            style |= WsExTransparent | WsExNoActivate;
        else
            style &= ~(WsExTransparent | WsExNoActivate);
        SetWindowLong(_hwnd, GwlExStyle, style);
    }

    private void KeepTopmost()
    {
        if (!_keepTopmost || _hwnd == IntPtr.Zero)
            return;
        SetWindowPos(_hwnd, HwndTopmost, 0, 0, 0, 0, SwpNoSize | SwpNoMove | SwpNoActivate);
    }

    public void SetEditingPosition(bool editing)
    {
        _keepTopmost = !editing;
        if (_hwnd != IntPtr.Zero)
        {
            SetWindowPos(_hwnd, editing ? new IntPtr(-2) : HwndTopmost, 0, 0, 0, 0,
                SwpNoSize | SwpNoMove | SwpNoActivate);
        }
    }

    [DllImport("user32.dll", EntryPoint = "GetWindowLongW", CharSet = CharSet.Unicode)]
    private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongW", CharSet = CharSet.Unicode)]
    private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int x, int y, int cx, int cy, uint flags);
}
