using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace PerfOverlay;

internal sealed class TrayController : IDisposable
{
    private readonly App _app;
    private readonly NotifyIcon _notifyIcon;
    private readonly ToolStripMenuItem _showItem;
    private readonly ToolStripMenuItem _lockItem;
    private bool _disposed;

    public TrayController(App app)
    {
        _app = app;

        _showItem = new ToolStripMenuItem("显示性能监控", null, (s, e) => _app.ToggleOverlay())
        {
            CheckOnClick = true
        };

        _lockItem = new ToolStripMenuItem("锁定位置（鼠标穿透）", null, OnToggleLockClick)
        {
            CheckOnClick = true
        };

        var menu = new ContextMenuStrip();
        menu.Items.Add(_showItem);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(new ToolStripMenuItem("设置", null, (s, e) => _app.ShowSettingsWindow()));
        menu.Items.Add(_lockItem);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(new ToolStripMenuItem("退出", null, (s, e) => _app.ExitApp()));

        _notifyIcon = new NotifyIcon
        {
            Text = "性能监控 PerfOverlay",
            Icon = CreateIcon(),
            Visible = true,
            ContextMenuStrip = menu
        };
        _notifyIcon.DoubleClick += (s, e) => _app.ShowSettingsWindow();

        SyncState();
    }

    public void SyncState()
    {
        _showItem.Checked = _app.OverlayVisible;
        _lockItem.Checked = App.Settings.LockPosition;
    }

    private void OnToggleLockClick(object? sender, EventArgs e)
    {
        _app.SetLockPosition(_lockItem.Checked);
    }

    private static Icon CreateIcon()
    {
        using Bitmap bmp = new(32, 32);
        using (Graphics g = Graphics.FromImage(bmp))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            using (SolidBrush bg = new(Color.FromArgb(255, 36, 36, 46)))
                g.FillEllipse(bg, 1, 1, 30, 30);
            using (Pen pen = new(Color.FromArgb(255, 255, 171, 64), 3))
                g.DrawLines(pen, new Point[] { new(6, 22), new(12, 14), new(18, 19), new(26, 8) });
            using (SolidBrush dot = new(Color.FromArgb(255, 105, 240, 174)))
                g.FillEllipse(dot, 24, 6, 5, 5);
        }
        return Icon.FromHandle(bmp.GetHicon());
    }

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
    }
}
