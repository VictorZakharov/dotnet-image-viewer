using System;
using System.IO;
using Avalonia.Threading;

namespace ImageViewer.ViewModels;

public partial class BrowserViewModel
{
    private ThumbnailItem? _renamingItem;
    private ThumbnailItem? _selectedVisualItem;

    public void OpenSelected()
    {
        if (SelectedPath is { } p)
            OpenRequested?.Invoke(p);
    }

    public void SelectIndex(int index)
    {
        var clamped = FilteredItems.Count == 0
            ? -1
            : Math.Clamp(index, -1, FilteredItems.Count - 1);
        if (SelectedIndex == clamped)
        {
            SyncSelectedVisual();
            return;
        }

        SelectedIndex = clamped;
    }

    public void SelectItem(ThumbnailItem item)
    {
        var index = FilteredItems.IndexOf(item);
        if (index >= 0) SelectIndex(index);
    }

    private void SyncSelectedVisual()
    {
        var selected = SelectedItem;
        if (ReferenceEquals(selected, _selectedVisualItem)) return;

        if (_selectedVisualItem is not null)
            _selectedVisualItem.IsSelected = false;
        _selectedVisualItem = selected;
        if (_selectedVisualItem is not null)
            _selectedVisualItem.IsSelected = true;
    }

    public void BeginRenameSelected()
    {
        if (SelectedIndex < 0 || SelectedIndex >= FilteredItems.Count) return;
        var target = FilteredItems[SelectedIndex];
        if (target.IsFolder) return;

        if (_renamingItem is not null && !ReferenceEquals(_renamingItem, target) && _renamingItem.IsRenaming)
        {
            var prev = _renamingItem;
            _renamingItem = null;
            if (prev.TryCommitRename(out _)) ResortItems();
        }

        _renamingItem = target;
        target.BeginRename();
        RenameRequested?.Invoke(target);
    }

    public void CommitRename(ThumbnailItem item)
    {
        var committed = item.TryCommitRename(out _);
        if (ReferenceEquals(_renamingItem, item)) _renamingItem = null;
        if (committed) ResortItems();
    }

    public void CancelRename(ThumbnailItem item)
    {
        item.CancelRename();
        if (ReferenceEquals(_renamingItem, item)) _renamingItem = null;
    }

    partial void OnSelectedIndexChanged(int value)
    {
        SyncSelectedVisual();

        if (_renamingItem is not null)
        {
            var newItem = (value >= 0 && value < FilteredItems.Count) ? FilteredItems[value] : null;
            if (!ReferenceEquals(_renamingItem, newItem))
            {
                var prev = _renamingItem;
                _renamingItem = null;
                Dispatcher.UIThread.Post(() =>
                {
                    if (!prev.IsRenaming) return;
                    if (prev.TryCommitRename(out _)) ResortItems();
                });
            }
        }

        if (ShowExifPane) LoadCurrentExif();
    }

    public void BeginEditPath()
    {
        PathEditText = CurrentFolder ?? "";
        PathEditHasError = false;
        PathEditErrorMessage = "";
        IsEditingPath = true;
    }

    public void TryCommitPathEdit()
    {
        var p = (PathEditText ?? "").Trim();
        if (p.Length >= 2 && p[0] == '"' && p[^1] == '"') p = p[1..^1].Trim();
        if (string.IsNullOrEmpty(p))
        {
            PathEditHasError = true;
            PathEditErrorMessage = "Path is empty.";
            return;
        }
        try
        {
            if (!Directory.Exists(p))
            {
                PathEditHasError = true;
                PathEditErrorMessage = "Folder not found.";
                return;
            }
        }
        catch
        {
            PathEditHasError = true;
            PathEditErrorMessage = "Path is not valid.";
            return;
        }
        PathEditHasError = false;
        IsEditingPath = false;
        OpenRequested?.Invoke(p);
    }

    public void CancelPathEdit()
    {
        IsEditingPath = false;
        PathEditHasError = false;
        PathEditErrorMessage = "";
    }
}
