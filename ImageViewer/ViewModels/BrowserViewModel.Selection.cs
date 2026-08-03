using System;
using System.Collections.Generic;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using ImageViewer.Models;

namespace ImageViewer.ViewModels;

public partial class BrowserViewModel
{
    private readonly SelectionModel<ThumbnailItem> _selection = new();
    private HashSet<ThumbnailItem> _visualSelection = new();

    [ObservableProperty] private int _selectedCount;
    [ObservableProperty] private int _selectedFileCount;
    [ObservableProperty] private string _selectionSummary = "";

    public bool HasSelection => SelectedCount > 0;
    public bool HasSelectedFiles => SelectedFileCount > 0;
    public IReadOnlyList<ThumbnailItem> SelectedItems =>
        Items.Where(_selection.IsSelected).ToList();
    public IReadOnlyList<ThumbnailItem> SelectedFiles =>
        Items.Where(item => item.IsFile && _selection.IsSelected(item)).ToList();

    public void SelectIndex(int index)
    {
        var item = ItemAt(index);
        if (item is null)
        {
            ClearSelection();
            SetFocusedIndex(-1);
            return;
        }

        _selection.SelectOnly(item);
        SetFocusedIndex(index);
        SyncSelectionVisuals();
    }

    public void SelectItem(ThumbnailItem item, bool toggle = false, bool extend = false)
    {
        var index = FilteredItems.IndexOf(item);
        if (index < 0) return;

        if (extend)
            _selection.SelectRange(FilteredItems, item, additive: toggle);
        else if (toggle)
            _selection.Toggle(item);
        else
            _selection.SelectOnly(item);

        SetFocusedIndex(index);
        SyncSelectionVisuals();
    }

    public void SelectForContextMenu(ThumbnailItem item)
    {
        var index = FilteredItems.IndexOf(item);
        if (index < 0) return;
        if (_selection.IsSelected(item))
            _selection.FocusOnly(item);
        else
            _selection.SelectOnly(item);
        SetFocusedIndex(index);
        SyncSelectionVisuals();
    }

    public void NavigateSelection(int index, bool extend, bool preserveSelection)
    {
        var item = ItemAt(index);
        if (item is null) return;

        if (extend)
            _selection.SelectRange(FilteredItems, item, additive: preserveSelection);
        else if (preserveSelection)
            _selection.FocusOnly(item);
        else
            _selection.SelectOnly(item);

        SetFocusedIndex(index);
        SyncSelectionVisuals();
    }

    public void ToggleFocusedSelection()
    {
        if (SelectedItem is not { } item) return;
        _selection.Toggle(item);
        SyncSelectionVisuals();
    }

    public void SelectAll()
    {
        _selection.SelectAll(FilteredItems);
        if (SelectedIndex < 0 && FilteredItems.Count > 0) SetFocusedIndex(0);
        SyncSelectionVisuals();
    }

    private void ClearSelection()
    {
        _selection.Clear();
        SyncSelectionVisuals();
    }

    private void PreserveSelectionAfterCollectionChange(ThumbnailItem? focusedItem)
    {
        _selection.Reconcile(Items);
        var focusedIndex = focusedItem is null ? -1 : FilteredItems.IndexOf(focusedItem);
        if (focusedIndex < 0)
        {
            var visibleSelected = FilteredItems.FirstOrDefault(_selection.IsSelected);
            focusedIndex = visibleSelected is null ? -1 : FilteredItems.IndexOf(visibleSelected);
        }
        if (focusedIndex < 0 && FilteredItems.Count > 0) focusedIndex = 0;
        if (focusedIndex >= 0) _selection.FocusOnly(FilteredItems[focusedIndex]);
        SetFocusedIndex(focusedIndex);
        SyncSelectionVisuals();
    }

    private void SetFocusedIndex(int index)
    {
        var clamped = FilteredItems.Count == 0
            ? -1
            : Math.Clamp(index, -1, FilteredItems.Count - 1);
        if (SelectedIndex != clamped) SelectedIndex = clamped;
    }

    private ThumbnailItem? ItemAt(int index) =>
        index >= 0 && index < FilteredItems.Count ? FilteredItems[index] : null;

    private void SyncSelectionVisuals()
    {
        var selected = _selection.Selected.ToHashSet();
        foreach (var item in _visualSelection)
        {
            if (selected.Contains(item)) continue;
            item.IsSelected = false;
            item.ShowSelectionCheckmark = false;
        }

        var showCheckmarks = selected.Count > 1;
        foreach (var item in selected)
        {
            item.IsSelected = true;
            item.ShowSelectionCheckmark = showCheckmarks;
        }
        _visualSelection = selected;

        SelectedCount = selected.Count;
        SelectedFileCount = selected.Count(item => item.IsFile);
        OnPropertyChanged(nameof(HasSelection));
        OnPropertyChanged(nameof(HasSelectedFiles));
        OnPropertyChanged(nameof(SelectedItems));
        OnPropertyChanged(nameof(SelectedFiles));
        SelectionSummary = FormatSelectionSummary(selected);
    }

    private static string FormatSelectionSummary(IReadOnlyCollection<ThumbnailItem> selected)
    {
        if (selected.Count == 0) return "";
        var files = selected.Where(item => item.IsFile).ToList();
        var folderCount = selected.Count - files.Count;
        var size = files.Sum(item => item.FileSize);
        var parts = new List<string> { $"{selected.Count} selected" };
        if (files.Count > 0 && folderCount > 0)
            parts.Add($"{files.Count} file{(files.Count == 1 ? "" : "s")}");
        if (folderCount > 0)
            parts.Add($"{folderCount} folder{(folderCount == 1 ? "" : "s")}");
        if (files.Count > 0) parts.Add(FormatSize(size));
        return string.Join(" · ", parts);
    }

    private static string FormatSize(long bytes)
    {
        string[] units = { "B", "KB", "MB", "GB", "TB" };
        double value = bytes;
        var unit = 0;
        while (value >= 1024 && unit < units.Length - 1)
        {
            value /= 1024;
            unit++;
        }
        return unit == 0 ? $"{bytes} B" : $"{value:0.#} {units[unit]}";
    }
}
