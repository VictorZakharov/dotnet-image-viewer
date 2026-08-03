using System;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using ImageViewer.Models;

namespace ImageViewer.ViewModels;

public sealed class DuplicateGroupViewModel : ObservableObject, IDisposable
{
    private readonly Action _selectionChanged;
    private string _keeperReason;

    public DuplicateGroup Model { get; }
    public ObservableCollection<DuplicateFileViewModel> Files { get; }
    public string KindText => Model.Kind == DuplicateGroupKind.Exact
        ? "EXACT · BYTE-FOR-BYTE"
        : "VISUALLY SIMILAR";
    public bool IsSimilar => Model.Kind == DuplicateGroupKind.Similar;
    public string SimilarityText => IsSimilar
        ? $"dHash distance up to {Model.MaximumDistance}; scan threshold {Model.SimilarityThreshold}"
        : "SHA-256 plus byte comparison";
    public string GroupSummary =>
        $"{Files.Count} files · {DuplicateDisplay.FormatBytes(ReclaimableBytes)} reclaimable";
    public string KeeperRule => $"Suggested keeper: {_keeperReason}";
    public long ReclaimableBytes => Files
        .Where(file => !file.IsSuggestedKeeper)
        .Sum(file => file.Entry.SizeBytes);
    public DateTime NewestDateUtc => Model.NewestDateUtc;
    public int SelectedCount => Files.Count(file => file.IsSelected);
    public long SelectedBytes => Files.Where(file => file.IsSelected).Sum(file => file.Entry.SizeBytes);
    public bool LeavesFileAfterDeletion => Files.Any(file => !file.IsSelected);

    public DuplicateGroupViewModel(DuplicateGroup model, Action selectionChanged)
    {
        Model = model;
        _selectionChanged = selectionChanged;
        _keeperReason = model.KeeperReason;
        var keeper = model.Files.First(file => string.Equals(
            file.Path, model.SuggestedKeeperPath, StringComparison.OrdinalIgnoreCase));
        Files = new ObservableCollection<DuplicateFileViewModel>(model.Files.Select(file =>
            new DuplicateFileViewModel(file, keeper, model.Kind, OnFileSelectionChanged)));
    }

    public void SelectSuggestedDuplicates()
    {
        foreach (var file in Files) file.IsSelected = !file.IsSuggestedKeeper;
    }

    public void ClearSelection()
    {
        foreach (var file in Files) file.IsSelected = false;
    }

    public IReadOnlyList<string> GetComparisonPaths()
    {
        var selected = Files.Where(file => file.IsSelected).ToList();
        if (selected.Count is >= 1 and <= 4)
        {
            var keeper = Files.FirstOrDefault(file => file.IsSuggestedKeeper);
            if (keeper is not null && !selected.Contains(keeper))
                selected.Insert(0, keeper);
            if (selected.Count == 1)
            {
                var alternative = Files.FirstOrDefault(file => !selected.Contains(file));
                if (alternative is not null) selected.Add(alternative);
            }
            return selected.Take(4).Select(file => file.Path).ToList();
        }

        return Files
            .OrderByDescending(file => file.IsSuggestedKeeper)
            .Take(4)
            .Select(file => file.Path)
            .ToList();
    }

    public void ApplyCompareResult(ImageCompareResult result)
    {
        var decisions = result.Decisions.ToDictionary(
            decision => decision.Path,
            StringComparer.OrdinalIgnoreCase);
        foreach (var file in Files)
            if (decisions.TryGetValue(file.Path, out var decision))
                file.ApplyDecision(decision);
        if (!string.IsNullOrEmpty(result.PickedPath))
        {
            foreach (var file in Files)
                file.IsSuggestedKeeper = string.Equals(
                    file.Path, result.PickedPath, StringComparison.OrdinalIgnoreCase);
            _keeperReason = "chosen in side-by-side compare.";
        }
        OnPropertyChanged(nameof(KeeperRule));
        OnPropertyChanged(nameof(ReclaimableBytes));
        OnPropertyChanged(nameof(GroupSummary));
        OnFileSelectionChanged();
    }

    public void Dispose()
    {
        foreach (var file in Files) file.Dispose();
        Files.Clear();
    }

    private void OnFileSelectionChanged()
    {
        OnPropertyChanged(nameof(SelectedCount));
        OnPropertyChanged(nameof(SelectedBytes));
        OnPropertyChanged(nameof(LeavesFileAfterDeletion));
        _selectionChanged();
    }
}
