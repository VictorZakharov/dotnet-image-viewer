using Avalonia.Controls;
using Avalonia.Interactivity;
using ImageViewer.ViewModels;

namespace ImageViewer.Views;

public partial class ResourceMonitorWindow : Window
{
    private readonly ResourceMonitorViewModel _viewModel = new();

    public ResourceMonitorWindow()
    {
        InitializeComponent();
        DataContext = _viewModel;
        Closed += (_, _) => _viewModel.Dispose();
    }

    private void OnClose(object? sender, RoutedEventArgs e) => Close();
}
