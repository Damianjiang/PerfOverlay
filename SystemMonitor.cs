using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace PerfOverlay;

public sealed record SystemSnapshot(
    float CpuPercent,
    double RamUsedGb,
    double RamTotalGb,
    double RamPercent,
    double DownloadBytesPerSec,
    double UploadBytesPerSec,
    bool GpuSupported,
    float GpuPercent,
    double GpuDedicatedGb);

public sealed class SystemMonitor : IDisposable
{
    public event Action<SystemSnapshot>? SnapshotReady;

    private const string CpuCategory = "Processor Information";
    private const string CpuCategoryLegacy = "Processor";
    private const string NetCategory = "Network Interface";
    private const string GpuEngineCategory = "GPU Engine";
    private const string GpuMemoryCategory = "GPU Adapter Memory";
    private static readonly TimeSpan NetworkRefreshInterval = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan GpuRefreshInterval = TimeSpan.FromSeconds(15);

    private readonly PeriodicTimer _timer;
    private readonly CancellationTokenSource _cts = new();
    private Task? _loopTask;
    private PerformanceCounter? _cpuCounter;
    private readonly List<PerformanceCounter> _rxCounters = new();
    private readonly List<PerformanceCounter> _txCounters = new();
    private readonly List<PerformanceCounter> _gpuCounters = new();
    private readonly List<PerformanceCounter> _vramCounters = new();
    private DateTime _nextNetworkRefreshUtc;
    private DateTime _nextGpuRefreshUtc;
    private bool _disposed;

    public SystemMonitor(int intervalMs)
    {
        _timer = new PeriodicTimer(TimeSpan.FromMilliseconds(Math.Clamp(intervalMs, 250, 60000)));
    }

    public void SetInterval(int intervalMs)
    {
        try
        {
            _timer.Period = TimeSpan.FromMilliseconds(Math.Clamp(intervalMs, 250, 60000));
        }
        catch (ObjectDisposedException)
        {
        }
    }

    public void Start()
    {
        InitCpu();
        InitNetwork();
        InitGpu();
        _loopTask = Task.Run(() => RunAsync(_cts.Token));
    }

    private async Task RunAsync(CancellationToken ct)
    {
        try
        {
            while (await _timer.WaitForNextTickAsync(ct).ConfigureAwait(false))
            {
                SystemSnapshot snapshot = Collect();
                SnapshotReady?.Invoke(snapshot);
            }
        }
        catch (OperationCanceledException)
        {
        }
    }

    private SystemSnapshot Collect()
    {
        float cpu = 0;
        try
        {
            if (_cpuCounter != null)
                cpu = _cpuCounter.NextValue();
        }
        catch
        {
        }
        cpu = Math.Clamp(cpu, 0f, 100f);

        double ramTotalGb = 0;
        double ramUsedGb = 0;
        double ramPercent = 0;
        var memory = new MemoryStatusEx();
        if (GlobalMemoryStatusEx(memory))
        {
            ramTotalGb = memory.TotalPhysicalBytes / (1024.0 * 1024 * 1024);
            ramUsedGb = (memory.TotalPhysicalBytes - memory.AvailablePhysicalBytes) / (1024.0 * 1024 * 1024);
            ramPercent = ramTotalGb > 0 ? ramUsedGb / ramTotalGb * 100.0 : 0;
        }

        if (DateTime.UtcNow >= _nextNetworkRefreshUtc)
            InitNetwork();

        double download = 0;
        double upload = 0;
        try
        {
            foreach (PerformanceCounter c in _rxCounters)
                download += c.NextValue();
            foreach (PerformanceCounter c in _txCounters)
                upload += c.NextValue();
        }
        catch
        {
        }

        if (DateTime.UtcNow >= _nextGpuRefreshUtc)
            InitGpu();

        bool gpuSupported = _gpuCounters.Count > 0 || _vramCounters.Count > 0;
        float gpuPercent = 0;
        double vramGb = -1;
        if (gpuSupported)
        {
            var perAdapter = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);
            try
            {
                foreach (PerformanceCounter c in _gpuCounters)
                {
                    float value = c.NextValue();
                    string adapter = AdapterKeyFromInstance(c.InstanceName);
                    perAdapter[adapter] = perAdapter.GetValueOrDefault(adapter) + value;
                }
            }
            catch
            {
            }
            if (perAdapter.Count > 0)
                gpuPercent = Math.Clamp(perAdapter.Values.Max(), 0f, 100f);

            if (_vramCounters.Count > 0)
            {
                double vramBytes = 0;
                try
                {
                    foreach (PerformanceCounter c in _vramCounters)
                    {
                        float value = c.NextValue();
                        if (value > vramBytes)
                            vramBytes = value;
                    }
                }
                catch
                {
                }
                vramGb = vramBytes / (1024.0 * 1024 * 1024);
            }
        }

