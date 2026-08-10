using ImageViewer.Services;
using ImageViewer.ViewModels;

namespace ImageViewer.Tests;

public sealed class VideoPlaybackToolsTests
{
    [Fact]
    public void VideoToolRailOnlyShowsForNonFullscreenVideo()
    {
        using var viewer = new ViewerViewModel(new AppSettings());

        Assert.False(viewer.ShowVideoTools);

        viewer.IsVideo = true;
        Assert.True(viewer.ShowVideoTools);

        viewer.IsFullscreen = true;
        Assert.False(viewer.ShowVideoTools);

        viewer.IsFullscreen = false;
        viewer.IsVideo = false;
        Assert.False(viewer.ShowVideoTools);
    }

    [Fact]
    public void ActivationBuildsUsefulLabelsAndMarksCurrentTracks()
    {
        var backend = new FakeVideoTrackBackend
        {
            Audio =
            [
                new VideoTrackDescriptor(10, "Main", "eng", "Stereo"),
                new VideoTrackDescriptor(11, "Commentary", "eng", null)
            ],
            Subtitles =
            [
                new VideoTrackDescriptor(20, "Signs", "spa", "Forced")
            ],
            SelectedAudio = 11,
            SelectedSubtitle = -1
        };
        var tools = new VideoPlaybackTools();

        tools.Attach(backend);

        Assert.False(tools.IsReady);
        Assert.Empty(tools.AudioTracks);

        tools.Activate();

        Assert.True(tools.IsReady);
        Assert.Equal("ENG · Main · Stereo", tools.AudioTracks[0].Label);
        Assert.False(tools.AudioTracks[0].IsSelected);
        Assert.True(tools.AudioTracks[1].IsSelected);
        Assert.Equal("SPA · Signs · Forced", tools.SubtitleTracks[0].Label);
        Assert.True(tools.IsSubtitleOff);
        Assert.Equal(1f, backend.LastPlaybackRate);
    }

    [Fact]
    public void AttachingAnotherVideoClearsEveryPriorTrackBeforeRefresh()
    {
        var first = new FakeVideoTrackBackend
        {
            Audio = [new VideoTrackDescriptor(1, "First audio", null, null)],
            Subtitles = [new VideoTrackDescriptor(2, "First subtitles", null, null)],
            SelectedAudio = 1,
            SelectedSubtitle = 2
        };
        var second = new FakeVideoTrackBackend
        {
            Audio = [new VideoTrackDescriptor(9, "Second audio", null, null)],
            SelectedAudio = 9
        };
        var tools = new VideoPlaybackTools();
        tools.Attach(first);
        tools.Activate();

        tools.Attach(second);

        Assert.False(tools.IsReady);
        Assert.Empty(tools.AudioTracks);
        Assert.Empty(tools.SubtitleTracks);
        Assert.Equal(-1, tools.SelectedAudioTrackId);
        Assert.Equal(-1, tools.SelectedSubtitleTrackId);

        tools.Activate();

        Assert.Equal([9], tools.AudioTracks.Select(track => track.Id).ToArray());
        Assert.Empty(tools.SubtitleTracks);
    }

    [Fact]
    public void AudioAndSubtitleSelectionsApplyWithoutReplacingTheBackend()
    {
        var backend = new FakeVideoTrackBackend
        {
            Audio =
            [
                new VideoTrackDescriptor(1, "English", null, null),
                new VideoTrackDescriptor(2, "French", null, null)
            ],
            Subtitles =
            [
                new VideoTrackDescriptor(3, "English", null, null)
            ],
            SelectedAudio = 1,
            SelectedSubtitle = -1
        };
        var tools = new VideoPlaybackTools();
        tools.Attach(backend);
        tools.Activate();

        Assert.True(tools.SelectAudioTrack(2));
        Assert.True(tools.AudioTracks.Single(track => track.Id == 2).IsSelected);
        Assert.True(tools.SelectSubtitleTrack(3));
        Assert.False(tools.IsSubtitleOff);
        Assert.True(tools.SubtitleTracks.Single().IsSelected);
        Assert.True(tools.SelectSubtitleTrack(-1));
        Assert.True(tools.IsSubtitleOff);
    }

