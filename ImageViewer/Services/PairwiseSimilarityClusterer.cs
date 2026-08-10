using System.Collections.Generic;
using System.Linq;

namespace ImageViewer.Services;

internal static class PairwiseSimilarityClusterer
{
    public static List<List<int>> Create(
        IReadOnlyList<HashSet<int>> neighbors)
    {
        var remaining = new HashSet<int>(Enumerable.Range(0, neighbors.Count));
        var result = new List<List<int>>();
        while (remaining.Count > 0)
        {
            var seed = remaining
                .OrderByDescending(index => RemainingDegree(neighbors[index], remaining))
                .ThenBy(index => index)
                .First();
            var cluster = new List<int> { seed };
            var candidates = neighbors[seed]
                .Where(remaining.Contains)
                .OrderByDescending(index => RemainingDegree(neighbors[index], remaining))
                .ThenBy(index => index)
                .ToList();
            foreach (var candidate in candidates)
                if (cluster.All(member => neighbors[candidate].Contains(member)))
                    cluster.Add(candidate);

            foreach (var index in cluster) remaining.Remove(index);
            if (cluster.Count > 1) result.Add(cluster);
        }

        return result;
    }

    private static int RemainingDegree(
        HashSet<int> neighbors,
        HashSet<int> remaining) => neighbors.Count(remaining.Contains);
}
