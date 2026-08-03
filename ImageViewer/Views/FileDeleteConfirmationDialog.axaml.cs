using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;

namespace ImageViewer.Views;

public partial class FileDeleteConfirmationDialog : Window
{
    public FileDeleteConfirmationDialog()
    {
        InitializeComponent();
    }

    public FileDeleteConfirmationDialog(IReadOnlyList<string> paths)
        : this()
    {
        HeadingText.Text = paths.Count == 1
            ? "Delete 1 selected file?"
            : $"Delete {paths.Count} selected files?";
        var names = paths.Take(12).Select(Path.GetFileName).ToList();
        if (paths.Count > names.Count) names.Add($"...and {paths.Count - names.Count} more");
        FileListText.Text = string.Join(System.Environment.NewLine, names);
    }

    public static async Task<bool> ConfirmAsync(Window owner, IReadOnlyList<string> paths) =>
        await new FileDeleteConfirmationDialog(paths).ShowDialog<bool>(owner);

    private void OnConfirm(object? sender, RoutedEventArgs e) => Close(true);
    private void OnCancel(object? sender, RoutedEventArgs e) => Close(false);
}
