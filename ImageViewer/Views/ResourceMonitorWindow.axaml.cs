using Avalonia.Controls;
using Avalonia.Interactivity;
using ImageViewer.ViewModels;

namespace ImageViewer.Views;

public partial class ResourceMonitorWindow : Window
{
    private readonly ResourceMonitorViewModel _viewModel;
    private readonly bool _ownsViewModel;

    public ResourceMonitorWindow()
        : this(new ResourceMonitorViewModel(), ownsViewModel: true)
    {
    }

    public ResourceMonitorWindow(ResourceMonitorViewModel viewModel)
        : this(viewModel, ownsViewModel: false)
    {
    }

    private ResourceMonitorWindow(ResourceMonitorViewModel viewModel, bool ownsViewModel)
    {
        _viewModel = viewModel;
        _ownsViewModel = ownsViewModel;
        InitializeComponent();
        DataContext = _viewModel;
        _viewModel.BeginWindowSession();
        Closed += OnClosed;
    }

    private void OnClose(object? sender, RoutedEventArgs e) => Close();

    private void OnClosed(object? sender, System.EventArgs e)
    {
        _viewModel.EndWindowSession();
        if (_ownsViewModel)
            _viewModel.Dispose();
    }
}
