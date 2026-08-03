using System;
using System.IO;
using Avalonia.Threading;

namespace ImageViewer.ViewModels;

public partial class BrowserViewModel
{
    private ThumbnailItem? _renamingItem;

    public void OpenSelected()
    {
        if (SelectedPath is { } p)
            OpenRequested?.Invoke(p);
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
            var oldPath = prev.Path;
            if (prev.TryCommitRename(out var newPath))
            {
                ReportRename(oldPath, newPath);
                ResortItems();
            }
        }

        _renamingItem = target;
        target.BeginRename();
        RenameRequested?.Invoke(target);
    }

    public void CommitRename(ThumbnailItem item)
    {
        var oldPath = item.Path;
        var committed = item.TryCommitRename(out var newPath);
        if (ReferenceEquals(_renamingItem, item)) _renamingItem = null;
        if (committed)
        {
            ReportRename(oldPath, newPath);
            ResortItems();
        }
    }

    public void CancelRename(ThumbnailItem item)
    {
        item.CancelRename();
        if (ReferenceEquals(_renamingItem, item)) _renamingItem = null;
    }

    partial void OnSelectedIndexChanged(int value)
    {
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
                    var oldPath = prev.Path;
                    if (prev.TryCommitRename(out var newPath))
                    {
                        ReportRename(oldPath, newPath);
                        ResortItems();
                    }
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
