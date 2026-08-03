using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using ImageViewer.Models;
using ImageViewer.Services;
using ImageViewer.ViewModels;

namespace ImageViewer.Views;

public partial class DuplicateFinderWindow : Window
{
    private readonly DuplicateFinderViewModel _viewModel = new();
    private readonly DuplicateScanner _scanner = new();
    private readonly List<string> _roots = [];
    private DuplicateScanPause _pause = new();
    private CancellationTokenSource? _scanCancellation;
    private CancellationTokenSource? _thumbnailCancellation;
    private bool _isClosing;

    public DuplicateFinderWindow() : this(null) { }

    public DuplicateFinderWindow(string? initialFolder)
    {
        InitializeComponent();
        DataContext = _viewModel;
        if (!string.IsNullOrWhiteSpace(initialFolder) && Directory.Exists(initialFolder))
            _roots.Add(Path.GetFullPath(initialFolder));
        UpdateRootsSummary();
        Closed += OnWindowClosed;
    }

    private async void OnChooseFolders(object? sender, RoutedEventArgs e)
    {
        var folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "Choose folders to scan for duplicate images",
            AllowMultiple = true
        });
        var selected = folders
            .Select(folder => folder.TryGetLocalPath())
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Select(path => Path.GetFullPath(path!))
            .Distinct(FileSystemPath.Comparer)
            .ToList();
        if (selected.Count == 0) return;
        _roots.Clear();
        _roots.AddRange(selected);
        UpdateRootsSummary();
    }

    private async void OnStartScan(object? sender, RoutedEventArgs e)
    {
        if (_viewModel.IsScanning || _roots.Count == 0)
        {
            if (_roots.Count == 0)
                _viewModel.StatusText = "Choose at least one folder before starting a scan.";
            return;
        }

        _thumbnailCancellation?.Cancel();
        _scanCancellation = new CancellationTokenSource();
        _pause = new DuplicateScanPause();
        _viewModel.IsPaused = false;
        _viewModel.IsScanning = true;
        _viewModel.ProgressText = "Starting scan...";
        ScanButton.IsEnabled = false;
        try
        {
            var progress = new Progress<DuplicateScanProgress>(_viewModel.ReportProgress);
            var options = new DuplicateScanOptions(
                _roots.ToList(),
                ModePicker.SelectedIndex == 1
                    ? DuplicateScanMode.Similar
                    : DuplicateScanMode.Exact,
                (int)Math.Round(SimilaritySlider.Value));
            var result = await _scanner.ScanAsync(
                options, _pause, progress, _scanCancellation.Token);
            if (_isClosing) return;
            _viewModel.ApplyResult(result, SelectedSortMode());
            StartThumbnailLoading();
        }
        finally
        {
            _viewModel.IsScanning = false;
            _viewModel.IsPaused = false;
            ScanButton.IsEnabled = true;
            _scanCancellation?.Dispose();
            _scanCancellation = null;
        }
    }

    private void OnPauseScan(object? sender, RoutedEventArgs e)
    {
        if (!_viewModel.IsScanning) return;
        if (_pause.IsPaused)
        {
            _pause.Resume();
            _viewModel.IsPaused = false;
            PauseButton.Content = "Pause";
        }
        else
        {
            _pause.Pause();
            _viewModel.IsPaused = true;
            PauseButton.Content = "Resume";
            _viewModel.ProgressText = "Paused after active file operations finish";
        }
    }

    private void OnCancelScan(object? sender, RoutedEventArgs e)
    {
        _pause.Resume();
        _scanCancellation?.Cancel();
        _viewModel.ProgressText = "Canceling safely; completed hashes will be cached...";
    }

    private void OnModeChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (ThresholdPanel is not null && ModePicker is not null)
            ThresholdPanel.IsVisible = ModePicker.SelectedIndex == 1;
    }

    private void OnSortChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (SortPicker is not null) _viewModel.Sort(SelectedSortMode());
    }

    private DuplicateSortMode SelectedSortMode() => SortPicker.SelectedIndex switch
    {
        1 => DuplicateSortMode.GroupSize,
        2 => DuplicateSortMode.Date,
        _ => DuplicateSortMode.ReclaimableSpace
    };

    private void UpdateRootsSummary()
    {
        _viewModel.RootsSummary = _roots.Count switch
        {
            0 => "Choose one or more folders",
            1 => _roots[0],
            _ => $"{_roots.Count} folders · {string.Join(" · ", _roots)}"
        };
        ScanButton.IsEnabled = _roots.Count > 0 && !_viewModel.IsScanning;
    }

    private void OnClose(object? sender, RoutedEventArgs e) => Close();

    private void OnWindowClosed(object? sender, EventArgs e)
    {
        _isClosing = true;
        _pause.Resume();
        _scanCancellation?.Cancel();
        _thumbnailCancellation?.Cancel();
        _viewModel.Dispose();
    }
}
