using System;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using ImageViewer.Services;

namespace ImageViewer.Views;

public partial class WindowsIntegrationDialog : Window
{
    private readonly bool _isStartupPrompt;
    private readonly AppSettings? _settings;
    private WindowsIntegrationStatus _status = new(WindowsIntegrationState.NotRegistered);
    private bool _selectionInitialized;

    public WindowsIntegrationDialog() : this(isStartupPrompt: false, settings: null)
    {
    }

    public WindowsIntegrationDialog(bool isStartupPrompt, AppSettings? settings = null)
    {
        _isStartupPrompt = isStartupPrompt;
        _settings = settings;
        InitializeComponent();
        InitializeAssociationSelectors();
        ConfigurePrompt();
        Closing += OnClosing;
        Opened += OnOpened;
    }

    private void ConfigurePrompt()
    {
        NeverAskAgainCheckBox.IsVisible = _settings is not null;
        NeverAskAgainCheckBox.IsChecked = _settings?.SuppressAssociationPrompt == true;
        if (!_isStartupPrompt) return;

        Title = "Missing file associations";
        HeaderText.Text = "Choose file associations";
        SubtitleText.Text = "Select the media formats to add for this Windows account";
        CloseButton.Content = "Not now";
    }

    private void OnOpened(object? sender, EventArgs e) =>
        RefreshStatus(resetSelection: true);

    private void RefreshStatus(bool resetSelection = false)
    {
        _status = WindowsFileRegistration.GetStatus();
        var hasMissingFormats = _status.Extensions.Count
                                < WindowsFileRegistration.TotalAssociationCount;
        (StatusText.Text, StatusBorder.BorderBrush) = _status.State switch
        {
            WindowsIntegrationState.RegisteredHere when hasMissingFormats =>
                ("Some formats are not registered", Brush.Parse("#e0a060")),
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

        RegisteredTypesText.Text = _status.Extensions.Count == 0
            ? "No media formats are registered."
            : $"Registered formats: {DescribeExtensions(_status.Extensions)}.";
        PathText.Text = _status.RegisteredExecutablePath is { Length: > 0 } path
            ? $"Registered executable: {path}"
            : "No executable is registered for this Windows account.";

        if (resetSelection || !_selectionInitialized)
        {
            _selectionInitialized = true;
            SetSelectedExtensions(_status.Extensions.Count == 0
                ? AllExtensions
                : _status.Extensions);
        }

        RegisterButton.Content = _status.State == WindowsIntegrationState.RegisteredHere
            ? "Apply selected"
            : "Register selected";
        if (_isStartupPrompt)
            CloseButton.Content = _status.State == WindowsIntegrationState.RegisteredHere
                                  && !hasMissingFormats
                ? "Done"
                : "Not now";
        UpdateActionAvailability();
    }

    private void UpdateActionAvailability()
    {
        RegisterButton.IsEnabled = _status.State != WindowsIntegrationState.Unsupported
                                   && SelectedExtensions.Length > 0;
        DefaultsButton.IsEnabled = _status.IsRegistered;
        RemoveButton.IsEnabled = _status.State is not
            (WindowsIntegrationState.Unsupported or WindowsIntegrationState.NotRegistered);
    }

    private void OnRegisterClicked(object? sender, RoutedEventArgs e)
    {
        var extensions = SelectedExtensions;
        RunRegistryAction(
            () => WindowsFileRegistration.RegisterCurrentExecutable(extensions),
            $"ImageViewer is now registered for {DescribeExtensions(extensions)}.");
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

    private void OnClosing(object? sender, WindowClosingEventArgs e)
    {
        if (_settings is null) return;

        _settings.SuppressAssociationPrompt = NeverAskAgainCheckBox.IsChecked == true;
        SettingsStore.Save(_settings);
    }
}
