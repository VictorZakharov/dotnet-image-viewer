using ImageViewer.Models;
using ImageViewer.Services;
using ImageViewer.ViewModels;

namespace ImageViewer.Tests;

public sealed class ResourceMonitorTests
{
    private const long MiB = 1024L * 1024;

    [Fact]
    public void CpuPercentageIsNormalizedAcrossLogicalProcessors()
    {
        var percentage = ProcessResourceSampler.CalculateCpuPercentage(
            TimeSpan.FromMilliseconds(500),
            TimeSpan.FromSeconds(1),
            processorCount: 2);

        Assert.Equal(25, percentage, precision: 5);
        Assert.Equal(0, ProcessResourceSampler.CalculateCpuPercentage(
            TimeSpan.Zero,
            TimeSpan.FromSeconds(1),
            processorCount: 4));
        Assert.Equal(100, ProcessResourceSampler.CalculateCpuPercentage(
            TimeSpan.FromSeconds(8),
            TimeSpan.FromSeconds(1),
            processorCount: 4));
    }

    [Fact]
    public void SamplesPublishCurrentValuesAndRollingSummaries()
    {
        var startedAt = new DateTimeOffset(2026, 8, 10, 12, 0, 0, TimeSpan.Zero);
        using var sampler = new FakeSampler(startedAt,
        [
            Sample(startedAt.AddSeconds(1), 10, 100, 150, 20, 8),
            Sample(startedAt.AddSeconds(2), 30, 160, 210, 24, 9),
            Sample(startedAt.AddSeconds(3), 5, 120, 180, 22, 7)
        ]);
        using var viewModel = new ResourceMonitorViewModel(sampler, startTimer: false);

        viewModel.SampleNow();

        Assert.Equal([10d, 30d], viewModel.CpuHistory);
        Assert.Equal([100d * MiB, 160d * MiB], viewModel.MemoryHistory);
        Assert.Equal("30.0%", viewModel.CurrentCpuText);
        Assert.Equal("Average 20.0% · Peak 30.0%", viewModel.CpuSummary);
        Assert.Equal("160 MB", viewModel.WorkingSetText);
        Assert.Equal("Peak 160 MB", viewModel.MemorySummary);
        Assert.Equal("24 MB", viewModel.ManagedHeapText);
        Assert.Equal("210 MB private", viewModel.PrivateMemoryText);
        Assert.Equal("9", viewModel.ThreadCountText);
        Assert.Equal("00:00:02 uptime", viewModel.UptimeText);
        Assert.Equal("2 samples · 1s of 5 minutes", viewModel.HistorySummary);
        Assert.Equal(192d * MiB, viewModel.MemoryGraphMaximum);
        Assert.Equal("192 MB scale", viewModel.MemoryScaleText);
        Assert.Equal("ImageViewer process 42 · 4 logical processors", viewModel.ProcessSummary);

        viewModel.TogglePauseCommand.Execute(null);
        Assert.True(viewModel.IsPaused);
        Assert.Equal("Resume", viewModel.PauseLabel);
        Assert.Equal("Paused · history is preserved", viewModel.StatusText);

        viewModel.ClearHistoryCommand.Execute(null);
        Assert.Equal([5d], viewModel.CpuHistory);
        Assert.Equal("1 sample · 0s of 5 minutes", viewModel.HistorySummary);
    }

    [Fact]
    public void HistoryKeepsMostRecentFiveMinutesOfOneSecondSamples()
    {
        var startedAt = DateTimeOffset.UtcNow;
        using var sampler = new FakeSampler(startedAt, index =>
            Sample(startedAt.AddSeconds(index), index % 100, 64 + index, 80, 16, 4));
        using var viewModel = new ResourceMonitorViewModel(sampler, startTimer: false);

        for (var index = 1; index < 305; index++)
            viewModel.SampleNow();

        Assert.Equal(300, viewModel.CpuHistory.Count);
        Assert.Equal(5, viewModel.CpuHistory[0]);
        Assert.Equal(4, viewModel.CpuHistory[^1]);
        Assert.Equal("300 samples · 4m 59s of 5 minutes", viewModel.HistorySummary);
    }

    [Fact]
    public void HistoryDropsSamplesOlderThanFiveMinutesAfterAGap()
    {
        var startedAt = DateTimeOffset.UtcNow;
        using var sampler = new FakeSampler(startedAt,
        [
            Sample(startedAt, 10, 64, 80, 16, 4),
            Sample(startedAt.AddMinutes(5).AddSeconds(1), 20, 65, 81, 17, 5)
        ]);
        using var viewModel = new ResourceMonitorViewModel(sampler, startTimer: false);

        viewModel.SampleNow();

        Assert.Equal([20d], viewModel.CpuHistory);
        Assert.Equal("1 sample · 0s of 5 minutes", viewModel.HistorySummary);
    }

    private static ResourceUsageSample Sample(
        DateTimeOffset timestamp,
        double cpu,
        long workingSetMiB,
        long privateMiB,
        long managedMiB,
        int threads) => new(
        timestamp,
        cpu,
        workingSetMiB * MiB,
        privateMiB * MiB,
        managedMiB * MiB,
        threads);

    private sealed class FakeSampler : IResourceUsageSampler
    {
        private readonly Func<int, ResourceUsageSample> _capture;
        private int _index;

        public int ProcessId => 42;
        public int ProcessorCount => 4;
        public DateTimeOffset StartedAt { get; }

        public FakeSampler(
            DateTimeOffset startedAt,
            IReadOnlyList<ResourceUsageSample> samples)
            : this(startedAt, index => samples[Math.Min(index, samples.Count - 1)])
        {
        }

        public FakeSampler(
            DateTimeOffset startedAt,
            Func<int, ResourceUsageSample> capture)
        {
            StartedAt = startedAt;
            _capture = capture;
        }

        public ResourceUsageSample Capture() => _capture(_index++);

        public void Dispose()
        {
        }
    }
}
