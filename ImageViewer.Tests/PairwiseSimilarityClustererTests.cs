using ImageViewer.Services;

namespace ImageViewer.Tests;

public sealed class PairwiseSimilarityClustererTests
{
    [Fact]
    public void SimilarityChainDoesNotCreateTransitiveGroup()
    {
        HashSet<int>[] neighbors = [
            [1],
            [0, 2],
            [1]
        ];

        var clusters = PairwiseSimilarityClusterer.Create(neighbors);

        var cluster = Assert.Single(clusters);
        Assert.Equal(2, cluster.Count);
        Assert.Contains(cluster[1], neighbors[cluster[0]]);
    }

    [Fact]
    public void PairwiseSimilarFilesRemainInOneGroup()
    {
        HashSet<int>[] neighbors = [
            [1, 2],
            [0, 2],
            [0, 1]
        ];

        var cluster = Assert.Single(PairwiseSimilarityClusterer.Create(neighbors));

        Assert.Equal([0, 1, 2], cluster.Order());
    }
}
