using System;
using System.IO;
using System.Threading;
using Avalonia.Controls;
using Avalonia.Interactivity;
using ImageViewer.Models;

namespace ImageViewer.Views;

public partial class FileOperationProgressDialog : Window
{
    private readonly CancellationTokenSource _cancellation = new();
    private bool _canClose;

    public CancellationToken CancellationToken => _cancellation.Token;

    public FileOperationProgressDialog()
    {
        InitializeComponent();
        Closing += (_, args) =>
        {
            if (_canClose) return;
            _cancellation.Cancel();
            args.Cancel = true;
        };
    }

    public FileOperationProgressDialog(string heading)
        : this()
    {
        HeadingText.Text = heading;
    }

    public void Report(FileOperationProgress progress)
    {
        OperationProgressBar.Value = progress.Percentage;
        ProgressText.Text = $"{Math.Min(progress.Completed, progress.Total)} of {progress.Total}";
        CurrentFileText.Text = string.IsNullOrEmpty(progress.CurrentPath)
            ? "Finishing..."
            : Path.GetFileName(progress.CurrentPath);
    }

    public void Finish()
    {
        _canClose = true;
        Close();
        _cancellation.Dispose();
    }

    private void OnCancel(object? sender, RoutedEventArgs e)
    {
        _cancellation.Cancel();
        CurrentFileText.Text = "Canceling after the current safe step...";
        if (sender is Button button) button.IsEnabled = false;
    }
}
