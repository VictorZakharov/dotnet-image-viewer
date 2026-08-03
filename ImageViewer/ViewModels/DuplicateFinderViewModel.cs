using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using ImageViewer.Models;

namespace ImageViewer.ViewModels;

public partial class DuplicateFinderViewModel : ObservableObject, IDisposable
{
    public ObservableCollection<DuplicateGroupViewModel> Groups { get; } = [];
    public ObservableCollection<string> ScanDetails { get; } = [];

    [ObservableProperty] private string _rootsSummary = "Choose one or more folders";
    [ObservableProperty] private bool _isScanning;
    [ObservableProperty] private bool _isPaused;
    [ObservableProperty] private bool _isProgressIndeterminate;
    [ObservableProperty] private double _progressValue;
    [ObservableProperty] private string _progressText = "Ready to scan";
    [ObservableProperty] private string _statusText =
        "No files are selected automatically. Review every choice before using the Recycle Bin.";

    public bool HasGroups => Groups.Count > 0;
    public bool HasScanDetails => ScanDetails.Count > 0;
    public int SelectedCount => Groups.Sum(group => group.SelectedCount);
    public long SelectedBytes => Groups.Sum(group => group.SelectedBytes);
    public string SelectionSummary => SelectedCount == 0
        ? "Nothing selected"
        : $"{SelectedCount} selected · {DuplicateDisplay.FormatBytes(SelectedBytes)}";
    public bool CanDelete => !IsScanning && SelectedCount > 0;
    public bool SelectionLeavesOnePerGroup =>
        Groups.All(group => group.SelectedCount == 0 || group.LeavesFileAfterDeletion);
    public IReadOnlyList<string> SelectedPaths => Groups
        .SelectMany(group => group.Files)
        .Where(file => file.IsSelected)
        .Select(file => file.Path)
        .ToList();

    public void ReportProgress(DuplicateScanProgress progress)
    {
        IsProgressIndeterminate = progress.IsIndeterminate;
        ProgressValue = progress.Percentage;
        var action = progress.Stage switch
        {
            DuplicateScanStage.Enumerating => "Finding images",
            DuplicateScanStage.Hashing => "Hashing images",
            DuplicateScanStage.Comparing => "Comparing images",
            DuplicateScanStage.ReadingMetadata => "Reading metadata",
            _ => "Finishing"
        };
        var count = progress.Total > 0 ? $" · {progress.Completed} of {progress.Total}" : "";
        var name = string.IsNullOrEmpty(progress.CurrentPath)
            ? ""
            : $" · {System.IO.Path.GetFileName(progress.CurrentPath)}";
        ProgressText = action + count + name;
    }

    public void ApplyResult(DuplicateScanResult result, DuplicateSortMode sortMode)
    {
        ClearGroups();
        ScanDetails.Clear();
        foreach (var link in result.HardLinks)
            ScanDetails.Add($"Hard link ignored: {link.AliasPath}\nSame file as {link.CanonicalPath}");
        foreach (var error in result.Errors)
            ScanDetails.Add($"Could not read: {error.Path}\n{error.Error}");
        foreach (var group in result.Groups)
            Groups.Add(new DuplicateGroupViewModel(group, NotifySelectionChanged));
        Sort(sortMode);

        StatusText = result.IsCanceled
            ? $"Scan canceled. Cached hashes were saved for the next scan."
            : $"Scanned {result.ScannedFileCount} images · found {Groups.Count} duplicate groups.";
        NotifyResultChanged();
    }

    public void Sort(DuplicateSortMode sortMode)
    {
        IEnumerable<DuplicateGroupViewModel> ordered = sortMode switch
        {
            DuplicateSortMode.GroupSize => Groups
                .OrderByDescending(group => group.Files.Count)
                .ThenByDescending(group => group.ReclaimableBytes),
            DuplicateSortMode.Date => Groups
                .OrderByDescending(group => group.NewestDateUtc),
            _ => Groups
                .OrderByDescending(group => group.ReclaimableBytes)
                .ThenByDescending(group => group.Files.Count)
        };
        var snapshot = ordered.ToList();
        Groups.Clear();
        foreach (var group in snapshot) Groups.Add(group);
    }

    public void RemoveDeletedPaths(IReadOnlyList<string> paths)
    {
        var removed = new HashSet<string>(paths, StringComparer.OrdinalIgnoreCase);
        foreach (var group in Groups.ToList())
        {
            if (!group.Files.Any(file => removed.Contains(file.Path))) continue;
            Groups.Remove(group);
            group.Dispose();
        }
        StatusText = $"Moved {paths.Count} file{(paths.Count == 1 ? "" : "s")} to the Recycle Bin. " +
                     "Affected groups were removed; rescan to review what remains.";
        NotifySelectionChanged();
        NotifyResultChanged();
    }

    public void ClearGroups()
    {
        foreach (var group in Groups) group.Dispose();
        Groups.Clear();
        NotifyResultChanged();
    }

    public void Dispose() => ClearGroups();

    partial void OnIsScanningChanged(bool value) => NotifySelectionChanged();

    private void NotifySelectionChanged()
    {
        OnPropertyChanged(nameof(SelectedCount));
        OnPropertyChanged(nameof(SelectedBytes));
        OnPropertyChanged(nameof(SelectionSummary));
        OnPropertyChanged(nameof(CanDelete));
        OnPropertyChanged(nameof(SelectionLeavesOnePerGroup));
    }

    private void NotifyResultChanged()
    {
        OnPropertyChanged(nameof(HasGroups));
        OnPropertyChanged(nameof(HasScanDetails));
        NotifySelectionChanged();
    }
}
