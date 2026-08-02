using System;
using CommunityToolkit.Mvvm.ComponentModel;
using ImageViewer.Services;

namespace ImageViewer.ViewModels;

public partial class BrowserViewModel
{
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsSmoothScrollingActive))]
    private bool _smoothScrollingEnabled;

    public bool SystemAnimationsEnabled { get; } =
        MotionPreferences.AreAnimationsEnabled();

    public bool IsSmoothScrollingActive =>
        SmoothScrollingEnabled && SystemAnimationsEnabled;

    public string SmoothScrollingToolTip => SystemAnimationsEnabled
        ? "Animate and accelerate mouse-wheel scrolling"
        : "Disabled because Windows animations are turned off";

    public event Action? SmoothScrollingChanged;

    partial void OnSmoothScrollingEnabledChanged(bool value)
    {
        Settings.SmoothScrollingEnabled = value;
        SmoothScrollingChanged?.Invoke();
    }
}
