using System.Collections.Generic;

namespace ImageViewer.Models;

public enum FileOperationKind
{
    Copy,
    Move,
    Delete
}

public enum FileCollisionChoice
{
    Skip,
    Replace,
    Rename,
    Cancel
}

public enum BrowserFileCommand
{
    Copy,
    Cut,
    Paste,
    Move,
    Delete,
    Undo
}

public sealed record FileOperationRequest(
    FileOperationKind Kind,
    IReadOnlyList<string> SourcePaths,
    string? DestinationFolder = null);

public sealed record FileTransferPair(string SourcePath, string DestinationPath);

public sealed record FileCollision(
    string SourcePath,
    string DestinationPath,
    bool IsSamePath);

public sealed record FileCollisionDecision(
    FileCollisionChoice Choice,
    bool ApplyToRemaining);

public sealed record FileOperationSuccess(
    string SourcePath,
    string? DestinationPath);

public sealed record FileOperationFailure(
    string SourcePath,
    string? DestinationPath,
    string Error);

public sealed record FileOperationProgress(
    int Completed,
    int Total,
    string CurrentPath)
{
    public double Percentage => Total == 0 ? 0 : Completed * 100d / Total;
}

public sealed record FileOperationResult(
    FileOperationKind Kind,
    IReadOnlyList<FileOperationSuccess> Successful,
    IReadOnlyList<string> SkippedPaths,
    IReadOnlyList<FileOperationFailure> Failures,
    bool IsCanceled);
