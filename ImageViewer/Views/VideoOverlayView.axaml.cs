using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using ImageViewer.ViewModels;

namespace ImageViewer.Views;

public partial class VideoOverlayView : UserControl
{
    private bool _isTimelineScrubbing;

    public VideoOverlayView()
    {
        InitializeComponent();
        AddHandler(InputElement.KeyDownEvent, OnOverlayKeyDown, RoutingStrategies.Tunnel);
    }

    private void OnTimelinePointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (DataContext is not ViewerViewModel viewer ||
            !e.GetCurrentPoint(TimelineSlider).Properties.IsLeftButtonPressed)
        {
            return;
        }

        _isTimelineScrubbing = true;
        var position = UpdateTimelineFromPointer(e);
        viewer.BeginScrubPreview(position);
    }

    private void OnTimelinePointerMoved(object? sender, PointerEventArgs e)
    {
        if (!_isTimelineScrubbing || DataContext is not ViewerViewModel viewer) return;
        if (!e.GetCurrentPoint(TimelineSlider).Properties.IsLeftButtonPressed)
        {
            FinishTimelineScrub(viewer);
            return;
        }

        viewer.UpdateScrubPreview(UpdateTimelineFromPointer(e));
    }

    private void OnTimelinePointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (!_isTimelineScrubbing || DataContext is not ViewerViewModel viewer) return;
        viewer.UpdateScrubPreview(UpdateTimelineFromPointer(e));
        FinishTimelineScrub(viewer);
    }

    private void OnTimelinePointerCaptureLost(object? sender, PointerCaptureLostEventArgs e)
    {
        if (_isTimelineScrubbing && DataContext is ViewerViewModel viewer)
            FinishTimelineScrub(viewer);
    }

    private double UpdateTimelineFromPointer(PointerEventArgs e)
    {
        var timelinePoint = e.GetPosition(TimelineSlider);
        var width = TimelineSlider.Bounds.Width;
        var position = width <= 0 ? TimelineSlider.Value : Math.Clamp(timelinePoint.X / width, 0, 1);
        TimelineSlider.Value = position;

        const double previewWidth = 208;
        const double edgeMargin = 8;
        var pointerX = e.GetPosition(this).X;
        var availableWidth = Math.Max(0, Bounds.Width - previewWidth - (edgeMargin * 2));
        var left = edgeMargin + Math.Clamp(
            pointerX - (previewWidth / 2) - edgeMargin,
            0,
            availableWidth);
        ScrubPreviewPopup.Margin = new Thickness(left, 0, 0, 84);
        return position;
    }

    private void FinishTimelineScrub(ViewerViewModel viewer)
    {
        _isTimelineScrubbing = false;
        viewer.EndScrubPreview();
    }

    private void OnOverlayKeyDown(object? sender, KeyEventArgs e)
    {
        if (DataContext is not ViewerViewModel viewer) return;

        // LibVLCSharp renders this content in a small owned overlay window.
        // Once a playback control has focus, its keys no longer bubble to the
        // main window, so mirror the viewer shortcuts here.
        switch (e.Key)
        {
            case Key.Escape:
                if (viewer.IsFullscreen)
                    viewer.ToggleFullscreenCommand.Execute(null);
                else if (GetMainWindow() is
                         { DataContext: MainWindowViewModel { CloseViewerOnEscape: true } } mainWindow)
                    mainWindow.Close();
                else if (GetMainViewModel() is { } main)
                    main.ToggleModeCommand.Execute(null);
                e.Handled = true;
                break;
            case Key.Enter when e.Source is not Button:
                GetMainViewModel()?.ToggleModeCommand.Execute(null);
                e.Handled = true;
                break;
            case Key.Left when e.Source is not Slider:
            case Key.Up when e.Source is not Slider:
                viewer.PreviousCommand.Execute(null);
                e.Handled = true;
                break;
            case Key.Right when e.Source is not Slider:
            case Key.Down when e.Source is not Slider:
                viewer.NextCommand.Execute(null);
                e.Handled = true;
                break;
            case Key.F:
            case Key.F11:
                viewer.ToggleFullscreenCommand.Execute(null);
                e.Handled = true;
                break;
            case Key.I:
                viewer.ToggleExifOverlayCommand.Execute(null);
                e.Handled = true;
                break;
            case Key.Space when e.Source is not Button:
                viewer.TogglePlaybackCommand.Execute(null);
                e.Handled = true;
                break;
        }
    }

    private MainWindowViewModel? GetMainViewModel()
    {
        if (GetMainWindow()?.DataContext is MainWindowViewModel main)
        {
            return main;
        }

        return null;
    }

    private MainWindow? GetMainWindow() =>
        TopLevel.GetTopLevel(this) is Window overlay && overlay.Owner is MainWindow mainWindow
            ? mainWindow
            : null;

    private void OnSubtitleToolsClicked(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button button || DataContext is not ViewerViewModel vm) return;
        var tools = vm.VideoTools;
        tools.Refresh();

        var items = new List<object>
        {
            RadioItem("Off", tools.IsSubtitleOff, "subtitle-tracks", () =>
                tools.SelectSubtitleTrack(-1))
        };

        if (!tools.HasSubtitleTracks)
        {
            items.Add(new MenuItem
            {
                Header = "No embedded subtitle tracks",
                IsEnabled = false
            });
        }
        else
        {
            foreach (var track in tools.SubtitleTracks)
            {
                var trackId = track.Id;
                items.Add(RadioItem(track.Label, track.IsSelected, "subtitle-tracks", () =>
                    tools.SelectSubtitleTrack(trackId)));
            }
        }

        if (!string.IsNullOrEmpty(tools.PendingExternalSubtitleLabel))
        {
            items.Add(new MenuItem
            {
                Header = $"Loading {tools.PendingExternalSubtitleLabel}...",
                IsEnabled = false,
                ToggleType = MenuItemToggleType.Radio,
                IsChecked = true,
                GroupName = "subtitle-tracks"
            });
        }

        items.Add(new Separator());
        var loadItem = new MenuItem { Header = "Load subtitle file..." };
        loadItem.Click += async (_, _) => await LoadSubtitleFileAsync(vm);
        items.Add(loadItem);
        ShowMenu(button, items);
    }

    private void OnAudioToolsClicked(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button button || DataContext is not ViewerViewModel vm) return;
        var tools = vm.VideoTools;
        tools.Refresh();
        var items = tools.AudioTracks
            .Select(track =>
            {
                var trackId = track.Id;
                return (object)RadioItem(track.Label, track.IsSelected, "audio-tracks", () =>
                    tools.SelectAudioTrack(trackId));
            })
            .ToList();
        if (items.Count == 0)
            items.Add(new MenuItem { Header = "No selectable audio tracks", IsEnabled = false });
        ShowMenu(button, items);
    }

    private void OnPlaybackSpeedClicked(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button button || DataContext is not ViewerViewModel vm) return;
        var tools = vm.VideoTools;
        var items = VideoPlaybackTools.PlaybackRatePresets
            .Select(rate =>
            {
                var selectedRate = rate;
                return (object)RadioItem(
                    VideoPlaybackTools.FormatPlaybackRate(rate),
                    Math.Abs(rate - tools.PlaybackRate) < 0.001f,
                    "playback-rates",
                    () => tools.SelectPlaybackRate(selectedRate));
            })
            .ToList();
        ShowMenu(button, items);
    }

    private void OnVideoInfoClicked(object? sender, RoutedEventArgs e)
    {
        if (DataContext is ViewerViewModel vm)
            vm.ToggleExifOverlayCommand.Execute(null);
    }

    private async Task LoadSubtitleFileAsync(ViewerViewModel vm)
    {
        var topLevel = (TopLevel?)GetMainWindow() ?? TopLevel.GetTopLevel(this);
        if (topLevel is null) return;
        var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Load subtitle file",
            AllowMultiple = false,
            FileTypeFilter =
            [
                new FilePickerFileType("Subtitle files")
                {
                    Patterns = ["*.srt", "*.ass", "*.ssa", "*.vtt", "*.sub", "*.smi"]
                }
            ]
        });
        var path = files.FirstOrDefault()?.TryGetLocalPath();
        if (!string.IsNullOrEmpty(path))
            vm.LoadExternalSubtitle(path);
    }

    private static MenuItem RadioItem(
        string header,
        bool isChecked,
        string groupName,
        Action action)
    {
        var item = new MenuItem
        {
            Header = header,
            ToggleType = MenuItemToggleType.Radio,
            IsChecked = isChecked,
            GroupName = groupName
        };
        item.Click += (_, _) => action();
        return item;
    }

    private static void ShowMenu(Control target, IReadOnlyList<object> items)
    {
        var flyout = new MenuFlyout { ItemsSource = items };
        flyout.ShowAt(target);
    }
}
