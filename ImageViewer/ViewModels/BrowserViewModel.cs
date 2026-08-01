using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ImageViewer.Models;
using ImageViewer.Services;

namespace ImageViewer.ViewModels;

public partial class BrowserViewModel : ObservableObject
{
    public AppSettings Settings { get; }
    public ObservableCollection<ThumbnailItem> Items { get; } = new();
    public ObservableCollection<ThumbnailItem> FilteredItems { get; } = new();
    public ObservableCollection<FolderTreeItem> DriveTree { get; } = new();

    [ObservableProperty] private object? _selectedTreeItem;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasFolder), nameof(EmptyStateHint))]
    private string? _currentFolder;

    [ObservableProperty] private int _selectedIndex = -1;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasFilter), nameof(EmptyStateHint))]
    private string _filterText = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SortLabelName), nameof(SortLabelDate), nameof(SortLabelSize))]
    private SortMode _sortMode;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SortLabelName), nameof(SortLabelDate), nameof(SortLabelSize))]
    private bool _sortDescending;

    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private string _itemsSummary = "";
    [ObservableProperty] private bool _showEmptyState = true;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ThumbnailHeight))]
    private int _thumbnailWidth = 200;

    public int ThumbnailHeight => (int)(ThumbnailWidth * 0.78) + 18;

    public const int MinThumbnailSize = 96;
    public const int MaxThumbnailSize = 512;

    private static readonly int[] CacheTiers = { 128, 192, 256, 384, 512 };

    private static int RoundToCacheTier(int dim)
    {
        foreach (var t in CacheTiers) if (dim <= t) return t;
        return CacheTiers[^1];
    }

    private int _activeCacheTier;
    private bool _suppressTreeSelectionLoad;
    private int _treeSyncVersion;
    private ThumbnailItem? _renamingItem;

    [ObservableProperty] private bool _isEditingPath;
    [ObservableProperty] private string _pathEditText = "";
    [ObservableProperty] private bool _pathEditHasError;
    [ObservableProperty] private string _pathEditErrorMessage = "";

    [ObservableProperty] private bool _showExifPane;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasAnyExifData))]
    private Models.ImageMetadata? _exifMetadata;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasExifPaneData))]
    private string? _exifFileName;

    [ObservableProperty] private string? _exifFolder;

    public bool HasExifPaneData => !string.IsNullOrEmpty(ExifFileName);
    public bool HasAnyExifData => ExifMetadata?.HasAnyExif == true;

    public event Action<FolderTreeItem>? TreeNodeFocused;
    public event Action<ThumbnailItem>? RenameRequested;

    public bool HasFilter => !string.IsNullOrEmpty(FilterText);
    public bool HasFolder => !string.IsNullOrEmpty(CurrentFolder);
    public string SortLabelName => SortMode == SortMode.Name ? (SortDescending ? "Name ↓" : "Name ↑") : "Name";
    public string SortLabelDate => SortMode == SortMode.Date ? (SortDescending ? "Date ↓" : "Date ↑") : "Date";
    public string SortLabelSize => SortMode == SortMode.Size ? (SortDescending ? "Size ↓" : "Size ↑") : "Size";

    public string EmptyStateHint
    {
        get
        {
            if (string.IsNullOrEmpty(CurrentFolder))
                return "Click \"Open folder…\", press Ctrl+O, or drag a folder here.";
            if (HasFilter)
                return $"No items match \"{FilterText}\".";
            return "This folder contains no supported images, videos, or visible subfolders.";
        }
    }

    public event Action<string>? OpenRequested;

    private readonly ThumbnailCache _cache = new();
    private CancellationTokenSource? _loadCts;

    public BrowserViewModel(AppSettings settings)
    {
        Settings = settings;
        if (Enum.TryParse(settings.SortMode, true, out SortMode sm)) SortMode = sm;
        SortDescending = settings.SortDescending;
        // Initialize the backing field directly. Going through the generated
        // setter here would synchronously rewrite settings before the first
        // window appears.
        _thumbnailWidth = Math.Clamp(settings.ThumbnailSize, MinThumbnailSize, MaxThumbnailSize);
        _activeCacheTier = RoundToCacheTier(_thumbnailWidth);
        ShowExifPane = settings.ShowExifPane;
        _ = LoadDrivesAsync();
    }

    partial void OnShowExifPaneChanged(bool value)
    {
        Settings.ShowExifPane = value;
        if (value) LoadCurrentExif();
        else ExifMetadata = null;
    }

    [RelayCommand]
    private void ToggleExifPane() => ShowExifPane = !ShowExifPane;

    private void LoadCurrentExif()
    {
        var item = SelectedItem;
        if (item is null || item.IsFolder)
        {
            ExifMetadata = null;
            ExifFileName = null;
            ExifFolder = null;
            return;
        }
        var path = item.Path;
        ExifFileName = Path.GetFileName(path);
        ExifFolder = Path.GetDirectoryName(path);
        try { ExifMetadata = ExifReader.Read(path); }
        catch { ExifMetadata = null; }
    }

    partial void OnThumbnailWidthChanged(int value)
    {
        Settings.ThumbnailSize = value;
        SettingsStore.Save(Settings);
        var tier = RoundToCacheTier(value);
        if (tier != _activeCacheTier)
        {
            _activeCacheTier = tier;
            _ = ReloadThumbnailsAsync();
        }
    }

    private async Task ReloadThumbnailsAsync()
    {
        _loadCts?.Cancel();
        _loadCts = new CancellationTokenSource();
        await LoadThumbnailsAsync(_loadCts.Token, force: true);
    }

    private async Task LoadDrivesAsync()
    {
        try
        {
            var drives = await Task.Run(() =>
            {
                var result = new List<(string Path, string Label)>();
                foreach (var drive in DriveInfo.GetDrives())
                {
                    if (!drive.IsReady) continue;
                    string label;
                    try
                    {
                        label = string.IsNullOrEmpty(drive.VolumeLabel)
                            ? drive.Name.TrimEnd('\\')
                            : $"{drive.Name.TrimEnd('\\')} ({drive.VolumeLabel})";
                    }
                    catch
                    {
                        label = drive.Name;
                    }
                    result.Add((drive.RootDirectory.FullName, label));
                }
                return result;
            }).ConfigureAwait(false);

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
            _ = LoadFolderAsync(item.Path);
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
            string.Equals(NormalizeRoot(d.Path), NormalizeRoot(chain[0]), StringComparison.OrdinalIgnoreCase));
        if (drive is null) return;

        FolderTreeItem? current = drive;
        for (int i = 1; i < chain.Count && current is not null; i++)
        {
            if (!current.IsExpanded) current.IsExpanded = true;
            await current.EnsureChildrenLoadedAsync();
            if (syncVersion != _treeSyncVersion) return;
            current = current.Children.FirstOrDefault(c =>
                string.Equals(c.Path, chain[i], StringComparison.OrdinalIgnoreCase));
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

    public async Task LoadFolderAsync(string folder)
    {
        if (string.Equals(CurrentFolder, folder, StringComparison.OrdinalIgnoreCase) && Items.Count > 0)
        {
            _ = SyncTreeSelectionAsync(folder);
            return;
        }

        _loadCts?.Cancel();
        _loadCts = new CancellationTokenSource();
        var ct = _loadCts.Token;

        CurrentFolder = folder;
        Items.Clear();
        FilteredItems.Clear();
        SelectedIndex = -1;
        FilterText = "";
        IsLoading = true;

        try
        {
            var contents = await FolderScanner.ScanBrowserAsync(folder, ct);
            var entries = contents.Folders
                .Select(entry => ThumbnailItem.CreateFolder(entry.Path, entry.PreviewMedia))
                .Concat(contents.Media.Select(entry => entry.IsVideo
                    ? ThumbnailItem.CreateVideo(entry.Path)
                    : new ThumbnailItem(entry.Path)));

            foreach (var entry in ApplySortToItems(entries))
            {
                ct.ThrowIfCancellationRequested();
                Items.Add(entry);
            }
            ApplyFilter();
            UpdateItemsSummary();
            _ = LoadThumbnailsAsync(ct);
        }
        catch (OperationCanceledException) { /* expected */ }
        catch { /* ignore folder read errors */ }
        finally
        {
            IsLoading = false;
            _ = SyncTreeSelectionAsync(folder);
        }
    }

    private async Task LoadThumbnailsAsync(CancellationToken ct, bool force = false)
    {
        var snapshot = Items.ToList();
        var tier = _activeCacheTier;
        var folderPreviewTier = RoundToCacheTier(Math.Max(64, ThumbnailWidth / 2));
        foreach (var item in snapshot)
        {
            if (ct.IsCancellationRequested) break;

            try
            {
                if (item.IsFolder)
                {
                    var previewCount = Math.Min(4, item.FolderPreviewMedia.Count);
                    for (var index = 0; index < previewCount; index++)
                    {
                        if (!force && item.GetFolderThumbnail(index) is not null) continue;

                        var media = item.FolderPreviewMedia[index];
                        var preview = await _cache.GetOrCreateAsync(
                            media.Path, folderPreviewTier, media.IsVideo, ct);
                        if (preview is not null)
                        {
                            var previewIndex = index;
                            await Dispatcher.UIThread.InvokeAsync(
                                () => item.SetFolderThumbnail(previewIndex, preview));
                        }
                    }
                }
                else
                {
                    if (!force && item.Thumbnail is not null) continue;

                    var thumbnail = await _cache.GetOrCreateAsync(
                        item.Path, tier, item.IsVideo, ct);
                    if (thumbnail is not null)
                    {
                        await Dispatcher.UIThread.InvokeAsync(() => item.Thumbnail = thumbnail);
                    }
                }
            }
            catch (OperationCanceledException) { break; }
            catch { /* skip this thumbnail */ }
        }
    }

    private List<ThumbnailItem> ApplySortToItems(IEnumerable<ThumbnailItem> items)
    {
        IEnumerable<ThumbnailItem> SortGroup(IEnumerable<ThumbnailItem> group)
        {
            IEnumerable<ThumbnailItem> sorted = SortMode switch
            {
                SortMode.Date => group.OrderBy(item => GetMtime(item.Path)),
                SortMode.Size => group.OrderBy(item => GetSize(item.Path)),
                _ => group.OrderBy(item => item.FileName, StringComparer.OrdinalIgnoreCase)
            };
            return SortDescending ? sorted.Reverse() : sorted;
        }

        // Keep navigation folders grouped ahead of media files in every sort mode.
        return SortGroup(items.Where(item => item.IsFolder))
            .Concat(SortGroup(items.Where(item => !item.IsFolder)))
            .ToList();
    }

    private static DateTime GetMtime(string p)
    {
        try { return File.GetLastWriteTime(p); }
        catch { return DateTime.MinValue; }
    }

    private static long GetSize(string p)
    {
        try { return new FileInfo(p).Length; }
        catch { return 0; }
    }

    [RelayCommand]
    private void SortByName() => SetSort(SortMode.Name);

    [RelayCommand]
    private void SortByDate() => SetSort(SortMode.Date);

    [RelayCommand]
    private void SortBySize() => SetSort(SortMode.Size);

    private void SetSort(SortMode mode)
    {
        if (SortMode == mode) SortDescending = !SortDescending;
        else { SortMode = mode; SortDescending = false; }
        Settings.SortMode = mode.ToString();
        Settings.SortDescending = SortDescending;
        ResortItems();
    }

    private void ResortItems()
    {
        var current = SelectedPath;
        var sorted = ApplySortToItems(Items);
        Items.Clear();
        foreach (var item in sorted)
            Items.Add(item);
        ApplyFilter();
        if (current is not null)
        {
            var idx = FilteredItems.ToList().FindIndex(i => i.Path == current);
            if (idx >= 0) SelectedIndex = idx;
        }
    }

    partial void OnFilterTextChanged(string value)
    {
        ApplyFilter();
    }

    private void ApplyFilter()
    {
        var current = SelectedPath;
        FilteredItems.Clear();
        var f = FilterText?.Trim() ?? "";
        foreach (var item in Items)
        {
            if (f.Length == 0 ||
                Path.GetFileName(item.Path).Contains(f, StringComparison.OrdinalIgnoreCase))
            {
                FilteredItems.Add(item);
            }
        }
        UpdateItemsSummary();
        if (current is not null)
        {
            var idx = FilteredItems.ToList().FindIndex(i => i.Path == current);
            SelectedIndex = idx >= 0 ? idx : (FilteredItems.Count > 0 ? 0 : -1);
        }
        else if (FilteredItems.Count > 0 && SelectedIndex < 0)
        {
            SelectedIndex = 0;
        }
    }

    private void UpdateItemsSummary()
    {
        var folderCount = Items.Count(item => item.IsFolder);
        var videoCount = Items.Count(item => item.IsVideo);
        var imageCount = Items.Count(item => item.IsImage);
        ItemsSummary = FilteredItems.Count == Items.Count
            ? FormatItemCounts(folderCount, imageCount, videoCount)
            : $"{FilteredItems.Count} of {Items.Count} items";
        ShowEmptyState = FilteredItems.Count == 0 && !IsLoading;
    }

    private static string FormatItemCounts(int folderCount, int imageCount, int videoCount)
    {
        var parts = new List<string>();
        if (folderCount > 0)
            parts.Add($"{folderCount} folder{(folderCount == 1 ? "" : "s")}");
        if (imageCount > 0 || parts.Count == 0)
            parts.Add($"{imageCount} image{(imageCount == 1 ? "" : "s")}");
        if (videoCount > 0)
            parts.Add($"{videoCount} video{(videoCount == 1 ? "" : "s")}");
        return string.Join(", ", parts);
    }

    partial void OnIsLoadingChanged(bool value)
    {
        ShowEmptyState = FilteredItems.Count == 0 && !value;
    }

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

    public void ResizeThumbnailsBy(int delta)
    {
        ThumbnailWidth = Math.Clamp(ThumbnailWidth + delta, MinThumbnailSize, MaxThumbnailSize);
    }

    [RelayCommand]
    private void DeleteSelected()
    {
        var selected = SelectedItem;
        if (selected is null || selected.IsFolder) return;
        var path = selected.Path;
        if (!FileOperations.DeleteToRecycleBin(path)) return;

        var item = FilteredItems[SelectedIndex];
        int oldIndex = SelectedIndex;
        Items.Remove(item);
        FilteredItems.RemoveAt(oldIndex);
        SelectedIndex = FilteredItems.Count == 0
            ? -1
            : Math.Min(oldIndex, FilteredItems.Count - 1);
        UpdateItemsSummary();
    }
}
