using System;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using ImageViewer.Services;

namespace ImageViewer.Views;

public partial class WindowsIntegrationDialog : Window
{
    public WindowsIntegrationDialog()
    {
        InitializeComponent();
        Opened += (_, _) => RefreshStatus();
    }

    private void RefreshStatus()
    {
        var status = WindowsFileRegistration.GetStatus();
        (StatusText.Text, StatusBorder.BorderBrush) = status.State switch
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

        PathText.Text = status.RegisteredExecutablePath is { Length: > 0 } path
            ? $"Registered executable: {path}"
            : "No executable is registered for this Windows account.";
        RegisterButton.Content = status.State == WindowsIntegrationState.RegisteredHere
            ? "Repair registration"
            : "Register this copy";
        RegisterButton.IsEnabled = status.State != WindowsIntegrationState.Unsupported;
        DefaultsButton.IsEnabled = status.IsRegistered;
        RemoveButton.IsEnabled = status.State is not
            (WindowsIntegrationState.Unsupported or WindowsIntegrationState.NotRegistered);
    }

    private void OnRegisterClicked(object? sender, RoutedEventArgs e)
    {
        RunRegistryAction(
            WindowsFileRegistration.RegisterCurrentExecutable,
            "ImageViewer is now available in Open with and Default Apps.");
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
        RefreshStatus();
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
}
