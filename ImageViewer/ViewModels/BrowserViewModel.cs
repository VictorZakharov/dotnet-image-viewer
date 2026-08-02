using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ImageViewer.Collections;
using ImageViewer.Models;
using ImageViewer.Services;

namespace ImageViewer.ViewModels;

public partial class BrowserViewModel : ObservableObject, IDisposable
{
    public AppSettings Settings { get; }
    public RangeObservableCollection<ThumbnailItem> Items { get; } = new();
    public RangeObservableCollection<ThumbnailItem> FilteredItems { get; } = new();
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

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsGridLoading))]
    private bool _isLoading;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsGridLoading))]
    private bool _isThumbnailLoading;

    public bool IsGridLoading => IsLoading || IsThumbnailLoading;

    [ObservableProperty] private string _itemsSummary = "";
    [ObservableProperty] private bool _showEmptyState = true;

    [ObservableProperty]
    [NotifyPropertyChangedFor(
        nameof(ThumbnailHeight),
        nameof(ThumbnailCellWidth),
        nameof(ThumbnailCellHeight))]
    private int _thumbnailWidth = 200;

    public int ThumbnailHeight => (int)(ThumbnailWidth * 0.78) + 18;
    public int ThumbnailCellWidth => ThumbnailWidth + 4;
    public int ThumbnailCellHeight => ThumbnailHeight + 4;

    public const int MinThumbnailSize = 96;
    public const int MaxThumbnailSize = 512;

    private static readonly int[] CacheTiers = { 128, 192, 256, 384, 512 };

    private static int RoundToCacheTier(int dim)
    {
        foreach (var t in CacheTiers) if (dim <= t) return t;
        return CacheTiers[^1];
    }

    private int _activeCacheTier;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(GridDiagnostics))]
    private int _realizedItemCount;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(GridDiagnostics))]
    private int _queuedThumbnailCount;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(GridDiagnostics))]
    private int _activeThumbnailCount;

    public bool ShowGridDiagnostics { get; }
    private string _gridLayoutDiagnostics = "";
    public string GridDiagnostics =>
        $"{RealizedItemCount} realized / {FilteredItems.Count} · {QueuedThumbnailCount} queued · {ActiveThumbnailCount} active"
        + _gridLayoutDiagnostics;

    public void ReportGridLayoutMetrics(
        int columns,
        double viewportWidth,
        double scrollBoundsWidth,
        double repeaterBoundsWidth)
    {
        if (!ShowGridDiagnostics) return;
        _gridLayoutDiagnostics =
            $" · {columns} cols · viewport {viewportWidth:F0} · scroll {scrollBoundsWidth:F0} · repeater {repeaterBoundsWidth:F0}";
        OnPropertyChanged(nameof(GridDiagnostics));
    }

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
    public event Action? ThumbnailRequestsInvalidated;

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
        _smoothScrollingEnabled = settings.SmoothScrollingEnabled;
        ShowExifPane = settings.ShowExifPane;
        ShowGridDiagnostics = string.Equals(
            Environment.GetEnvironmentVariable("IMAGEVIEWER_GRID_DIAGNOSTICS"),
            "1",
            StringComparison.Ordinal);
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
            ResetThumbnailRequests();
        }
        ThumbnailRequestsInvalidated?.Invoke();
    }

    private List<ThumbnailItem> ApplySortToItems(IEnumerable<ThumbnailItem> items) =>
        ApplySortToItems(items, SortMode, SortDescending);

    private static List<ThumbnailItem> ApplySortToItems(
        IEnumerable<ThumbnailItem> items,
        SortMode sortMode,
        bool sortDescending)
    {
        IEnumerable<ThumbnailItem> SortGroup(IEnumerable<ThumbnailItem> group)
        {
            IEnumerable<ThumbnailItem> sorted = sortMode switch
            {
                SortMode.Date => group.OrderBy(item => item.ModifiedAt ?? DateTime.MinValue),
                SortMode.Size => group.OrderBy(item => item.FileSize),
                _ => group.OrderBy(item => item.FileName, StringComparer.OrdinalIgnoreCase)
            };
            return sortDescending ? sorted.Reverse() : sorted;
        }

        // Keep navigation folders grouped ahead of media files in every sort mode.
        return SortGroup(items.Where(item => item.IsFolder))
            .Concat(SortGroup(items.Where(item => !item.IsFolder)))
            .ToList();
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
        Items.ReplaceAll(sorted);
        ApplyFilter();
        if (current is not null)
        {
            var idx = FilteredItems.ToList().FindIndex(i => i.Path == current);
            if (idx >= 0) SelectIndex(idx);
        }
    }

    partial void OnFilterTextChanged(string value)
    {
        ApplyFilter();
    }

    private void ApplyFilter()
    {
        var current = SelectedPath;
        ResetThumbnailRequests();
        var f = FilterText?.Trim() ?? "";
        var matches = Items.Where(item =>
            f.Length == 0
            || Path.GetFileName(item.Path).Contains(f, StringComparison.OrdinalIgnoreCase));
        FilteredItems.ReplaceAll(matches);
        UpdateItemsSummary();
        var newIndex = -1;
        if (current is not null)
        {
            var idx = FilteredItems.ToList().FindIndex(i => i.Path == current);
            newIndex = idx >= 0 ? idx : (FilteredItems.Count > 0 ? 0 : -1);
        }
        else if (FilteredItems.Count > 0)
        {
            newIndex = Math.Clamp(SelectedIndex, 0, FilteredItems.Count - 1);
        }
        SelectIndex(newIndex);

        ThumbnailRequestsInvalidated?.Invoke();
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
        OnPropertyChanged(nameof(GridDiagnostics));
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
        SelectIndex(-1);
        Items.Remove(item);
        FilteredItems.RemoveAt(oldIndex);
        item.Dispose();
        ResetThumbnailRequests();
        SelectIndex(FilteredItems.Count == 0
            ? -1
            : Math.Min(oldIndex, FilteredItems.Count - 1));
        UpdateItemsSummary();
        ThumbnailRequestsInvalidated?.Invoke();
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _folderLoadVersion++;
        _folderLoadCts?.Cancel();
        _folderLoadCts?.Dispose();
        _folderLoadCts = null;

        _thumbnailGeneration++;
        _thumbnailSessionCts.Cancel();
        _thumbnailSessionCts.Dispose();
        foreach (var active in _activeThumbnailRequests.Values)
            active.Cancellation.Cancel();
        _activeThumbnailRequests.Clear();
        _thumbnailQueue.Clear();
        _queuedThumbnailPriorities.Clear();

        SelectIndex(-1);
        DisposeItems(Items);
        Items.Clear();
        FilteredItems.Clear();
        UpdateSchedulerStatus();
    }
}