    [Fact]
    public void ExternalSubtitleAppearsSelectedAndCanBeTurnedOff()
    {
        var backend = new FakeVideoTrackBackend();
        var tools = new VideoPlaybackTools();
        tools.Attach(backend);
        tools.Activate();

        Assert.True(tools.LoadExternalSubtitle("captions.ass"));

        Assert.Equal("captions.ass", backend.ExternalSubtitlePath);
        Assert.Single(tools.SubtitleTracks);
        Assert.Equal("captions.ass · External", tools.SubtitleTracks[0].Label);
        Assert.True(tools.SubtitleTracks[0].IsSelected);
        Assert.Null(tools.PendingExternalSubtitleLabel);

        Assert.True(tools.SelectSubtitleTrack(-1));
        Assert.True(tools.IsSubtitleOff);
    }

    [Fact]
    public void PlaybackSpeedUsesPresetsAndCarriesAcrossVideos()
    {
        var first = new FakeVideoTrackBackend();
        var second = new FakeVideoTrackBackend();
        var tools = new VideoPlaybackTools();
        tools.Attach(first);
        tools.Activate();

        Assert.False(tools.SelectPlaybackRate(3f));
        Assert.True(tools.SelectPlaybackRate(1.5f));
        Assert.Equal("1.5x", tools.PlaybackRateLabel);
        Assert.Equal(1.5f, first.LastPlaybackRate);

        tools.Attach(second);
        tools.Activate();

        Assert.Equal(1.5f, second.LastPlaybackRate);
        Assert.Equal("1.5x", tools.PlaybackRateLabel);
    }

    [Fact]
    public void CatalogDropsDisabledAndDuplicateTrackIds()
    {
        var options = VideoTrackCatalog.Build(
            VideoTrackKind.Audio,
            [
                new VideoTrackDescriptor(-1, "Disable", null, null),
                new VideoTrackDescriptor(4, null, null, null),
                new VideoTrackDescriptor(4, "Duplicate", null, null)
            ],
            selectedId: 4);

        Assert.Single(options);
        Assert.Equal("Audio track 1", options[0].Label);
        Assert.True(options[0].IsSelected);
    }

    private sealed class FakeVideoTrackBackend : IVideoTrackBackend
    {
        public IReadOnlyList<VideoTrackDescriptor> Audio { get; set; } =
            Array.Empty<VideoTrackDescriptor>();
        public IReadOnlyList<VideoTrackDescriptor> Subtitles { get; set; } =
            Array.Empty<VideoTrackDescriptor>();
        public int SelectedAudio { get; set; } = -1;
        public int SelectedSubtitle { get; set; } = -1;
        public string? ExternalSubtitlePath { get; private set; }
        public float LastPlaybackRate { get; private set; }

        public IReadOnlyList<VideoTrackDescriptor> GetAudioTracks() => Audio;
        public IReadOnlyList<VideoTrackDescriptor> GetSubtitleTracks() => Subtitles;
        public int SelectedAudioTrackId => SelectedAudio;
        public int SelectedSubtitleTrackId => SelectedSubtitle;

        public bool SelectAudioTrack(int id)
        {
            if (!Audio.Any(track => track.Id == id)) return false;
            SelectedAudio = id;
            return true;
        }

        public bool SelectSubtitleTrack(int id)
        {
            if (id >= 0 && !Subtitles.Any(track => track.Id == id)) return false;
            SelectedSubtitle = id;
            return true;
        }

        public bool LoadExternalSubtitle(string path)
        {
            ExternalSubtitlePath = path;
            const int externalId = 99;
            Subtitles = Subtitles
                .Append(new VideoTrackDescriptor(
                    externalId,
                    Path.GetFileName(path),
                    null,
                    "External"))
                .ToArray();
            SelectedSubtitle = externalId;
            return true;
        }

        public bool SetPlaybackRate(float rate)
        {
            LastPlaybackRate = rate;
            return true;
        }
    }
}
