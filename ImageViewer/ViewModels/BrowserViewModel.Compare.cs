using System;
using System.Collections.Generic;
using System.Linq;
using ImageViewer.Models;
using ImageViewer.Services;

namespace ImageViewer.ViewModels;

public partial class BrowserViewModel
{
    public void ApplyCompareDecisions(IReadOnlyList<CompareCandidateDecision> decisions)
    {
        var byPath = decisions.ToDictionary(
            decision => decision.Path,
            FileSystemPath.Comparer);
        foreach (var item in Items)
        {
            if (!byPath.TryGetValue(item.Path, out var decision)) continue;
            item.CompareMark = decision.Mark;
        }
    }

    public void RestoreSelectionByPaths(
        IReadOnlyList<string> selectedPaths,
        string? focusedPath)
    {
        var selected = new HashSet<string>(selectedPaths, FileSystemPath.Comparer);
        _selection.Clear();
        foreach (var item in Items)
            if (selected.Contains(item.Path)) _selection.Toggle(item);

        var focused = Items.FirstOrDefault(item =>
            FileSystemPath.Equals(item.Path, focusedPath));
        if (focused is not null && _selection.IsSelected(focused))
            _selection.FocusOnly(focused);
        var focusedIndex = focused is null ? -1 : FilteredItems.IndexOf(focused);
        if (focusedIndex < 0)
        {
            var firstSelected = FilteredItems.FirstOrDefault(_selection.IsSelected);
            focusedIndex = firstSelected is null ? -1 : FilteredItems.IndexOf(firstSelected);
        }
        SetFocusedIndex(focusedIndex);
        SyncSelectionVisuals();
    }
}
