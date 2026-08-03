using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ImageViewer.Models;
using ImageViewer.Services;

namespace ImageViewer.ViewModels;

public partial class BrowserViewModel
{
    public void ApplyFileOperationChanges(FileOperationResult result)
    {
        if (result.Kind == FileOperationKind.Delete)
        {
            ApplyDeletedPaths(result.Successful.Select(item => item.SourcePath).ToList());
            return;
        }

        if (result.Kind == FileOperationKind.Move)
            ApplyDeletedPaths(result.Successful.Select(item => item.SourcePath).ToList());
        ApplyTransferredDestinations(result.Successful.Select(item => item.DestinationPath));
    }

    private void ApplyTransferredDestinations(IEnumerable<string?> destinationPaths)
    {
        if (string.IsNullOrEmpty(CurrentFolder)) return;
        var destinations = destinationPaths
            .Where(path => !string.IsNullOrEmpty(path) && IsInCurrentFolder(path!))
            .Cast<string>()
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (destinations.Count == 0) return;

        var focusedItem = SelectedItem;
        var focusedPath = SelectedPath;
        var selectedPaths = SelectedPaths;
        var destinationSet = new HashSet<string>(
            destinations,
            StringComparer.OrdinalIgnoreCase);
        var replacedItems = Items
            .Where(item => destinationSet.Contains(item.Path))
            .ToList();
        foreach (var item in replacedItems)
        {
            Items.Remove(item);
            FilteredItems.Remove(item);
        }

        var addedItems = destinations
            .Select(CreateTransferredItem)
            .Where(item => item is not null)
            .Cast<ThumbnailItem>()
            .ToList();
        InsertInCurrentOrder(Items, ApplySortToItems(Items.Concat(addedItems)), addedItems);

        var visibleOrder = ApplySortToItems(Items.Where(MatchesCurrentFilter));
        InsertInCurrentOrder(
            FilteredItems,
            visibleOrder,
            addedItems.Where(MatchesCurrentFilter));

        if (replacedItems.Count > 0)
            RestoreSelectionByPaths(selectedPaths, focusedPath);
        else if (focusedItem is not null)
            SetFocusedIndex(FilteredItems.IndexOf(focusedItem));

        ReconcileThumbnailsAfterCollectionChange();
        UpdateItemsSummary();
        AddTransferredFoldersToTree(addedItems.Where(item => item.IsFolder));
        ThumbnailRequestsInvalidated?.Invoke();
        DisposeItems(replacedItems);
    }

    private static void InsertInCurrentOrder(
        IList<ThumbnailItem> target,
        IReadOnlyList<ThumbnailItem> finalOrder,
        IEnumerable<ThumbnailItem> addedItems)
    {
        var positions = finalOrder
            .Select((item, index) => (item, index))
            .ToDictionary(pair => pair.item, pair => pair.index);
        foreach (var item in addedItems.OrderBy(item => positions[item]))
            target.Insert(positions[item], item);
    }

    private ThumbnailItem? CreateTransferredItem(string path)
    {
        if (Directory.Exists(path)) return ThumbnailItem.CreateFolder(path);
        if (!File.Exists(path) || !MediaFileTypes.IsSupported(path)) return null;
        return MediaFileTypes.IsVideo(path)
            ? ThumbnailItem.CreateVideo(path)
            : new ThumbnailItem(path);
    }

    private bool MatchesCurrentFilter(ThumbnailItem item)
    {
        var filter = FilterText.Trim();
        return filter.Length == 0 || Path.GetFileName(item.Path)
            .Contains(filter, StringComparison.OrdinalIgnoreCase);
    }

    private bool IsInCurrentFolder(string path)
    {
        try
        {
            var parent = Path.GetDirectoryName(Path.GetFullPath(path));
            return string.Equals(
                parent?.TrimEnd('\\', '/'),
                CurrentFolder?.TrimEnd('\\', '/'),
                StringComparison.OrdinalIgnoreCase);
        }
        catch { return false; }
    }

    private void AddTransferredFoldersToTree(IEnumerable<ThumbnailItem> folders)
    {
        if (SelectedTreeItem is not FolderTreeItem parent
            || !string.Equals(parent.Path, CurrentFolder, StringComparison.OrdinalIgnoreCase)
            || parent.Children.Any(child => string.IsNullOrEmpty(child.Path))) return;

        foreach (var folder in folders)
        {
            if (parent.Children.Any(child => string.Equals(
                    child.Path, folder.Path, StringComparison.OrdinalIgnoreCase))) continue;
            var node = new FolderTreeItem(folder.Path, folder.FileName);
            var index = parent.Children.TakeWhile(child => StringComparer.OrdinalIgnoreCase.Compare(
                child.Name, node.Name) < 0).Count();
            parent.Children.Insert(index, node);
        }
    }
}
