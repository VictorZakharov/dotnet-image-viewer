using System;
using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using ImageViewer.ViewModels;
using LibVLCSharp.Avalonia;

namespace ImageViewer.Views;

public partial class ViewerView : UserControl
{
    private ViewerViewModel? _viewModel;
    private VideoView? _videoView;
    private VideoOverlayView? _videoOverlay;

    public ViewerView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
        AttachedToVisualTree += (_, _) => SchedulePlayerReattach();
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (_viewModel is not null)
            _viewModel.PropertyChanged -= OnViewModelPropertyChanged;

        _viewModel = DataContext as ViewerViewModel;
        if (_viewModel is not null)
            _viewModel.PropertyChanged += OnViewModelPropertyChanged;

        UpdateVideoView();
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(ViewerViewModel.VideoPlayer)
            or nameof(ViewerViewModel.IsVideo))
        {
            UpdateVideoView();
        }
    }

    private void UpdateVideoView()
    {
        if (_viewModel?.VideoPlayer is null)
        {
            if (_videoView is not null) _videoView.MediaPlayer = null;
            return;
        }

        if (_videoView is null)
        {
            _videoOverlay = new VideoOverlayView { DataContext = _viewModel };
            _videoView = new VideoView
            {
                HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch,
                VerticalAlignment = Avalonia.Layout.VerticalAlignment.Stretch,
                Content = _videoOverlay
            };
        }

        if (_videoOverlay is not null)
            _videoOverlay.DataContext = _viewModel;
        _videoView.MediaPlayer = _viewModel.VideoPlayer;
        VideoHost.Content = _videoView;
        SchedulePlayerReattach();
    }

    private void SchedulePlayerReattach()
    {
        var player = _viewModel?.VideoPlayer;
        if (player is null || _videoView is null) return;

        // NativeControlHost can receive MediaPlayer before its HWND exists,
        // especially when this cached view is reattached after browser mode.
        // Re-applying it after layout guarantees LibVLC renders in our host
        // instead of creating a separate VLC output window.
        Dispatcher.UIThread.Post(() =>
        {
            if (_videoView is null || !ReferenceEquals(_viewModel?.VideoPlayer, player))
                return;

            _videoView.MediaPlayer = null;
            _videoView.MediaPlayer = player;
        }, DispatcherPriority.Loaded);
    }

    private void OnPropertiesClicked(object? sender, RoutedEventArgs e)
    {
        if (DataContext is ViewerViewModel vm)
            vm.ToggleExifOverlayCommand.Execute(null);
    }

    private void OnExifPillClicked(object? sender, PointerPressedEventArgs e)
    {
        if (DataContext is ViewerViewModel vm)
            vm.ToggleExifOverlayCommand.Execute(null);
        e.Handled = true;
    }
}
