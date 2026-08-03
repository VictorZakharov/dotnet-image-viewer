using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.VisualTree;
using ImageViewer.ViewModels;

namespace ImageViewer.Views;

public partial class BrowserView
{
    private PointerPressedEventArgs? _dragStartEvent;
    private Point _dragStartPoint;
    private IReadOnlyList<string> _draggedPaths = Array.Empty<string>();
    private IPointer? _dragPointer;
    private bool _dragStarting;

    internal bool IsInternalItemDragActive => _dragStarting;

    private void PrepareItemDrag(PointerPressedEventArgs e, Control source)
    {
        if (_vm is not { } vm || !e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            ClearItemDrag();
            return;
        }

        _draggedPaths = vm.SelectedPaths;
        if (_draggedPaths.Count == 0)
        {
            ClearItemDrag();
            return;
        }

        _dragStartEvent = e;
        _dragStartPoint = e.GetPosition(this);
        _dragPointer = e.Pointer;
        _dragPointer.Capture(source);
    }

    private async void OnThumbnailPointerMoved(object? sender, PointerEventArgs e)
    {
        if (_dragStarting || _dragStartEvent is null || _draggedPaths.Count == 0) return;
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            ClearItemDrag();
            return;
        }

        var current = e.GetPosition(this);
        if (Math.Abs(current.X - _dragStartPoint.X) < 5 &&
            Math.Abs(current.Y - _dragStartPoint.Y) < 5) return;

        _dragStarting = true;
        try
        {
            var storageItems = await ResolveStorageItemsAsync(_draggedPaths);
            if (storageItems.Count == 0) return;

            var transfer = new DataTransfer();
            foreach (var file in storageItems)
                transfer.Add(DataTransferItem.CreateFile(file));
            await DragDrop.DoDragDropAsync(
                _dragStartEvent,
                transfer,
                DragDropEffects.Move);
        }
        catch (Exception ex)
        {
            _vm?.ReportFileOperation($"Could not start item drag: {ex.Message}");
        }
        finally
        {
            ClearItemDrag();
        }
    }

    private void OnThumbnailPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (!_dragStarting) ClearItemDrag();
    }

    private void OnFolderTreeDragOver(object? sender, DragEventArgs e)
    {
        var target = FindFolderTarget(e.Source as Visual);
        e.DragEffects = target is not null && CanDropOnFolder(target.Path)
            ? DragDropEffects.Move
            : DragDropEffects.None;
        e.Handled = true;
    }

    private async void OnFolderTreeDrop(object? sender, DragEventArgs e)
    {
        var target = FindFolderTarget(e.Source as Visual);
        var paths = _draggedPaths.ToArray();
        e.Handled = true;
        if (target is null || paths.Length == 0 || !CanDropOnFolder(target.Path)) return;

        await MoveFilesToFolderAsync(paths, target.Path);
    }

    private bool CanDropOnFolder(string destination)
    {
        if (!Directory.Exists(destination)) return false;

        var normalizedDestination = NormalizePath(destination);
        if (_draggedPaths.Any(path => Directory.Exists(path) &&
            IsSameOrDescendant(normalizedDestination, NormalizePath(path)))) return false;

        return _draggedPaths.Any(path => !string.Equals(
            NormalizePath(Path.GetDirectoryName(path) ?? ""),
            normalizedDestination,
            StringComparison.OrdinalIgnoreCase));
    }

    private static FolderTreeItem? FindFolderTarget(Visual? source)
    {
        for (var current = source; current is not null; current = current.GetVisualParent())
        {
            if (current is TreeViewItem { DataContext: FolderTreeItem item } &&
                !string.IsNullOrEmpty(item.Path)) return item;
        }
        return null;
    }

    private static string NormalizePath(string path) =>
        Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

    private static bool IsSameOrDescendant(string candidate, string parent) =>
        string.Equals(candidate, parent, StringComparison.OrdinalIgnoreCase) ||
        candidate.StartsWith(parent + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);

    private void ClearItemDrag()
    {
        try { _dragPointer?.Capture(null); }
        catch { /* the platform drag may already have released this pointer */ }
        _dragPointer = null;
        _dragStartEvent = null;
        _draggedPaths = Array.Empty<string>();
        _dragStarting = false;
    }
}
