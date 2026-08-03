using System.Collections.Generic;
using System.Linq;

namespace ImageViewer.Models;

public sealed class SelectionModel<T> where T : class
{
    private readonly HashSet<T> _selected = new();

    public IReadOnlyCollection<T> Selected => _selected;
    public T? Anchor { get; private set; }
    public T? Focus { get; private set; }
    public int Count => _selected.Count;

    public bool IsSelected(T item) => _selected.Contains(item);

    public void Clear()
    {
        _selected.Clear();
        Anchor = default;
        Focus = default;
    }

    public void SelectOnly(T item)
    {
        _selected.Clear();
        _selected.Add(item);
        Anchor = item;
        Focus = item;
    }

    public void Toggle(T item)
    {
        if (!_selected.Add(item)) _selected.Remove(item);
        Anchor = item;
        Focus = item;
    }

    public void FocusOnly(T item)
    {
        Focus = item;
        Anchor ??= item;
    }

    public void SelectRange(IReadOnlyList<T> orderedItems, T item, bool additive)
    {
        var anchorIndex = IndexOf(orderedItems, Anchor);
        var itemIndex = IndexOf(orderedItems, item);
        if (itemIndex < 0) return;
        if (anchorIndex < 0)
        {
            anchorIndex = IndexOf(orderedItems, Focus);
            Anchor = anchorIndex < 0 ? item : Focus;
            if (anchorIndex < 0) anchorIndex = itemIndex;
        }

        if (!additive) _selected.Clear();
        var first = System.Math.Min(anchorIndex, itemIndex);
        var last = System.Math.Max(anchorIndex, itemIndex);
        for (var index = first; index <= last; index++)
            _selected.Add(orderedItems[index]);
        Focus = item;
    }

    public void SelectAll(IReadOnlyList<T> orderedItems)
    {
        _selected.Clear();
        foreach (var item in orderedItems) _selected.Add(item);
        if (orderedItems.Count == 0)
        {
            Anchor = default;
            Focus = default;
            return;
        }

        if (IndexOf(orderedItems, Anchor) < 0) Anchor = orderedItems[0];
        if (IndexOf(orderedItems, Focus) < 0) Focus = orderedItems[0];
    }

    public void Reconcile(IReadOnlyCollection<T> availableItems)
    {
        _selected.IntersectWith(availableItems);
        if (Anchor is not null && !availableItems.Contains(Anchor)) Anchor = default;
        if (Focus is not null && !availableItems.Contains(Focus)) Focus = default;
    }

    private static int IndexOf(IReadOnlyList<T> items, T? value)
    {
        if (value is null) return -1;
        var comparer = EqualityComparer<T>.Default;
        for (var index = 0; index < items.Count; index++)
            if (comparer.Equals(items[index], value)) return index;
        return -1;
    }
}
