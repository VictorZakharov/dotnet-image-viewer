using System;
using System.IO;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using ImageViewer.Models;
using ImageViewer.Services;

namespace ImageViewer.ViewModels;

public partial class DuplicateFileViewModel : ObservableObject, IDisposable
{
    private readonly Action _selectionChanged;

    public DuplicateFileEntry Entry { get; }
    public string FileName => System.IO.Path.GetFileName(Entry.Path);
    public string Path => Entry.Path;
    public string SizeText => DuplicateDisplay.FormatBytes(Entry.SizeBytes);
    public string DimensionsText => Entry.Width > 0 && Entry.Height > 0
        ? $"{Entry.Width} × {Entry.Height}"
        : "Dimensions unavailable";
    public string TakenText => Entry.TakenAt is { } taken
        ? $"Taken {taken:yyyy-MM-dd HH:mm:ss}"
        : "Taken date unavailable";
    public string CreatedText => $"Created {Entry.CreatedUtc.ToLocalTime():yyyy-MM-dd HH:mm:ss}";
    public string ModifiedText => $"Modified {Entry.ModifiedUtc.ToLocalTime():yyyy-MM-dd HH:mm:ss}";
    public string AccessedText => $"Accessed {Entry.AccessedUtc.ToLocalTime():yyyy-MM-dd HH:mm:ss}";
    public string MetadataText => BuildMetadataText();
    public string MatchText { get; }

    [ObservableProperty] private bool _isSelected;
    [ObservableProperty] private Bitmap? _thumbnail;
    [ObservableProperty] private bool _isSuggestedKeeper;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CompareBadgeText), nameof(HasCompareBadge))]
    private CompareMark _compareMark;

    public bool HasCompareBadge => CompareMark != ImageViewer.Models.CompareMark.Neutral;
    public string CompareBadgeText => CompareMark switch
    {
        ImageViewer.Models.CompareMark.Pick => "PICK",
        ImageViewer.Models.CompareMark.Reject => "REJECT",
        _ => ""
    };

    public DuplicateFileViewModel(
        DuplicateFileEntry entry,
        DuplicateFileEntry keeper,
        DuplicateGroupKind groupKind,
        Action selectionChanged)
    {
        Entry = entry;
        _selectionChanged = selectionChanged;
        IsSuggestedKeeper = FileSystemPath.Equals(entry.Path, keeper.Path);
        MatchText = groupKind == DuplicateGroupKind.Exact
            ? "Byte-identical"
            : entry.ContentHash == keeper.ContentHash
                ? "Byte-identical to keeper"
                : "Visual match";
    }

    public void ApplyDecision(CompareCandidateDecision decision)
    {
        CompareMark = decision.Mark;
        if (decision.Mark == ImageViewer.Models.CompareMark.Reject) IsSelected = true;
        else if (decision.Mark == ImageViewer.Models.CompareMark.Pick) IsSelected = false;
    }

    partial void OnIsSelectedChanged(bool value) => _selectionChanged();

    public void Dispose()
    {
        Thumbnail?.Dispose();
        Thumbnail = null;
    }

    private string BuildMetadataText()
    {
        var camera = Entry.Camera;
        if (!string.IsNullOrWhiteSpace(Entry.Lens))
            camera = string.IsNullOrWhiteSpace(camera)
                ? Entry.Lens
                : $"{camera} · {Entry.Lens}";
        if (!string.IsNullOrWhiteSpace(Entry.Exposure))
            camera = string.IsNullOrWhiteSpace(camera)
                ? Entry.Exposure
                : $"{camera} · {Entry.Exposure}";
        return string.IsNullOrWhiteSpace(camera) ? "No camera metadata" : camera;
    }
}
