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

    private void OnClose(object? sender, RoutedEventArgs e) => Close();
}
