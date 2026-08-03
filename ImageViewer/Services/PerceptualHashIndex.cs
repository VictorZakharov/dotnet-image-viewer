using System.Collections.Generic;

namespace ImageViewer.Services;

internal sealed class PerceptualHashIndex
{
    private Node? _root;

    public void Add(ulong hash, int index)
    {
        if (_root is null)
        {
            _root = new Node(hash, index);
            return;
        }

        var node = _root;
        while (true)
        {
            var distance = DuplicateImageHasher.Distance(hash, node.Hash);
            if (!node.Children.TryGetValue(distance, out var child))
            {
                node.Children[distance] = new Node(hash, index);
                return;
            }
            node = child;
        }
    }

    public void FindWithin(ulong hash, int threshold, List<int> results)
    {
        if (_root is null) return;
        var pending = new Stack<Node>();
        pending.Push(_root);
        while (pending.Count > 0)
        {
            var node = pending.Pop();
            var distance = DuplicateImageHasher.Distance(hash, node.Hash);
            if (distance <= threshold) results.Add(node.Index);
            var minimum = distance - threshold;
            var maximum = distance + threshold;
            foreach (var child in node.Children)
                if (child.Key >= minimum && child.Key <= maximum)
                    pending.Push(child.Value);
        }
    }

    private sealed record Node(ulong Hash, int Index)
    {
        public Dictionary<int, Node> Children { get; } = [];
    }
}
