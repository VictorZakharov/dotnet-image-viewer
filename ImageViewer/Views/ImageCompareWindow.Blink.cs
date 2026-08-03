using System;
using System.Linq;
using Avalonia.Interactivity;
using ImageViewer.Controls;

namespace ImageViewer.Views;

public partial class ImageCompareWindow
{
    private int _blinkIndex;

    private void OnBlink(object? sender, RoutedEventArgs e) => AlternateBlink();
    private void OnExitBlink(object? sender, RoutedEventArgs e) => ExitBlink();

    private void AlternateBlink()
    {
        if (!_viewModel.CanBlink) return;
        if (_viewModel.IsBlinking)
            _blinkIndex = (_blinkIndex + 1) % 2;
        else
        {
            _viewModel.IsBlinking = true;
            _blinkIndex = 0;
        }
        RefreshBlinkImage();
    }

    private void ExitBlink()
    {
        if (!_viewModel.IsBlinking && BlinkImage.Source is null) return;
        _viewModel.IsBlinking = false;
        BlinkImage.Source = null;
    }

    private void RefreshBlinkImage()
    {
        if (!_viewModel.IsBlinking || _viewModel.Candidates.Count != 2) return;
        var candidate = _viewModel.Candidates[_blinkIndex];
        _viewModel.SetActive(candidate);
        BlinkImage.Source = candidate.Bitmap;
        BlinkImage.Rotation = candidate.Rotation;
        BlinkLabel.Text = $"Blink {_blinkIndex + 1}/2 · {candidate.FileName}";

        var sourceControl = NormalImageControls().FirstOrDefault(control =>
            ReferenceEquals(control.DataContext, candidate));
        if (sourceControl is null) return;
        _applyingViewport = true;
        try { BlinkImage.ApplyViewport(sourceControl.CurrentViewport); }
        finally { _applyingViewport = false; }
    }

    private void OnBlinkViewportChanged(object? sender, EventArgs e)
    {
        if (_applyingViewport || !_viewModel.IsBlinking) return;
        var state = BlinkImage.CurrentViewport;
        if (_viewModel.IsSynchronized)
        {
            ApplyViewportToNormalImages(state);
            return;
        }

        var candidate = _viewModel.Candidates[_blinkIndex];
        var control = NormalImageControls().FirstOrDefault(image =>
            ReferenceEquals(image.DataContext, candidate));
        control?.ApplyViewport(state);
    }
}
