using System;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using ImageViewer.Services;

namespace ImageViewer.Views;

public partial class WindowsIntegrationDialog : Window
{
    private readonly bool _isStartupPrompt;
    private WindowsIntegrationStatus _status = new(WindowsIntegrationState.NotRegistered);
    private bool _selectionInitialized;

    public WindowsIntegrationDialog() : this(isStartupPrompt: false)
    {
    }

    public WindowsIntegrationDialog(bool isStartupPrompt)
    {
        _isStartupPrompt = isStartupPrompt;
        InitializeComponent();
        Opened += OnOpened;
    }

    private void OnOpened(object? sender, EventArgs e)
    {
        ImagesCheckBox.Content = $"Images ({WindowsFileRegistration.ImageAssociationCount} formats)";
        VideosCheckBox.Content = $"Videos ({WindowsFileRegistration.VideoAssociationCount} formats)";

        if (_isStartupPrompt)
        {
            Title = "Missing file associations";
            HeaderText.Text = "Choose file associations";
            SubtitleText.Text = "Select the media groups to add for this Windows account";
            CloseButton.Content = "Not now";
        }

        RefreshStatus(resetSelection: true);
    }

    private void RefreshStatus(bool resetSelection = false)
    {
        _status = WindowsFileRegistration.GetStatus();
        (StatusText.Text, StatusBorder.BorderBrush) = _status.State switch
        {
            WindowsIntegrationState.RegisteredHere =>
                ("Registered for this copy", Brush.Parse("#5bc78a")),
            WindowsIntegrationState.RegisteredElsewhere =>
                ("Registered to another portable copy", Brush.Parse("#e0a060")),
            WindowsIntegrationState.NeedsRepair =>
                ("Registration needs repair", Brush.Parse("#e0a060")),
            WindowsIntegrationState.Unsupported =>
                ("Available only on Windows", Brush.Parse("#e76060")),
            _ => ("Not registered", Brush.Parse("#445573"))
        };

        RegisteredTypesText.Text = _status.RegisteredGroups == MediaAssociationGroups.None
            ? "No media groups are registered."
            : $"Registered types: {DescribeGroups(_status.RegisteredGroups)}.";
        PathText.Text = _status.RegisteredExecutablePath is { Length: > 0 } path
            ? $"Registered executable: {path}"
            : "No executable is registered for this Windows account.";

        if (resetSelection || !_selectionInitialized)
        {
            var groups = _status.RegisteredGroups == MediaAssociationGroups.None
                ? MediaAssociationGroups.All
                : _status.RegisteredGroups;
            _selectionInitialized = true;
            ImagesCheckBox.IsChecked = (groups & MediaAssociationGroups.Images) != 0;
            VideosCheckBox.IsChecked = (groups & MediaAssociationGroups.Videos) != 0;
        }

        RegisterButton.Content = _status.State == WindowsIntegrationState.RegisteredHere
            ? "Apply selected"
            : "Register selected";
        if (_isStartupPrompt)
            CloseButton.Content = _status.State == WindowsIntegrationState.RegisteredHere
                ? "Done"
                : "Not now";
        UpdateActionAvailability();
    }

    private MediaAssociationGroups SelectedGroups
    {
        get
        {
            var groups = MediaAssociationGroups.None;
            if (ImagesCheckBox.IsChecked == true) groups |= MediaAssociationGroups.Images;
            if (VideosCheckBox.IsChecked == true) groups |= MediaAssociationGroups.Videos;
            return groups;
        }
    }

    private void UpdateActionAvailability()
    {
        RegisterButton.IsEnabled = _status.State != WindowsIntegrationState.Unsupported
                                   && SelectedGroups != MediaAssociationGroups.None;
        DefaultsButton.IsEnabled = _status.IsRegistered;
        RemoveButton.IsEnabled = _status.State is not
            (WindowsIntegrationState.Unsupported or WindowsIntegrationState.NotRegistered);
    }

    private void OnAssociationSelectionChanged(object? sender, RoutedEventArgs e) =>
        UpdateActionAvailability();

    private void OnRegisterClicked(object? sender, RoutedEventArgs e)
    {
        var groups = SelectedGroups;
        RunRegistryAction(
            () => WindowsFileRegistration.RegisterCurrentExecutable(groups),
            $"ImageViewer is now registered for {DescribeGroups(groups)}.");
    }

    private void OnUnregisterClicked(object? sender, RoutedEventArgs e)
    {
        RunRegistryAction(
            WindowsFileRegistration.UnregisterCurrentUser,
            "ImageViewer's per-user Explorer integration was removed.");
    }

    private void RunRegistryAction(Action action, string successMessage)
    {
        try
        {
            action();
            ResultText.Foreground = Brush.Parse("#8fcfa8");
            ResultText.Text = successMessage;
        }
        catch (Exception ex)
        {
            ResultText.Foreground = Brush.Parse("#ff8a8a");
            ResultText.Text = ex.Message;
        }
        RefreshStatus(resetSelection: true);
    }

    private void OnDefaultsClicked(object? sender, RoutedEventArgs e)
    {
        var opened = WindowsIntegrationLauncher.OpenDefaultApps();
        ResultText.Foreground = Brush.Parse(opened ? "#8fcfa8" : "#ff8a8a");
        ResultText.Text = opened
            ? "Choose ImageViewer for the file types you want Windows to open with it."
            : "Windows Settings could not be opened.";
    }

    private void OnCloseClicked(object? sender, RoutedEventArgs e) => Close();

    private static string DescribeGroups(MediaAssociationGroups groups) => groups switch
    {
        MediaAssociationGroups.Images => "images",
        MediaAssociationGroups.Videos => "videos",
        MediaAssociationGroups.All => "images and videos",
        _ => "no media types"
    };
}
