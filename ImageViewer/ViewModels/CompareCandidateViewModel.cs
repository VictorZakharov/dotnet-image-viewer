using System;
using System.Collections.ObjectModel;
using System.IO;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using ImageViewer.Models;

namespace ImageViewer.ViewModels;

public partial class CompareCandidateViewModel : ObservableObject, IDisposable
{
    private readonly Action _stateChanged;
    private Bitmap? _bitmap;

    public string Path { get; }
    public string FileName => System.IO.Path.GetFileName(Path);
    public ObservableCollection<CompareMetadataRow> MetadataRows { get; } = [];
    public Bitmap? Bitmap
    {
        get => _bitmap;
        private set
        {
            if (ReferenceEquals(_bitmap, value)) return;
            var previous = _bitmap;
            SetProperty(ref _bitmap, value);
            previous?.Dispose();
        }
    }

    [ObservableProperty] private ImageMetadata _metadata = new();
    [ObservableProperty] private bool _isLoading = true;
    [ObservableProperty] private bool _isFullResolution;
    [ObservableProperty] private string? _loadError;
    [ObservableProperty] private bool _isActive;
    [ObservableProperty] private int _rotation;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(MarkText), nameof(HasMark))]
    private CompareMark _mark;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(RatingText), nameof(HasRating))]
    private int _rating;

    public string MarkText => Mark switch
    {
        CompareMark.Pick => "PICK",
        CompareMark.Reject => "REJECT",
        _ => ""
    };
    public bool HasMark => Mark != CompareMark.Neutral;
    public string RatingText => Rating > 0 ? new string('★', Rating) : "";
    public bool HasRating => Rating > 0;
    public string ResolutionText => IsFullResolution ? "Full resolution" : "Preview";

    public CompareCandidateViewModel(string path, Action stateChanged)
    {
        Path = path;
        _stateChanged = stateChanged;
    }

    public void ReplaceBitmap(Bitmap bitmap, bool isFullResolution)
    {
        Bitmap = bitmap;
        IsFullResolution = isFullResolution;
        OnPropertyChanged(nameof(ResolutionText));
    }

    public void SetMetadata(ImageMetadata metadata)
    {
        Metadata = metadata;
        BuildMetadataRows();
    }

    public void Dispose() => Bitmap = null;

    partial void OnMarkChanged(CompareMark value) => _stateChanged();
    partial void OnRatingChanged(int value) => _stateChanged();

    private void BuildMetadataRows()
    {
        MetadataRows.Clear();
        MetadataRows.Add(new CompareMetadataRow(
            "Dimensions", Metadata.DimensionsSummary ?? "Unavailable"));
        MetadataRows.Add(new CompareMetadataRow(
            "File size", Metadata.FileSizeSummary ?? "Unavailable"));
        MetadataRows.Add(new CompareMetadataRow(
            "Date taken", Metadata.TakenAtSummary ?? "Unavailable"));
        MetadataRows.Add(new CompareMetadataRow(
            "Camera", Metadata.CameraSummary ?? "Unavailable"));
        MetadataRows.Add(new CompareMetadataRow(
            "Lens", Metadata.Lens ?? "Unavailable"));
        MetadataRows.Add(new CompareMetadataRow(
            "Exposure", Metadata.ExposureSummary ?? "Unavailable"));
    }
}
