using System.Collections.Generic;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Interactivity;
using ImageViewer.Models;

namespace ImageViewer.Views;

public partial class FileOperationResultDialog : Window
{
    public FileOperationResultDialog()
    {
        InitializeComponent();
    }

    public FileOperationResultDialog(FileOperationResult result)
        : this()
    {
        HeadingText.Text = result.Failures.Count == 0
            ? "File operation complete"
            : "Some files could not be processed";
        SummaryText.Text = $"{result.Successful.Count} succeeded, " +
                           $"{result.SkippedPaths.Count} skipped, " +
                           $"{result.Failures.Count} failed" +
                           (result.IsCanceled ? " · canceled" : "");
        DetailsText.Text = result.Failures.Count == 0
            ? "No file failures."
            : string.Join("\n\n", result.Failures.Select(failure =>
                $"{failure.SourcePath}\n{failure.Error}"));
    }

    public FileOperationResultDialog(string heading, BatchOperationResult result)
        : this()
    {
        HeadingText.Text = heading;
        SummaryText.Text = result.Summary;
        var details = new List<string>();
        details.AddRange(result.Failures.Select(failure =>
            $"FAILED: {failure.SourcePath}\n{failure.Error}"));
        details.AddRange(result.UnprocessedPaths.Select(path => $"NOT STARTED: {path}"));
        DetailsText.Text = details.Count == 0
            ? "No file failures. Every completed output is valid."
            : string.Join("\n\n", details);
    }

    private void OnClose(object? sender, RoutedEventArgs e) => Close();
}
