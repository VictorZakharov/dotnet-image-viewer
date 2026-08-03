using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using ImageViewer.Models;

namespace ImageViewer.Views;

public partial class FileCollisionDialog : Window
{
    public FileCollisionDialog()
    {
        InitializeComponent();
    }

    public FileCollisionDialog(FileCollision collision)
        : this()
    {
        SourcePathText.Text = collision.SourcePath;
        DestinationPathText.Text = collision.DestinationPath;
        ReplaceButton.IsEnabled = !collision.IsSamePath;
    }

    public static async Task<FileCollisionDecision> ShowAsync(
        Window owner,
        FileCollision collision)
    {
        var result = await new FileCollisionDialog(collision)
            .ShowDialog<FileCollisionDecision?>(owner);
        return result ?? new FileCollisionDecision(FileCollisionChoice.Cancel, false);
    }

    private void OnSkip(object? sender, RoutedEventArgs e) => Complete(FileCollisionChoice.Skip);
    private void OnReplace(object? sender, RoutedEventArgs e) => Complete(FileCollisionChoice.Replace);
    private void OnRename(object? sender, RoutedEventArgs e) => Complete(FileCollisionChoice.Rename);
    private void OnCancel(object? sender, RoutedEventArgs e) => Complete(FileCollisionChoice.Cancel);

    private void Complete(FileCollisionChoice choice) => Close(new FileCollisionDecision(
        choice,
        ApplyToRemainingCheckBox.IsChecked == true));
}
