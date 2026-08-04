using System.IO;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using ImageViewer.Models;

namespace ImageViewer.Views;

public partial class PendingImageEditsDialog : Window
{
    public PendingImageEditsDialog()
    {
        InitializeComponent();
    }

    private PendingImageEditsDialog(string path) : this()
    {
        FileNameText.Text = Path.GetFileName(path);
        ToolTip.SetTip(FileNameText, path);
    }

    public static Task<PendingImageEditChoice> ShowAsync(Window owner, string path) =>
        new PendingImageEditsDialog(path).ShowDialog<PendingImageEditChoice>(owner);

    private void OnDiscard(object? sender, RoutedEventArgs e) =>
        Close(PendingImageEditChoice.Discard);

    private void OnCancel(object? sender, RoutedEventArgs e) =>
        Close(PendingImageEditChoice.Cancel);

    private void OnSave(object? sender, RoutedEventArgs e) =>
        Close(PendingImageEditChoice.Save);
}
