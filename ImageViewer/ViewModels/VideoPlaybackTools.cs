using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using ImageViewer.Models;

namespace ImageViewer.ViewModels;

internal sealed record VideoTrackDescriptor(
    int Id,
    string? Name,
    string? Language,
    string? Description);

internal interface IVideoTrackBackend
{
    IReadOnlyList<VideoTrackDescriptor> GetAudioTracks();
    IReadOnlyList<VideoTrackDescriptor> GetSubtitleTracks();
    int SelectedAudioTrackId { get; }
    int SelectedSubtitleTrackId { get; }
    bool SelectAudioTrack(int id);
    bool SelectSubtitleTrack(int id);
    bool LoadExternalSubtitle(string path);
    bool SetPlaybackRate(float rate);
}

internal enum VideoTrackKind
{
    Audio,
    Subtitle
}

internal static class VideoTrackCatalog
{
    public static IReadOnlyList<VideoTrackOption> Build(
        VideoTrackKind kind,
        IReadOnlyList<VideoTrackDescriptor> descriptors,
        int selectedId)
    {
        var options = new List<VideoTrackOption>(descriptors.Count);
        var seenIds = new HashSet<int>();
        var ordinal = 0;
        foreach (var descriptor in descriptors)
        {
            if (descriptor.Id < 0 || !seenIds.Add(descriptor.Id)) continue;
            ordinal++;
            options.Add(new VideoTrackOption(
                descriptor.Id,
                BuildLabel(kind, descriptor, ordinal),
                descriptor.Id == selectedId));
        }
        return options;
    }

    private static string BuildLabel(
        VideoTrackKind kind,
        VideoTrackDescriptor descriptor,
        int ordinal)
    {
        var parts = new List<string>(3);
        AddDistinct(parts, FormatLanguage(descriptor.Language));
        AddDistinct(parts, descriptor.Name);
        AddDistinct(parts, descriptor.Description);
        return parts.Count > 0
            ? string.Join(" · ", parts)
            : $"{(kind == VideoTrackKind.Audio ? "Audio" : "Subtitle")} track {ordinal}";
    }

    private static string? FormatLanguage(string? language)
    {
        var value = language?.Trim();
        if (string.IsNullOrEmpty(value)
            || string.Equals(value, "und", StringComparison.OrdinalIgnoreCase))
            return null;
        return value.Length <= 3 ? value.ToUpperInvariant() : value;
    }

    private static void AddDistinct(List<string> parts, string? value)
    {
        var candidate = value?.Trim();
        if (string.IsNullOrEmpty(candidate)) return;
        if (parts.Any(existing =>
                string.Equals(existing, candidate, StringComparison.OrdinalIgnoreCase)))
            return;
        parts.Add(candidate);
    }
}

public partial class VideoPlaybackTools : ObservableObject
{
    public static IReadOnlyList<float> PlaybackRatePresets { get; } =
        [0.5f, 0.75f, 1f, 1.25f, 1.5f, 1.75f, 2f];

    private IVideoTrackBackend? _backend;

    [ObservableProperty]
    [NotifyPropertyChangedFor(
        nameof(HasAudioTracks),
        nameof(CanManageSubtitles),
        nameof(CanChangePlaybackSpeed))]
    private bool _isReady;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasAudioTracks))]
    private IReadOnlyList<VideoTrackOption> _audioTracks = Array.Empty<VideoTrackOption>();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSubtitleTracks))]
    private IReadOnlyList<VideoTrackOption> _subtitleTracks = Array.Empty<VideoTrackOption>();

    [ObservableProperty]
    private int _selectedAudioTrackId = -1;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsSubtitleOff))]
    private int _selectedSubtitleTrackId = -1;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PlaybackRateLabel))]
    private float _playbackRate = 1f;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsSubtitleOff))]
    private string? _pendingExternalSubtitleLabel;

    public bool HasAudioTracks => IsReady && AudioTracks.Count > 0;
    public bool HasSubtitleTracks => SubtitleTracks.Count > 0;
    public bool CanManageSubtitles => IsReady;
    public bool CanChangePlaybackSpeed => IsReady;
    public bool IsSubtitleOff =>
        SelectedSubtitleTrackId < 0 && string.IsNullOrEmpty(PendingExternalSubtitleLabel);
    public string PlaybackRateLabel => FormatPlaybackRate(PlaybackRate);

    internal void Attach(IVideoTrackBackend backend)
    {
        _backend = backend;
        IsReady = false;
        PendingExternalSubtitleLabel = null;
        SelectedAudioTrackId = -1;
        SelectedSubtitleTrackId = -1;
        AudioTracks = Array.Empty<VideoTrackOption>();
        SubtitleTracks = Array.Empty<VideoTrackOption>();
    }

    internal void Activate()
    {
        if (_backend is null) return;
        IsReady = true;
        _backend.SetPlaybackRate(PlaybackRate);
        Refresh();
    }

    internal void Detach()
    {
        _backend = null;
        IsReady = false;
        PendingExternalSubtitleLabel = null;
        SelectedAudioTrackId = -1;
        SelectedSubtitleTrackId = -1;
        AudioTracks = Array.Empty<VideoTrackOption>();
        SubtitleTracks = Array.Empty<VideoTrackOption>();
    }

    internal void Refresh()
    {
        if (!IsReady || _backend is null) return;

        SelectedAudioTrackId = _backend.SelectedAudioTrackId;
        SelectedSubtitleTrackId = _backend.SelectedSubtitleTrackId;
        AudioTracks = VideoTrackCatalog.Build(
            VideoTrackKind.Audio,
            _backend.GetAudioTracks(),
            SelectedAudioTrackId);
        SubtitleTracks = VideoTrackCatalog.Build(
            VideoTrackKind.Subtitle,
            _backend.GetSubtitleTracks(),
            SelectedSubtitleTrackId);

        if (SelectedSubtitleTrackId >= 0
            && SubtitleTracks.Any(option => option.Id == SelectedSubtitleTrackId))
            PendingExternalSubtitleLabel = null;
    }

    public bool SelectAudioTrack(int id)
    {
        if (!IsReady || _backend?.SelectAudioTrack(id) != true) return false;
        Refresh();
        return true;
    }

    public bool SelectSubtitleTrack(int id)
    {
        if (!IsReady || _backend?.SelectSubtitleTrack(id) != true) return false;
        if (id < 0) PendingExternalSubtitleLabel = null;
        Refresh();
        return true;
    }

    public bool LoadExternalSubtitle(string path)
    {
        if (!IsReady || _backend?.LoadExternalSubtitle(path) != true) return false;
        PendingExternalSubtitleLabel = Path.GetFileName(path);
        Refresh();
        return true;
    }

    public bool SelectPlaybackRate(float rate)
    {
        if (!PlaybackRatePresets.Any(preset => Math.Abs(preset - rate) < 0.001f))
            return false;
        if (!IsReady || _backend?.SetPlaybackRate(rate) != true) return false;
        PlaybackRate = rate;
        return true;
    }

    public static string FormatPlaybackRate(float rate) =>
        rate.ToString("0.##", CultureInfo.InvariantCulture) + "x";
}
