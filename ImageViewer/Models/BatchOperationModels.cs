using System;
using System.Collections.Generic;
using System.IO;

namespace ImageViewer.Models;

public enum BatchPreviewStatus
{
    Ready,
    Unchanged,
    WillSkip,
    Invalid,
    Collision,
    Unsupported
}

public sealed record BatchPreviewItem(
    string SourcePath,
    string TargetPath,
    BatchPreviewStatus Status,
    string Message)
{
    public string SourceName => Path.GetFileName(SourcePath);
    public string TargetName => Path.GetFileName(TargetPath);
    public string SourceFolder => Path.GetDirectoryName(SourcePath) ?? "";
    public string StatusLabel => Status switch
    {
        BatchPreviewStatus.Ready => "Ready",
        BatchPreviewStatus.Unchanged => "Unchanged",
        BatchPreviewStatus.WillSkip => "Will skip",
        BatchPreviewStatus.Invalid => "Invalid",
        BatchPreviewStatus.Collision => "Collision",
        _ => "Unsupported"
    };
    public bool IsBlocking => Status is BatchPreviewStatus.Invalid
        or BatchPreviewStatus.Collision
        or BatchPreviewStatus.Unsupported;
}

public sealed record BatchItemSuccess(string SourcePath, string TargetPath);
public sealed record BatchItemFailure(string SourcePath, string TargetPath, string Error);

public sealed record BatchOperationResult(
    IReadOnlyList<BatchItemSuccess> Successful,
    IReadOnlyList<string> SkippedPaths,
    IReadOnlyList<BatchItemFailure> Failures,
    IReadOnlyList<string> UnprocessedPaths,
    bool IsCanceled)
{
    public int CompletedCount => Successful.Count + SkippedPaths.Count + Failures.Count;
    public string Summary =>
        $"{Successful.Count} succeeded, {SkippedPaths.Count} skipped, " +
        $"{Failures.Count} failed, {UnprocessedPaths.Count} not started" +
        (IsCanceled ? " · canceled" : "");
}
