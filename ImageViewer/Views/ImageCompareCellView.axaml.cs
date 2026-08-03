using System;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using ImageViewer.Controls;
using ImageViewer.ViewModels;

namespace ImageViewer.Views;

public partial class ImageCompareCellView : UserControl
{
    public ImageCompareCellView() => InitializeComponent();

    private ImageCompareWindow? Owner => TopLevel.GetTopLevel(this) as ImageCompareWindow;

    private void OnCandidatePressed(object? sender, PointerPressedEventArgs e) =>
        Activate();

    private void OnImageFocused(object? sender, RoutedEventArgs e) => Activate();

    private void OnViewportChanged(object? sender, EventArgs e)
    {
        if (sender is ZoomPanImage image)
            Owner?.HandleCandidateViewportChanged(image);
    }

    private void OnPickCandidate(object? sender, RoutedEventArgs e)
    {
        if (DataContext is CompareCandidateViewModel candidate)
            Owner?.ToggleCandidatePick(candidate);
    }

    private void OnRejectCandidate(object? sender, RoutedEventArgs e)
    {
        if (DataContext is CompareCandidateViewModel candidate)
            Owner?.ToggleCandidateReject(candidate);
    }

    private void OnKeepCandidate(object? sender, RoutedEventArgs e)
    {
        if (DataContext is CompareCandidateViewModel candidate)
            Owner?.KeepCandidate(candidate);
    }

    private void Activate()
    {
        if (DataContext is CompareCandidateViewModel candidate)
            Owner?.ActivateCandidate(candidate);
    }
}
