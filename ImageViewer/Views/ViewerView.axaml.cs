using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using ImageViewer.ViewModels;

namespace ImageViewer.Views;

public partial class ViewerView : UserControl
{
    public ViewerView()
    {
        InitializeComponent();
    }

    private void OnPropertiesClicked(object? sender, RoutedEventArgs e)
    {
        if (DataContext is ViewerViewModel vm)
            vm.ToggleExifOverlayCommand.Execute(null);
    }

    private void OnExifPillClicked(object? sender, PointerPressedEventArgs e)
    {
        if (DataContext is ViewerViewModel vm)
            vm.ToggleExifOverlayCommand.Execute(null);
        e.Handled = true;
    }
}
