using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Threading;
using ImageViewer.Services;

namespace ImageViewer.ViewModels;

public partial class BrowserViewModel
{
    private bool _suppressTreeSelectionLoad;
    private int _treeSyncVersion;
    private int _folderLoadVersion;
    private CancellationTokenSource? _folderLoadCts;
    private Task? _loadDrivesTask;

    public Task EnsureDrivesLoadedAsync() =>
        _loadDrivesTask ??= LoadDrivesAsync();

    public async Task RefreshFolderTreeAsync()
    {
        if (_loadDrivesTask is { } pendingLoad)
        {
            try { await pendingLoad; }
            catch { /* the replacement load below can recover */ }
        }

        _suppressTreeSelectionLoad = true;
        try { SelectedTreeItem = null; }
        finally { _suppressTreeSelectionLoad = false; }
        _loadDrivesTask = LoadDrivesAsync();
        await _loadDrivesTask;
    }

    private async Task LoadDrivesAsync()
    {
        try
        {
            var drives = await Task.Run(FileSystemRoots.Get).ConfigureAwait(false);

            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                DriveTree.Clear();
                foreach (var drive in drives)
                {
                    // Never block UI creation by enumerating drive roots.
                    // Expanding a drive replaces this hint with actual children.
                    DriveTree.Add(new FolderTreeItem(
                        drive.Path,
                        drive.Label,
                        probeForChildren: false));
                }

                if (!string.IsNullOrEmpty(CurrentFolder))
                    _ = SyncTreeSelectionAsync(CurrentFolder);
            });
        }
        catch { /* drives inaccessible */ }
    }

    partial void OnSelectedTreeItemChanged(object? value)
    {
        if (_suppressTreeSelectionLoad) return;
        if (value is FolderTreeItem item && !string.IsNullOrEmpty(item.Path))
            OpenRequested?.Invoke(item.Path);
    }

    public async Task SyncTreeSelectionAsync(string folder)
    {
        if (string.IsNullOrEmpty(folder)) return;
        var syncVersion = ++_treeSyncVersion;

        var chain = new List<string>();
        try
        {
            var cursor = new DirectoryInfo(folder);
            while (cursor is not null)
            {
                chain.Add(cursor.FullName);
                cursor = cursor.Parent;
            }
        }
        catch { return; }

        if (chain.Count == 0) return;
        chain.Reverse();

        var drive = DriveTree.FirstOrDefault(d =>
            FileSystemPath.Equals(NormalizeRoot(d.Path), NormalizeRoot(chain[0])));
        if (drive is null) return;

        FolderTreeItem? current = drive;
        for (int i = 1; i < chain.Count && current is not null; i++)
        {
            if (!current.IsExpanded) current.IsExpanded = true;
            await current.EnsureChildrenLoadedAsync();
            if (syncVersion != _treeSyncVersion) return;
            current = current.Children.FirstOrDefault(c =>
                FileSystemPath.Equals(c.Path, chain[i]));
        }

        if (current is null) return;

        _suppressTreeSelectionLoad = true;
        try { SelectedTreeItem = current; }
        finally { _suppressTreeSelectionLoad = false; }
        TreeNodeFocused?.Invoke(current);
    }

    private static string NormalizeRoot(string p) => p.TrimEnd('\\', '/');

    public ThumbnailItem? SelectedItem =>
        SelectedIndex >= 0 && SelectedIndex < FilteredItems.Count
            ? FilteredItems[SelectedIndex]
            : null;

    public string? SelectedPath => SelectedItem?.Path;

    public Task LoadFolderAsync(string folder) => LoadFolderCoreAsync(folder, force: false);

    private async Task LoadFolderCoreAsync(string folder, bool force)
    {
        if (!force && FileSystemPath.Equals(CurrentFolder, folder) && Items.Count > 0)
        {
            _ = SyncTreeSelectionAsync(folder);
            return;
        }

        var loadVersion = ++_folderLoadVersion;
        _folderLoadCts?.Cancel();
        _folderLoadCts?.Dispose();
        _folderLoadCts = new CancellationTokenSource();
        var ct = _folderLoadCts.Token;

        ResetThumbnailRequests();
        SelectIndex(-1);
        DisposeItems(Items);
        Items.ReplaceAll(Array.Empty<ThumbnailItem>());
        FilteredItems.ReplaceAll(Array.Empty<ThumbnailItem>());

        CurrentFolder = folder;
        FilterText = "";
        ItemsSummary = "Loading...";
        IsLoading = true;

        List<ThumbnailItem>? sortedEntries = null;
        var published = false;
        try
        {
            var contents = await FolderScanner.ScanBrowserAsync(folder, ct);
            var sortMode = SortMode;
            var sortDescending = SortDescending;
            sortedEntries = await Task.Run(() =>
            {
                var entries = contents.Folders
                    .Select(entry => ThumbnailItem.CreateFolder(entry.Path))
                    .Concat(contents.Media.Select(entry => entry.IsVideo
                        ? ThumbnailItem.CreateVideo(entry.Path)
                        : new ThumbnailItem(entry.Path)));
                return ApplySortToItems(entries, sortMode, sortDescending);
            }, ct);

            ct.ThrowIfCancellationRequested();
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                if (ct.IsCancellationRequested || loadVersion != _folderLoadVersion)
                    return;

                Items.ReplaceAll(sortedEntries);
                FilteredItems.ReplaceAll(sortedEntries);
                published = true;
                UpdateItemsSummary();
                SelectIndex(FilteredItems.Count > 0 ? 0 : -1);
                ThumbnailRequestsInvalidated?.Invoke();
            });
        }
        catch (OperationCanceledException) { /* expected */ }
        catch { /* ignore folder read errors */ }
        finally
        {
            if (!published && sortedEntries is not null)
                DisposeItems(sortedEntries);

            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                if (loadVersion != _folderLoadVersion) return;
                IsLoading = false;
                UpdateItemsSummary();
                _ = SyncTreeSelectionAsync(folder);
            });
        }
    }
}
