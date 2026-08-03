using System;
using System.Collections.Generic;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using ImageViewer.Models;
using ImageViewer.ViewModels;

namespace ImageViewer.Views;

public partial class ImageCompareWindow : Window
{
    private readonly ImageCompareViewModel _viewModel;
    private bool _resourcesReleased;

    public ImageCompareResult? Result { get; private set; }

    public ImageCompareWindow() : this(Array.Empty<string>()) { }

    public ImageCompareWindow(IReadOnlyList<string> paths)
    {
        InitializeComponent();
        _viewModel = new ImageCompareViewModel(paths);
        DataContext = _viewModel;
        AddHandler(KeyDownEvent, OnPreviewKeyDown, RoutingStrategies.Tunnel);
        Opened += OnOpened;
        Closing += (_, _) => ReleaseResources();
    }

    private void OnOpened(object? sender, EventArgs e)
    {
        StartCandidateLoading();
        Focus();
    }

    internal void ActivateCandidate(CompareCandidateViewModel candidate) =>
        _viewModel.SetActive(candidate);

    internal void ToggleCandidatePick(CompareCandidateViewModel candidate)
    {
        _viewModel.SetActive(candidate);
        _viewModel.TogglePick();
    }

    internal void ToggleCandidateReject(CompareCandidateViewModel candidate)
    {
        _viewModel.SetActive(candidate);
        _viewModel.ToggleReject();
    }

    internal void KeepCandidate(CompareCandidateViewModel candidate)
    {
        _viewModel.SetActive(candidate);
        _viewModel.KeepActiveRejectOthers();
    }

    private void OnPreviewKeyDown(object? sender, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Left: _viewModel.MoveActive(-1); break;
            case Key.Right: _viewModel.MoveActive(1); break;
            case Key.F: ApplyFit(); break;
            case Key.A: ApplyActualSize(); break;
            case Key.S: _viewModel.IsSynchronized = !_viewModel.IsSynchronized; break;
            case Key.B:
            case Key.Space: AlternateBlink(); break;
            case Key.P: _viewModel.TogglePick(); break;
            case Key.X: _viewModel.ToggleReject(); break;
            case Key.K: _viewModel.KeepActiveRejectOthers(); break;
            case Key.Delete: _ = DeleteRejectedAsync(); break;
            case Key.Escape when _viewModel.IsBlinking: ExitBlink(); break;
            case Key.Escape: Close(); break;
            default: return;
        }
        e.Handled = true;
    }

    private void OnClose(object? sender, RoutedEventArgs e) => Close();

    private void ReleaseResources()
    {
        if (_resourcesReleased) return;
        _resourcesReleased = true;
        Result = _viewModel.CreateResult();
        CancelCandidateLoading();
        ExitBlink();
        _viewModel.Dispose();
        DisposeLoadingInfrastructureWhenSafe();
    }
}
