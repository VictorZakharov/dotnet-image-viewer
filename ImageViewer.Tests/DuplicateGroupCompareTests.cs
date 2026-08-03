using ImageViewer.Models;
using ImageViewer.ViewModels;

namespace ImageViewer.Tests;

public sealed class DuplicateGroupCompareTests
{
    [Fact]
    public void CheckedAlternativesStillCompareAgainstSuggestedKeeper()
    {
        using var group = CreateGroup(5);
        foreach (var file in group.Files.Skip(1).Take(4)) file.IsSelected = true;

        var paths = group.GetComparisonPaths();

        Assert.Equal(4, paths.Count);
        Assert.Contains("image-0.jpg", paths);
    }

    [Fact]
    public void CompareChoiceBecomesKeeperAndRejectsBecomeReviewSelection()
    {
        using var group = CreateGroup(3);
        group.ApplyCompareResult(new ImageCompareResult([
            new CompareCandidateDecision("image-0.jpg", CompareMark.Reject),
            new CompareCandidateDecision("image-1.jpg", CompareMark.Pick),
            new CompareCandidateDecision("image-2.jpg", CompareMark.Reject)
        ], []));

        Assert.True(group.Files[1].IsSuggestedKeeper);
        Assert.False(group.Files[1].IsSelected);
        Assert.True(group.Files[0].IsSelected);
        Assert.True(group.Files[2].IsSelected);
        Assert.Contains("chosen in side-by-side compare", group.KeeperRule);
    }

    private static DuplicateGroupViewModel CreateGroup(int count)
    {
        var files = Enumerable.Range(0, count)
            .Select(index => new DuplicateFileEntry
            {
                Path = $"image-{index}.jpg",
                ContentHash = $"hash-{index}",
                PerceptualHash = (ulong)index,
                SizeBytes = 100 + index,
                CreatedUtc = DateTime.UtcNow,
                ModifiedUtc = DateTime.UtcNow,
                AccessedUtc = DateTime.UtcNow,
                Width = 1000 + index,
                Height = 800 + index
            }).ToList();
        return new DuplicateGroupViewModel(new DuplicateGroup
        {
            Kind = DuplicateGroupKind.Similar,
            Files = files,
            SuggestedKeeperPath = files[0].Path,
            KeeperReason = "test rule",
            SimilarityThreshold = 8,
            MaximumDistance = 2
        }, () => { });
    }
}
