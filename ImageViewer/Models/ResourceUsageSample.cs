using System;

namespace ImageViewer.Models;

public sealed record ResourceUsageSample(
    DateTimeOffset Timestamp,
    double CpuPercent,
    long WorkingSetBytes,
    long PrivateMemoryBytes,
    long ManagedHeapBytes,
    int ThreadCount);
