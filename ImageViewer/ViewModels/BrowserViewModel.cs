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
                return $"No images match \"{FilterText}\".";
            return "This folder contains no supported images.";
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
        LoadDrives();
    }

    private void LoadDrives()
    {
        DriveTree.Clear();
        try
        {
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
                DriveTree.Add(new FolderTreeItem(drive.RootDirectory.FullName, label));
            }
        }
        catch { /* drives inaccessible */ }
    }

    partial void OnSelectedTreeItemChanged(object? value)
    {
        if (value is FolderTreeItem item && !string.IsNullOrEmpty(item.Path))
            _ = LoadFolderAsync(item.Path);
    }

    public string? SelectedPath =>
        SelectedIndex >= 0 && SelectedIndex < FilteredItems.Count
            ? FilteredItems[SelectedIndex].Path
            : null;

    public async Task LoadFolderAsync(string folder)
    {
        if (string.Equals(CurrentFolder, folder, StringComparison.OrdinalIgnoreCase) && Items.Count > 0)
            return;

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
            var files = await FolderScanner.ScanAsync(folder, ct);
            var sorted = ApplySortToList(files);
            foreach (var f in sorted)
            {
                ct.ThrowIfCancellationRequested();
                Items.Add(new ThumbnailItem(f));
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
        }
    }

    private async Task LoadThumbnailsAsync(CancellationToken ct)
    {
        var snapshot = Items.ToList();
        foreach (var item in snapshot)
        {
            if (ct.IsCancellationRequested) break;
            if (item.Thumbnail is not null) continue;

            try
            {
                var thumb = await _cache.GetOrCreateAsync(item.Path, 256, ct);
                if (thumb is not null)
                {
                    await Dispatcher.UIThread.InvokeAsync(() => item.Thumbnail = thumb);
                }
            }
            catch (OperationCanceledException) { break; }
            catch { /* skip this thumbnail */ }
        }
    }

    private List<string> ApplySortToList(List<string> files)
    {
        IEnumerable<string> sorted = SortMode switch
        {
            SortMode.Date => files.OrderBy(GetMtime),
            SortMode.Size => files.OrderBy(GetSize),
            _ => files.OrderBy(f => Path.GetFileName(f), StringComparer.OrdinalIgnoreCase)
        };
        return SortDescending ? sorted.Reverse().ToList() : sorted.ToList();
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
        var sorted = ApplySortToList(Items.Select(i => i.Path).ToList());
        var byPath = Items.ToDictionary(i => i.Path, i => i);
        Items.Clear();
        foreach (var p in sorted)
            Items.Add(byPath.TryGetValue(p, out var existing) ? existing : new ThumbnailItem(p));
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
        ItemsSummary = FilteredItems.Count == Items.Count
            ? $"{Items.Count} image{(Items.Count == 1 ? "" : "s")}"
            : $"{FilteredItems.Count} of {Items.Count}";
        ShowEmptyState = FilteredItems.Count == 0 && !IsLoading;
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

    [RelayCommand]
    private void DeleteSelected()
    {
        var path = SelectedPath;
        if (path is null) return;
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
