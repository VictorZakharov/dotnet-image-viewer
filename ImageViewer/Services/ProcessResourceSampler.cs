using System;
using System.Diagnostics;
using ImageViewer.Models;

namespace ImageViewer.Services;

internal interface IResourceUsageSampler : IDisposable
{
    int ProcessId { get; }
    int ProcessorCount { get; }
    DateTimeOffset StartedAt { get; }
    ResourceUsageSample Capture();
}

internal sealed class ProcessResourceSampler : IResourceUsageSampler
{
    private readonly Process _process = Process.GetCurrentProcess();
    private TimeSpan _previousProcessorTime;
    private long _previousTimestamp;
    private bool _hasCpuBaseline;

    public int ProcessId => _process.Id;
    public int ProcessorCount { get; } = Math.Max(1, Environment.ProcessorCount);
    public DateTimeOffset StartedAt { get; }

    public ProcessResourceSampler()
    {
        StartedAt = ReadStartedAt(_process);
    }

    public ResourceUsageSample Capture()
    {
        _process.Refresh();
        var timestamp = Stopwatch.GetTimestamp();
        var processorTime = Read(() => _process.TotalProcessorTime, _previousProcessorTime);
        var cpuPercent = _hasCpuBaseline
            ? CalculateCpuPercentage(
                processorTime - _previousProcessorTime,
                Stopwatch.GetElapsedTime(_previousTimestamp, timestamp),
                ProcessorCount)
            : 0;

        _previousProcessorTime = processorTime;
        _previousTimestamp = timestamp;
        _hasCpuBaseline = true;

        return new ResourceUsageSample(
            DateTimeOffset.UtcNow,
            cpuPercent,
            Math.Max(0, Read(() => _process.WorkingSet64, 0L)),
            Math.Max(0, Read(() => _process.PrivateMemorySize64, 0L)),
            Math.Max(0, GC.GetTotalMemory(forceFullCollection: false)),
            Math.Max(0, Read(() => _process.Threads.Count, 0)));
    }

    internal static double CalculateCpuPercentage(
        TimeSpan processorTime,
        TimeSpan elapsed,
        int processorCount)
    {
        if (processorTime <= TimeSpan.Zero || elapsed <= TimeSpan.Zero)
            return 0;
        var utilization = processorTime.TotalSeconds /
                          (elapsed.TotalSeconds * Math.Max(1, processorCount)) * 100;
        return Math.Clamp(utilization, 0, 100);
    }

    public void Dispose() => _process.Dispose();

    private static DateTimeOffset ReadStartedAt(Process process)
    {
        try { return new DateTimeOffset(process.StartTime).ToUniversalTime(); }
        catch { return DateTimeOffset.UtcNow; }
    }

    private static T Read<T>(Func<T> read, T fallback)
    {
        try { return read(); }
        catch { return fallback; }
    }
}
