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
    private IReadOnlyList<string> _draggedFilePaths = Array.Empty<string>();
    private IPointer? _dragPointer;
    private bool _dragStarting;

    internal bool IsInternalFileDragActive => _dragStarting;

    private void PrepareFileDrag(PointerPressedEventArgs e, Control source)
    {
        if (_vm is not { } vm || !e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            ClearFileDrag();
            return;
        }

        _draggedFilePaths = vm.SelectedFilePaths;
        if (_draggedFilePaths.Count == 0)
        {
            ClearFileDrag();
            return;
        }

        _dragStartEvent = e;
        _dragStartPoint = e.GetPosition(this);
        _dragPointer = e.Pointer;
        _dragPointer.Capture(source);
    }

    private async void OnThumbnailPointerMoved(object? sender, PointerEventArgs e)
    {
        if (_dragStarting || _dragStartEvent is null || _draggedFilePaths.Count == 0) return;
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            ClearFileDrag();
            return;
        }

        var current = e.GetPosition(this);
        if (Math.Abs(current.X - _dragStartPoint.X) < 5 &&
            Math.Abs(current.Y - _dragStartPoint.Y) < 5) return;

        _dragStarting = true;
        try
        {
            var storageItems = await ResolveStorageFilesAsync(_draggedFilePaths);
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
            _vm?.ReportFileOperation($"Could not start file drag: {ex.Message}");
        }
        finally
        {
            ClearFileDrag();
        }
    }

    private void OnThumbnailPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (!_dragStarting) ClearFileDrag();
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
        var paths = _draggedFilePaths.ToArray();
        e.Handled = true;
        if (target is null || paths.Length == 0 || !CanDropOnFolder(target.Path)) return;

        await MoveFilesToFolderAsync(paths, target.Path);
    }

    private bool CanDropOnFolder(string destination)
    {
        if (!Directory.Exists(destination)) return false;

        return _draggedFilePaths.Any(path => !string.Equals(
            Path.GetDirectoryName(path)?.TrimEnd(Path.DirectorySeparatorChar),
            destination.TrimEnd(Path.DirectorySeparatorChar),
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

    private void ClearFileDrag()
    {
        try { _dragPointer?.Capture(null); }
        catch { /* the platform drag may already have released this pointer */ }
        _dragPointer = null;
        _dragStartEvent = null;
        _draggedFilePaths = Array.Empty<string>();
        _dragStarting = false;
    }
}
