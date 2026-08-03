using System;
using Avalonia.Interactivity;

namespace ImageViewer.Views;

public partial class BrowserView
{
    private DuplicateFinderWindow? _duplicateFinderWindow;

    private void OnFindDuplicatesClicked(object? sender, RoutedEventArgs e)
    {
        if (_duplicateFinderWindow is not null)
        {
            _duplicateFinderWindow.Activate();
            return;
        }

        if (GetOwnerWindow() is not { } owner) return;
        _duplicateFinderWindow = new DuplicateFinderWindow(_vm?.CurrentFolder);
        _duplicateFinderWindow.Closed += OnDuplicateFinderClosed;
        _duplicateFinderWindow.Show(owner);
    }

    private void OnDuplicateFinderClosed(object? sender, EventArgs e)
    {
        if (_duplicateFinderWindow is not null)
            _duplicateFinderWindow.Closed -= OnDuplicateFinderClosed;
        _duplicateFinderWindow = null;
    }
}
