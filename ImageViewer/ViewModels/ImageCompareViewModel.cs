using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using ImageViewer.Models;
using ImageViewer.Services;

namespace ImageViewer.ViewModels;

public partial class ImageCompareViewModel : ObservableObject, IDisposable
{
    private readonly List<string> _deletedPaths = [];

    public ObservableCollection<CompareCandidateViewModel> Candidates { get; } = [];

    [ObservableProperty] private CompareCandidateViewModel? _activeCandidate;
    [ObservableProperty] private bool _isSynchronized = true;
    [ObservableProperty] private bool _isBlinking;

    public int Rows => Candidates.Count <= 2 ? 1 : 2;
    public int Columns => Candidates.Count <= 1 ? 1 : 2;
    public bool CanBlink => Candidates.Count == 2;
    public bool HasRejected => Candidates.Any(candidate => candidate.Mark == CompareMark.Reject);
    public string CandidateSummary => $"{Candidates.Count} image{(Candidates.Count == 1 ? "" : "s")}";

    public ImageCompareViewModel(IReadOnlyList<string> paths)
    {
        foreach (var path in paths.Take(4))
            Candidates.Add(new CompareCandidateViewModel(path, NotifyCandidateStateChanged));
        SetActive(Candidates.FirstOrDefault());
    }

    public void SetActive(CompareCandidateViewModel? candidate)
    {
        if (candidate is null || !Candidates.Contains(candidate)) return;
        foreach (var item in Candidates) item.IsActive = ReferenceEquals(item, candidate);
        ActiveCandidate = candidate;
    }

    public void MoveActive(int delta)
    {
        if (Candidates.Count == 0) return;
        var index = ActiveCandidate is null ? 0 : Candidates.IndexOf(ActiveCandidate);
        SetActive(Candidates[(index + delta + Candidates.Count) % Candidates.Count]);
    }

    public void TogglePick()
    {
        if (ActiveCandidate is null) return;
        ActiveCandidate.Mark = ActiveCandidate.Mark == CompareMark.Pick
            ? CompareMark.Neutral
            : CompareMark.Pick;
    }

    public void ToggleReject()
    {
        if (ActiveCandidate is null) return;
        ActiveCandidate.Mark = ActiveCandidate.Mark == CompareMark.Reject
            ? CompareMark.Neutral
            : CompareMark.Reject;
    }

    public void KeepActiveRejectOthers()
    {
        if (ActiveCandidate is null) return;
        foreach (var candidate in Candidates)
            candidate.Mark = ReferenceEquals(candidate, ActiveCandidate)
                ? CompareMark.Pick
                : CompareMark.Reject;
    }

    public void RemoveDeleted(IReadOnlyList<string> paths)
    {
        var removed = new HashSet<string>(paths, FileSystemPath.Comparer);
        _deletedPaths.AddRange(paths.Where(path => !_deletedPaths.Contains(
            path, FileSystemPath.Comparer)));
        foreach (var candidate in Candidates.Where(candidate => removed.Contains(candidate.Path)).ToList())
        {
            Candidates.Remove(candidate);
            candidate.Dispose();
        }
        SetActive(Candidates.FirstOrDefault());
        RefreshDifferences();
        NotifyCollectionChanged();
    }

    public void RefreshDifferences()
    {
        for (var rowIndex = 0; rowIndex < 6; rowIndex++)
        {
            var rows = Candidates
                .Where(candidate => candidate.MetadataRows.Count > rowIndex)
                .Select(candidate => candidate.MetadataRows[rowIndex])
                .ToList();
            var different = rows.Select(row => row.Value)
                .Distinct(StringComparer.OrdinalIgnoreCase).Skip(1).Any();
            foreach (var row in rows) row.IsDifferent = different;
        }
    }

    public ImageCompareResult CreateResult() => new(
        Candidates.Select(candidate => new CompareCandidateDecision(
            candidate.Path, candidate.Mark)).ToList(),
        _deletedPaths.ToList());

    public void Dispose()
    {
        foreach (var candidate in Candidates) candidate.Dispose();
        Candidates.Clear();
    }

    private void NotifyCandidateStateChanged()
    {
        OnPropertyChanged(nameof(HasRejected));
    }

    private void NotifyCollectionChanged()
    {
        OnPropertyChanged(nameof(Rows));
        OnPropertyChanged(nameof(Columns));
        OnPropertyChanged(nameof(CanBlink));
        OnPropertyChanged(nameof(HasRejected));
        OnPropertyChanged(nameof(CandidateSummary));
    }
}
