using System;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using ImageViewer.Models;

namespace ImageViewer.ViewModels;

public sealed class DuplicateGroupViewModel : ObservableObject, IDisposable
{
    private readonly Action _selectionChanged;

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
    public string KeeperRule => $"Suggested keeper: {Model.KeeperReason}";
    public long ReclaimableBytes => Model.ReclaimableBytes;
    public DateTime NewestDateUtc => Model.NewestDateUtc;
    public int SelectedCount => Files.Count(file => file.IsSelected);
    public long SelectedBytes => Files.Where(file => file.IsSelected).Sum(file => file.Entry.SizeBytes);
    public bool LeavesFileAfterDeletion => Files.Any(file => !file.IsSelected);

    public DuplicateGroupViewModel(DuplicateGroup model, Action selectionChanged)
    {
        Model = model;
        _selectionChanged = selectionChanged;
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
