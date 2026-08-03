using System.Collections.Generic;
using System.Linq;

namespace ImageViewer.Models;

public enum CompareMark
{
    Neutral,
    Pick,
    Reject
}

public sealed record CompareCandidateDecision(
    string Path,
    CompareMark Mark,
    int Rating);

public sealed record ImageCompareResult(
    IReadOnlyList<CompareCandidateDecision> Decisions,
    IReadOnlyList<string> DeletedPaths)
{
    public string? PickedPath => Decisions
        .Where(decision => decision.Mark == CompareMark.Pick)
        .Select(decision => decision.Path)
        .FirstOrDefault();

    public IReadOnlyList<string> RejectedPaths => Decisions
        .Where(decision => decision.Mark == CompareMark.Reject)
        .Select(decision => decision.Path)
        .ToList();
}
