using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;

namespace PerfOverlay;

public partial class SettingsWindow : Window
{
    private readonly AppSettings _draft;

    private sealed record IntervalOption(int Ms, string Label);

    public SettingsWindow()
    {
        InitializeComponent();
        _draft = App.Settings.Clone();
        DataContext = _draft;
        BuildColorOptions();
        BuildIntervalOptions();
        UpdatePositionLabel();
        Closed += OnClosed;
    }

    private void BuildColorOptions()
    {
        (string Label, string Hex)[] palette =
        {
            ("白色", "#FFFFFF"),
            ("黑色", "#000000"),
            ("黄色", "#FFEB3B"),
            ("绿色", "#69F0AE"),
            ("青色", "#40C4FF"),
            ("蓝色", "#448AFF"),
            ("粉色", "#FF80AB"),
            ("橙色", "#FFB74D"),
            ("红色", "#FF5252"),
        };

        ColorBox.SelectedValuePath = "Tag";
        foreach ((string label, string hex) in palette)
        {
            var item = new ComboBoxItem
            {
                Content = "  " + label,
                Tag = hex
            };
            item.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
            ColorBox.Items.Add(item);
        }
        ColorBox.SetBinding(ComboBox.SelectedValueProperty,
            new Binding(nameof(AppSettings.FontColor)) { Source = _draft, Mode = BindingMode.TwoWay });
    }

    private void BuildIntervalOptions()
    {
        IntervalOption[] intervals =
        {
            new IntervalOption(500, "0.5 秒"),
            new IntervalOption(1000, "1 秒"),
            new IntervalOption(2000, "2 秒"),
            new IntervalOption(5000, "5 秒"),
        };

        IntervalBox.DisplayMemberPath = "Label";
        IntervalBox.SelectedValuePath = "Ms";
        foreach (IntervalOption option in intervals)
            IntervalBox.Items.Add(option);
        IntervalBox.SetBinding(ComboBox.SelectedValueProperty,
            new Binding(nameof(AppSettings.UpdateIntervalMs)) { Source = _draft, Mode = BindingMode.TwoWay });
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        _draft.FontSize = Math.Clamp(_draft.FontSize, 10, 40);
        _draft.TextOpacity = Math.Clamp(_draft.TextOpacity, 0.2, 1.0);
        _draft.UpdateIntervalMs = Math.Clamp(_draft.UpdateIntervalMs, 250, 60000);

        App app = (App)Application.Current;
        if (app.IsEditingPosition)
            app.FinishPositionEdit();
        app.ApplyAllSettings(_draft);
        Close();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        App app = (App)Application.Current;
        if (app.IsEditingPosition)
            app.FinishPositionEdit();
        Close();
    }

    private void ResetPosition_Click(object sender, RoutedEventArgs e)
    {
        App app = (App)Application.Current;
        app.ResetOverlayPosition();
        UpdatePositionLabel();
    }

    private void PositionDrag_Click(object sender, RoutedEventArgs e)
    {
        App app = (App)Application.Current;
        if (!app.IsEditingPosition)
        {
            app.BeginPositionEdit();
            PositionDragButton.Content = "② 已解锁，拖动悬浮窗后点击此处保存位置";
        }
        else
        {
            app.FinishPositionEdit();
            PositionDragButton.Content = "① 点击后拖动悬浮窗到想要的位置";
        }
        UpdatePositionLabel();
    }

    private void OnClosed(object? sender, EventArgs e)
    {
        App app = (App)Application.Current;
        if (app.IsEditingPosition)
            app.FinishPositionEdit();
    }

    private void UpdatePositionLabel()
    {
        PositionLabel.Text = $"当前位置：X = {App.Settings.Left:0}，Y = {App.Settings.Top:0}";
    }

    private void About_Click(object sender, RoutedEventArgs e)
    {
        MessageBox.Show(
            "本软件由 Damian 用心用 C# 制作",
            "关于 PerfOverlay",
            MessageBoxButton.OK,
            MessageBoxImage.Information);
    }
}
