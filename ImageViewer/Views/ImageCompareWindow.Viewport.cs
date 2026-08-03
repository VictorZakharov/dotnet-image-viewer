using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using ImageViewer.Controls;
using ImageViewer.ViewModels;

namespace ImageViewer.Views;

public partial class ImageCompareWindow
{
    private bool _applyingViewport;

    internal void HandleCandidateViewportChanged(ZoomPanImage source)
    {
        if (_applyingViewport) return;
        if (source.DataContext is CompareCandidateViewModel candidate)
            _viewModel.SetActive(candidate);
        if (!_viewModel.IsSynchronized) return;
        ApplyViewportToNormalImages(source.CurrentViewport, source);
    }

    private void OnSynchronizationChanged(object? sender, RoutedEventArgs e)
    {
        if (_viewModel.IsSynchronized && ActiveImageControl() is { } active)
            ApplyViewportToNormalImages(active.CurrentViewport, active);
    }

    private void OnFit(object? sender, RoutedEventArgs e) => ApplyFit();
    private void OnActualSize(object? sender, RoutedEventArgs e) => ApplyActualSize();

    private void ApplyFit()
    {
        ApplyToTargets(control => control.ApplyViewport(
            new NormalizedImageViewport(0.5, 0.5, 1, true)));
    }

    private void ApplyActualSize()
    {
        ApplyToTargets(control => control.SetActualSize(notify: false));
    }

    private void ApplyToTargets(Action<ZoomPanImage> action)
    {
        _applyingViewport = true;
        try
        {
            foreach (var control in NormalImageControls())
            {
                if (_viewModel.IsSynchronized
                    || ReferenceEquals(control.DataContext, _viewModel.ActiveCandidate))
                    action(control);
            }
            if (_viewModel.IsBlinking) RefreshBlinkImage();
        }
        finally { _applyingViewport = false; }
    }

    private void ApplyViewportToNormalImages(
        NormalizedImageViewport state,
        ZoomPanImage? except = null)
    {
        _applyingViewport = true;
        try
        {
            foreach (var control in NormalImageControls())
                if (!ReferenceEquals(control, except)) control.ApplyViewport(state);
            if (_viewModel.IsBlinking && !ReferenceEquals(BlinkImage, except))
                BlinkImage.ApplyViewport(state);
        }
        finally { _applyingViewport = false; }
    }

    private IReadOnlyList<ZoomPanImage> NormalImageControls() => CandidateItems
        .GetVisualDescendants()
        .OfType<ZoomPanImage>()
        .Where(control => control.DataContext is CompareCandidateViewModel)
        .ToList();

    private ZoomPanImage? ActiveImageControl() => NormalImageControls()
        .FirstOrDefault(control => ReferenceEquals(
            control.DataContext, _viewModel.ActiveCandidate));

    private void SynchronizeLoadedCandidate(CompareCandidateViewModel candidate)
    {
        if (!_viewModel.IsSynchronized) return;
        Dispatcher.UIThread.Post(() =>
        {
            var active = ActiveImageControl();
            var target = NormalImageControls().FirstOrDefault(control =>
                ReferenceEquals(control.DataContext, candidate));
            if (active is not null && target is not null && !ReferenceEquals(active, target))
                target.ApplyViewport(active.CurrentViewport);
        }, DispatcherPriority.Loaded);
    }
}
