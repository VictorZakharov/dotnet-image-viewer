namespace ImageViewer.Services;

internal sealed class DisjointSets
{
    private readonly int[] _parents;
    private readonly byte[] _ranks;

    public DisjointSets(int count)
    {
        _parents = new int[count];
        _ranks = new byte[count];
        for (var index = 0; index < count; index++) _parents[index] = index;
    }

    public int Find(int value)
    {
        if (_parents[value] != value) _parents[value] = Find(_parents[value]);
        return _parents[value];
    }

    public void Union(int left, int right)
    {
        var leftRoot = Find(left);
        var rightRoot = Find(right);
        if (leftRoot == rightRoot) return;
        if (_ranks[leftRoot] < _ranks[rightRoot])
            _parents[leftRoot] = rightRoot;
        else if (_ranks[leftRoot] > _ranks[rightRoot])
            _parents[rightRoot] = leftRoot;
        else
        {
            _parents[rightRoot] = leftRoot;
            _ranks[leftRoot]++;
        }
    }
}
