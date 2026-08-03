using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;

namespace ImageViewer.Views;

public partial class BatchConfirmationDialog : Window
{
    public BatchConfirmationDialog()
    {
        InitializeComponent();
    }

    private BatchConfirmationDialog(
        string heading,
        string warning,
        string confirmLabel,
        IReadOnlyList<string> paths)
        : this()
    {
        HeadingText.Text = heading;
        WarningText.Text = warning;
        ConfirmButton.Content = confirmLabel;
        var names = paths.Take(16).Select(path => Path.GetFileName(path)).ToList();
        if (paths.Count > names.Count) names.Add($"...and {paths.Count - names.Count} more");
        ItemListText.Text = string.Join(System.Environment.NewLine, names);
    }

    public static Task<bool> ConfirmAsync(
        Window owner,
        string heading,
        string warning,
        string confirmLabel,
        IReadOnlyList<string> paths) =>
        new BatchConfirmationDialog(heading, warning, confirmLabel, paths)
            .ShowDialog<bool>(owner);

    private void OnConfirm(object? sender, RoutedEventArgs e) => Close(true);
    private void OnCancel(object? sender, RoutedEventArgs e) => Close(false);
}
