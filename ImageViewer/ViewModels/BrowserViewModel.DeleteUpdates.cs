using System;
using System.Collections.Generic;
using System.Linq;
using ImageViewer.Services;

namespace ImageViewer.ViewModels;

public partial class BrowserViewModel
{
    public void ApplyDeletedPaths(IReadOnlyCollection<string> paths)
    {
        if (paths.Count == 0) return;
        var deleted = new HashSet<string>(paths, FileSystemPath.Comparer);
        var focusedItem = SelectedItem;
        var focusedIndex = SelectedIndex;
        var removedItems = Items.Where(item => deleted.Contains(item.Path)).ToList();

        foreach (var item in removedItems)
        {
            Items.Remove(item);
            FilteredItems.Remove(item);
        }

        RestoreSelectionAfterDelete(focusedItem, focusedIndex);
        ReconcileThumbnailsAfterCollectionChange();
        UpdateItemsSummary();
        RemoveDeletedTreeNodes(DriveTree, deleted);
        ThumbnailRequestsInvalidated?.Invoke();
        DisposeItems(removedItems);
    }

    private void RestoreSelectionAfterDelete(ThumbnailItem? focusedItem, int focusedIndex)
    {
        _selection.Reconcile(Items);
        var nextFocus = focusedItem is not null && FilteredItems.Contains(focusedItem)
            ? focusedItem
            : FilteredItems.FirstOrDefault(_selection.IsSelected);
        if (nextFocus is null && FilteredItems.Count > 0)
            nextFocus = FilteredItems[Math.Clamp(focusedIndex, 0, FilteredItems.Count - 1)];

        if (nextFocus is null)
            _selection.Clear();
        else if (_selection.Count == 0)
            _selection.SelectOnly(nextFocus);
        else
            _selection.FocusOnly(nextFocus);

        SetFocusedIndex(nextFocus is null ? -1 : FilteredItems.IndexOf(nextFocus));
        SyncSelectionVisuals();
    }

    private void ReconcileThumbnailsAfterCollectionChange()
    {
        if (_viewportValid && FilteredItems.Count == 0)
            _viewportValid = false;
        else if (_viewportValid)
        {
            var last = FilteredItems.Count - 1;
            _firstVisibleIndex = Math.Clamp(_firstVisibleIndex, 0, last);
            _lastVisibleIndex = Math.Clamp(_lastVisibleIndex, _firstVisibleIndex, last);
            _firstOverscanIndex = Math.Clamp(_firstOverscanIndex, 0, _firstVisibleIndex);
            _lastOverscanIndex = Math.Clamp(_lastOverscanIndex, _lastVisibleIndex, last);
        }
        ReconcileThumbnailRequests();
    }

    private static void RemoveDeletedTreeNodes(
        IEnumerable<FolderTreeItem> parents,
        IReadOnlySet<string> deleted)
    {
        foreach (var parent in parents) RemoveDeletedTreeNodes(parent, deleted);
    }

    private static void RemoveDeletedTreeNodes(
        FolderTreeItem parent,
        IReadOnlySet<string> deleted)
    {
        for (var index = parent.Children.Count - 1; index >= 0; index--)
        {
            var child = parent.Children[index];
            if (deleted.Contains(child.Path)) parent.Children.RemoveAt(index);
            else RemoveDeletedTreeNodes(child, deleted);
        }
    }
}
