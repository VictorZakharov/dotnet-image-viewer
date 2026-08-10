using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ImageViewer.Models;
using ImageViewer.Services;

namespace ImageViewer.ViewModels;

public partial class ResourceMonitorViewModel : ObservableObject, IDisposable
{
    private const int MaximumHistorySamples = 300;
    private const long MemoryScaleStep = 64L * 1024 * 1024;
    private static readonly TimeSpan HistoryWindow = TimeSpan.FromMinutes(5);

    private readonly IResourceUsageSampler _sampler;
    private readonly List<ResourceUsageSample> _history = [];
    private DispatcherTimer? _timer;
    private bool _disposed;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PauseLabel))]
    private bool _isPaused;

    [ObservableProperty] private string _statusText = "Live · updating every second";
    [ObservableProperty] private string _currentCpuText = "0.0%";
    [ObservableProperty] private string _cpuSummary = "Average 0.0% · Peak 0.0%";
    [ObservableProperty] private string _workingSetText = "0 B";
    [ObservableProperty] private string _memorySummary = "Peak 0 B";
    [ObservableProperty] private string _managedHeapText = "0 B";
    [ObservableProperty] private string _privateMemoryText = "0 B private";
    [ObservableProperty] private string _threadCountText = "0";
    [ObservableProperty] private string _uptimeText = "00:00:00 uptime";
    [ObservableProperty] private string _historySummary = "No history yet";
    [ObservableProperty] private string _memoryScaleText = "64 MB scale";
    [ObservableProperty] private IReadOnlyList<double> _cpuHistory = Array.Empty<double>();
    [ObservableProperty] private IReadOnlyList<double> _memoryHistory = Array.Empty<double>();
    [ObservableProperty] private double _memoryGraphMaximum = MemoryScaleStep;

    public string ProcessSummary { get; }
    public string StartedSummary { get; }
    public string PauseLabel => IsPaused ? "Resume" : "Pause";

    public ResourceMonitorViewModel()
        : this(new ProcessResourceSampler(), startTimer: true)
    {
    }

    internal ResourceMonitorViewModel(
        IResourceUsageSampler sampler,
        bool startTimer)
    {
        _sampler = sampler;
        ProcessSummary = $"ImageViewer process {_sampler.ProcessId} · " +
                         $"{_sampler.ProcessorCount} logical processor" +
                         (_sampler.ProcessorCount == 1 ? "" : "s");
        StartedSummary = $"Started {_sampler.StartedAt.ToLocalTime():g}";
        SampleNow();

        if (!startTimer) return;
        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _timer.Tick += OnTimerTick;
        _timer.Start();
    }

    internal void SampleNow()
    {
        if (_disposed) return;
        ResourceUsageSample sample;
        try
        {
            sample = _sampler.Capture();
        }
        catch
        {
            StatusText = "Resource sample unavailable · retrying";
            return;
        }

        _history.Add(sample);
        var cutoff = sample.Timestamp - HistoryWindow;
        _history.RemoveAll(existing => existing.Timestamp < cutoff);
        if (_history.Count > MaximumHistorySamples)
            _history.RemoveAt(0);
        Publish(sample);
    }

    [RelayCommand]
    private void TogglePause()
    {
        IsPaused = !IsPaused;
        if (IsPaused)
        {
            _timer?.Stop();
            StatusText = "Paused · history is preserved";
        }
        else
        {
            SampleNow();
            _timer?.Start();
            StatusText = "Live · updating every second";
        }
    }

    [RelayCommand]
    private void ClearHistory()
    {
        _history.Clear();
        SampleNow();
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        if (_timer is not null)
        {
            _timer.Stop();
            _timer.Tick -= OnTimerTick;
            _timer = null;
        }
        _sampler.Dispose();
    }

    private void OnTimerTick(object? sender, EventArgs e)
    {
        if (!IsPaused) SampleNow();
    }

    private void Publish(ResourceUsageSample current)
    {
        if (!IsPaused)
            StatusText = "Live · updating every second";
        var averageCpu = _history.Average(sample => sample.CpuPercent);
        var peakCpu = _history.Max(sample => sample.CpuPercent);
        var peakWorkingSet = _history.Max(sample => sample.WorkingSetBytes);
        MemoryGraphMaximum = ChooseMemoryScale(peakWorkingSet);

        CurrentCpuText = $"{current.CpuPercent:0.0}%";
        CpuSummary = $"Average {averageCpu:0.0}% · Peak {peakCpu:0.0}%";
        WorkingSetText = FileSizeDisplay.Format(current.WorkingSetBytes);
        MemorySummary = $"Peak {FileSizeDisplay.Format(peakWorkingSet)}";
        ManagedHeapText = FileSizeDisplay.Format(current.ManagedHeapBytes);
        PrivateMemoryText = $"{FileSizeDisplay.Format(current.PrivateMemoryBytes)} private";
        ThreadCountText = current.ThreadCount.ToString();
        UptimeText = $"{FormatDuration(current.Timestamp - _sampler.StartedAt)} uptime";
        MemoryScaleText = $"{FileSizeDisplay.Format((long)MemoryGraphMaximum)} scale";
        CpuHistory = _history.Select(sample => sample.CpuPercent).ToArray();
        MemoryHistory = _history.Select(sample => (double)sample.WorkingSetBytes).ToArray();

        var historyDuration = _history.Count > 1
            ? _history[^1].Timestamp - _history[0].Timestamp
            : TimeSpan.Zero;
        HistorySummary = $"{_history.Count} sample{(_history.Count == 1 ? "" : "s")} · " +
                         $"{FormatHistoryDuration(historyDuration)} of 5 minutes";
    }

    private static double ChooseMemoryScale(long peakBytes)
    {
        var desired = Math.Max(MemoryScaleStep, peakBytes * 1.1);
        return Math.Ceiling(desired / MemoryScaleStep) * MemoryScaleStep;
    }

    private static string FormatDuration(TimeSpan duration)
    {
        if (duration < TimeSpan.Zero) duration = TimeSpan.Zero;
        return duration.TotalDays >= 1
            ? $"{(int)duration.TotalDays}d {duration.Hours:00}:{duration.Minutes:00}:{duration.Seconds:00}"
            : $"{(int)duration.TotalHours:00}:{duration.Minutes:00}:{duration.Seconds:00}";
    }

    private static string FormatHistoryDuration(TimeSpan duration)
    {
        if (duration < TimeSpan.Zero) duration = TimeSpan.Zero;
        return duration.TotalMinutes >= 1
            ? $"{(int)duration.TotalMinutes}m {duration.Seconds}s"
            : $"{Math.Max(0, (int)duration.TotalSeconds)}s";
    }
}
