using System.Collections.Generic;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Interactivity;
using ImageViewer.Services;

namespace ImageViewer.Views;

public partial class BatchToolsWindow : Window
{
    public BatchToolsWindow()
    {
        InitializeComponent();
    }

    public BatchToolsWindow(IReadOnlyList<string> sourcePaths, string? currentFolder)
        : this()
    {
        var sources = sourcePaths.Distinct(FileSystemPath.Comparer).ToList();
        var imageCount = sources.Count(MediaFileTypes.IsImage);
        SourceSummaryText.Text = $"{sources.Count} selected item{(sources.Count == 1 ? "" : "s")} · " +
                                 $"{imageCount} processable image{(imageCount == 1 ? "" : "s")}";
        RenamePanel.Initialize(sources);
        ProcessPanel.Initialize(sources, currentFolder);
        RenamePanel.Completed += OnOperationCompleted;
        ProcessPanel.Completed += OnOperationCompleted;
    }

    private void OnOperationCompleted() => Close(true);
    private void OnClose(object? sender, RoutedEventArgs e) => Close(false);
}