        return new SystemSnapshot(
            cpu,
            ramUsedGb,
            ramTotalGb,
            ramPercent,
            download,
            upload,
            gpuSupported,
            gpuPercent,
            vramGb);
    }

    private void InitCpu()
    {
        try
        {
            string category = PerformanceCounterCategory.Exists(CpuCategory) ? CpuCategory : CpuCategoryLegacy;
            _cpuCounter = new PerformanceCounter(category, "% Processor Time", "_Total", readOnly: true);
            _cpuCounter.NextValue();
        }
        catch
        {
            _cpuCounter = null;
        }
    }

    private void InitNetwork()
    {
        DisposeAndClear(_rxCounters);
        DisposeAndClear(_txCounters);
        try
        {
            string[] instances = new PerformanceCounterCategory(NetCategory).GetInstanceNames();
            foreach (string instance in instances)
            {
                if (IsVirtualNic(instance))
                    continue;
                _rxCounters.Add(new PerformanceCounter(NetCategory, "Bytes Received/sec", instance, readOnly: true));
                _txCounters.Add(new PerformanceCounter(NetCategory, "Bytes Sent/sec", instance, readOnly: true));
            }
            foreach (PerformanceCounter c in _rxCounters)
                c.NextValue();
            foreach (PerformanceCounter c in _txCounters)
                c.NextValue();
        }
        catch
        {
        }
        _nextNetworkRefreshUtc = DateTime.UtcNow + NetworkRefreshInterval;
    }

    private static bool IsVirtualNic(string name)
    {
        return name.StartsWith("Loopback", StringComparison.OrdinalIgnoreCase)
               || name.StartsWith("isatap", StringComparison.OrdinalIgnoreCase)
               || name.StartsWith("Teredo", StringComparison.OrdinalIgnoreCase);
    }

    private void InitGpu()
    {
        DisposeAndClear(_gpuCounters);
        DisposeAndClear(_vramCounters);
        try
        {
            if (PerformanceCounterCategory.Exists(GpuEngineCategory))
            {
                string[] instances = new PerformanceCounterCategory(GpuEngineCategory).GetInstanceNames();
                foreach (string instance in instances)
                {
                    if (instance.IndexOf("engtype_3D", StringComparison.OrdinalIgnoreCase) >= 0)
                        _gpuCounters.Add(new PerformanceCounter(GpuEngineCategory, "Utilization Percentage", instance, readOnly: true));
                }
                foreach (PerformanceCounter c in _gpuCounters)
                    c.NextValue();
            }
            if (PerformanceCounterCategory.Exists(GpuMemoryCategory))
            {
                string[] instances = new PerformanceCounterCategory(GpuMemoryCategory).GetInstanceNames();
                foreach (string instance in instances)
                {
                    if (instance.StartsWith("phys_", StringComparison.OrdinalIgnoreCase))
                        _vramCounters.Add(new PerformanceCounter(GpuMemoryCategory, "Dedicated Usage", instance, readOnly: true));
                }
                foreach (PerformanceCounter c in _vramCounters)
                    c.NextValue();
            }
        }
        catch
        {
        }
        _nextGpuRefreshUtc = DateTime.UtcNow + GpuRefreshInterval;
    }

    private static string AdapterKeyFromInstance(string instance)
    {
        int luid = instance.IndexOf("luid_", StringComparison.OrdinalIgnoreCase);
        if (luid < 0)
            return instance;
        int phys = instance.IndexOf("_phys_", luid, StringComparison.OrdinalIgnoreCase);
        return phys > luid ? instance.Substring(luid, phys - luid) : instance;
    }

    private static void DisposeAndClear(List<PerformanceCounter> list)
    {
        foreach (PerformanceCounter c in list)
        {
            try
            {
                c.Dispose();
            }
            catch
            {
            }
        }
        list.Clear();
    }

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        try
        {
            _cts.Cancel();
        }
        catch
        {
        }
        try
        {
            _timer.Dispose();
        }
        catch
        {
        }
        try
        {
            _loopTask?.Wait(TimeSpan.FromSeconds(2));
        }
        catch
        {
        }
        _cpuCounter?.Dispose();
        DisposeAndClear(_rxCounters);
        DisposeAndClear(_txCounters);
        DisposeAndClear(_gpuCounters);
        DisposeAndClear(_vramCounters);
        _cts.Dispose();
    }

    [StructLayout(LayoutKind.Sequential)]
    private sealed class MemoryStatusEx
    {
        public uint Length = (uint)Marshal.SizeOf(typeof(MemoryStatusEx));
        public uint MemoryLoad;
        public ulong TotalPhysicalBytes;
        public ulong AvailablePhysicalBytes;
        public ulong TotalPageFileBytes;
        public ulong AvailablePageFileBytes;
        public ulong TotalVirtualBytes;
        public ulong AvailableVirtualBytes;
        public ulong AvailableExtendedVirtualBytes;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GlobalMemoryStatusEx([In, Out] MemoryStatusEx buffer);
}
