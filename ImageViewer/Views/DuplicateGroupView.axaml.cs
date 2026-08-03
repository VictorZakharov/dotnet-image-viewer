using Avalonia.Controls;
using Avalonia.Interactivity;
using ImageViewer.ViewModels;

namespace ImageViewer.Views;

public partial class DuplicateGroupView : UserControl
{
    public DuplicateGroupView() => InitializeComponent();

    private void OnSelectSuggested(object? sender, RoutedEventArgs e)
    {
        if (DataContext is DuplicateGroupViewModel group)
            group.SelectSuggestedDuplicates();
    }

    private async void OnCompare(object? sender, RoutedEventArgs e)
    {
        if (DataContext is DuplicateGroupViewModel group
            && TopLevel.GetTopLevel(this) is DuplicateFinderWindow owner)
            await owner.CompareGroupAsync(group);
    }
}
