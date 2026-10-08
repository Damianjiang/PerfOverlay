# PerfOverlay

<p align="center">
  <img alt="Platform" src="https://img.shields.io/badge/Platform-Windows%2010%2F11-blue.svg">
  <img alt=".NET" src="https://img.shields.io/badge/.NET-9.0-5C2D92?logo=dotnet">
  <img alt="WPF" src="https://img.shields.io/badge/UI-WPF%20%2B%20WinForms-green">
  <img alt="Language" src="https://img.shields.io/badge/Language-C%23-purple">
  <img alt="License" src="https://img.shields.io/badge/License-MIT-informational">
</p>

**PerfOverlay** 是一个轻量级 Windows 系统性能悬浮监控工具：把 CPU、内存、GPU、网速实时显示在桌面上，
像 FPS 监控器一样小巧、可拖拽、可锁定位置、可开机自启。单文件发布，几乎零占用。

> A lightweight real-time system performance overlay for Windows (CPU / RAM / GPU / Network speed),
> built with C# WPF on .NET 9.

## ✨ 功能特性 Features

- 📊 **实时监控**：CPU 使用率、内存占用（已用/总量/百分比）、GPU 3D 利用率与显存占用、网络上下行速度
- 🪟 **悬浮窗显示**：置顶悬浮于桌面，锁定位置后鼠标穿透，不挡操作
- 🖱️ **可拖拽定位**：一键解锁 → 拖动到想要的位置 → 保存，位置自动记忆
- 🎨 **高度自定义**：显示项开关（CPU/内存/网络/GPU）、背景开关、字号、透明度、9 种颜色主题
- ⚡ **刷新可调**：0.5s / 1s / 2s / 5s 刷新间隔，按需省电
- 🚀 **开机自启**：通过注册表 Run 项一键开启/关闭
- 📦 **单实例运行**：Mutex 防止重复启动；设置保存在 `%AppData%\PerfOverlay\settings.json`
- 🛠️ **无外部依赖**：仅使用 .NET BCL（PerformanceCounter + P/Invoke），无第三方 NuGet 包

## 🖥️ 界面预览

| 悬浮监控窗 | 设置面板 |
| --- | --- |
| 桌面角落实时显示 CPU / 内存 / 网速 / GPU | 显示项、样式、刷新间隔、位置、自启配置 |

> 右键系统托盘图标可快速：显示/隐藏监控、打开设置、锁定/解锁位置、退出。双击托盘图标直接打开设置。

## 🚀 快速上手

### 从源码构建（需 .NET 9 SDK，Windows）

```bash
git clone https://github.com/Damianjiang/PerfOverlay.git
cd PerfOverlay
dotnet build -c Release
```

### 发布单文件 exe（可直接运行）

```bash
dotnet publish -c Release -r win-x64 --self-contained true /p:PublishSingleFile=true
```

产物在 `bin/Release/net9.0-windows/win-x64/publish/PerfOverlay.exe`，
复制到任意目录双击即用，无需安装 .NET 运行时。

## ⚙️ 使用说明

1. 启动后默认在屏幕左上角显示监控悬浮窗，位置默认锁定（鼠标穿透）
2. 右键托盘图标 → **设置**，可调整显示内容与样式
3. 调整位置：设置面板中点击「解锁位置」→ 拖动悬浮窗 → 再点一次保存
4. 开机自启：设置面板勾选「开机自启动」
5. 退出：托盘菜单 → 退出（设置自动保存）

## 📁 项目结构

```
PerfOverlay/
├── App.xaml(.cs)          # 入口、单实例、开机自启注册表
├── OverlayWindow.xaml(.cs)# 悬浮监控窗
├── SettingsWindow.xaml(.cs)# 设置面板
├── SystemMonitor.cs       # CPU/RAM/GPU/网络 采样（PerformanceCounter + P/Invoke）
├── TrayController.cs      # 系统托盘
└── AppSettings.cs         # 设置持久化（%AppData%）
```

## 🔍 关键词 Keywords

performance overlay, system monitor, CPU monitor, GPU monitor, RAM memory monitor,
network speed, FPS monitor alternative, WPF, C#, .NET 9, Windows, 性能监控, 系统状态,
CPU 占用, 内存, 显卡, 网速, 悬浮窗, 开机自启, 桌面挂件, 轻量工具, productivity,
system information, resource monitor, hardware monitor, windows desktop widget

## 📄 License

MIT — 自由使用、修改、分发。
