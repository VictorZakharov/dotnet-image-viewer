using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ImageViewer.ViewModels;
using LibVLCSharp.Shared;
using LibVLCSharp.Shared.Structures;

namespace ImageViewer.Services;

internal sealed class LibVlcVideoTrackBackend(
    MediaPlayer player,
    Media media) : IVideoTrackBackend
{
    public IReadOnlyList<VideoTrackDescriptor> GetAudioTracks() =>
        GetTracks(TrackType.Audio);

    public IReadOnlyList<VideoTrackDescriptor> GetSubtitleTracks() =>
        GetTracks(TrackType.Text);

    public int SelectedAudioTrackId
    {
        get
        {
            try { return player.AudioTrack; }
            catch { return -1; }
        }
    }

    public int SelectedSubtitleTrackId
    {
        get
        {
            try { return player.Spu; }
            catch { return -1; }
        }
    }

    public bool SelectAudioTrack(int id)
    {
        try { return player.SetAudioTrack(id); }
        catch { return false; }
    }

    public bool SelectSubtitleTrack(int id)
    {
        try { return player.SetSpu(id); }
        catch { return false; }
    }

    public bool LoadExternalSubtitle(string path)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) return false;
            var uri = new Uri(Path.GetFullPath(path)).AbsoluteUri;
            return player.AddSlave(MediaSlaveType.Subtitle, uri, select: true);
        }
        catch
        {
            return false;
        }
    }

    public bool SetPlaybackRate(float rate)
    {
        try { return player.SetRate(rate) == 0; }
        catch { return false; }
    }

    private IReadOnlyList<VideoTrackDescriptor> GetTracks(TrackType type)
    {
        try
        {
            var parsedTracks = GetParsedTracks(type);
            var descriptions = GetTrackDescriptions(type);
            var result = new List<VideoTrackDescriptor>();
            var seenIds = new HashSet<int>();

            foreach (var description in descriptions)
            {
                if (description.Id < 0 || !seenIds.Add(description.Id)) continue;
                var hasParsedTrack = parsedTracks.TryGetValue(description.Id, out var parsed);
                result.Add(new VideoTrackDescriptor(
                    description.Id,
                    description.Name,
                    hasParsedTrack ? parsed.Language : null,
                    hasParsedTrack ? parsed.Description : null));
            }

            foreach (var parsed in parsedTracks.Values)
            {
                if (!seenIds.Add(parsed.Id)) continue;
                result.Add(new VideoTrackDescriptor(
                    parsed.Id,
                    null,
                    parsed.Language,
                    parsed.Description));
            }

            return result;
        }
        catch
        {
            return Array.Empty<VideoTrackDescriptor>();
        }
    }

    private IReadOnlyDictionary<int, MediaTrack> GetParsedTracks(TrackType type)
    {
        try
        {
            return media.Tracks
                .Where(track => track.TrackType == type && track.Id >= 0)
                .GroupBy(track => track.Id)
                .ToDictionary(group => group.Key, group => group.First());
        }
        catch
        {
            return new Dictionary<int, MediaTrack>();
        }
    }

    private TrackDescription[] GetTrackDescriptions(TrackType type)
    {
        try
        {
            return type == TrackType.Audio
                ? player.AudioTrackDescription
                : player.SpuDescription;
        }
        catch
        {
            return Array.Empty<TrackDescription>();
        }
    }
}
