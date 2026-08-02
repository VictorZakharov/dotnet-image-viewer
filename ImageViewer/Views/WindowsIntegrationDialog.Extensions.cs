using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Interactivity;
using ImageViewer.Services;

namespace ImageViewer.Views;

public partial class WindowsIntegrationDialog
{
    private readonly List<(string Extension, CheckBox CheckBox)> _imageOptions = [];
    private readonly List<(string Extension, CheckBox CheckBox)> _videoOptions = [];
    private bool _updatingSelection;

    private static string[] AllExtensions =>
        [.. WindowsFileRegistration.ImageExtensions, .. WindowsFileRegistration.VideoExtensions];

    private string[] SelectedExtensions =>
        _imageOptions.Concat(_videoOptions)
            .Where(option => option.CheckBox.IsChecked == true)
            .Select(option => option.Extension)
            .ToArray();

    private void InitializeAssociationSelectors()
    {
        AddExtensionOptions(
            ImageExtensionsPanel,
            WindowsFileRegistration.ImageExtensions,
            _imageOptions);
        AddExtensionOptions(
            VideoExtensionsPanel,
            WindowsFileRegistration.VideoExtensions,
            _videoOptions);
        SetSelectedExtensions(AllExtensions);
    }

    private void AddExtensionOptions(
        WrapPanel panel,
        IReadOnlyList<string> extensions,
        List<(string Extension, CheckBox CheckBox)> options)
    {
        foreach (var extension in extensions)
        {
            var checkBox = new CheckBox
            {
                Content = extension,
                FontSize = 11,
                Width = 65
            };
            AutomationProperties.SetAutomationId(
                checkBox,
                $"Extension_{extension.TrimStart('.')}");
            checkBox.IsCheckedChanged += OnExtensionSelectionChanged;
            panel.Children.Add(checkBox);
            options.Add((extension, checkBox));
        }
    }

    private void SetSelectedExtensions(IReadOnlyCollection<string> extensions)
    {
        var selected = new HashSet<string>(extensions, StringComparer.OrdinalIgnoreCase);
        _updatingSelection = true;
        try
        {
            foreach (var option in _imageOptions.Concat(_videoOptions))
                option.CheckBox.IsChecked = selected.Contains(option.Extension);
            UpdateGroupState(ImagesCheckBox, _imageOptions, "Images");
            UpdateGroupState(VideosCheckBox, _videoOptions, "Videos");
        }
        finally
        {
            _updatingSelection = false;
        }
        UpdateActionAvailability();
    }

    private void OnGroupClicked(object? sender, RoutedEventArgs e)
    {
        if (_updatingSelection || sender is not CheckBox group) return;

        var options = ReferenceEquals(group, ImagesCheckBox)
            ? _imageOptions
            : _videoOptions;
        var groupName = ReferenceEquals(group, ImagesCheckBox) ? "Images" : "Videos";

        _updatingSelection = true;
        try
        {
            var isSelected = group.IsChecked == true;
            foreach (var option in options)
                option.CheckBox.IsChecked = isSelected;
            UpdateGroupState(group, options, groupName);
        }
        finally
        {
            _updatingSelection = false;
        }
        UpdateActionAvailability();
    }

    private void OnExtensionSelectionChanged(object? sender, RoutedEventArgs e)
    {
        if (_updatingSelection) return;

        _updatingSelection = true;
        try
        {
            UpdateGroupState(ImagesCheckBox, _imageOptions, "Images");
            UpdateGroupState(VideosCheckBox, _videoOptions, "Videos");
        }
        finally
        {
            _updatingSelection = false;
        }
        UpdateActionAvailability();
    }

    private static void UpdateGroupState(
        CheckBox group,
        IReadOnlyCollection<(string Extension, CheckBox CheckBox)> options,
        string name)
    {
        var selectedCount = options.Count(option => option.CheckBox.IsChecked == true);
        group.IsChecked = selectedCount switch
        {
            0 => false,
            var count when count == options.Count => true,
            _ => null
        };
        group.Content = $"{name} ({selectedCount}/{options.Count})";
    }

    private static string DescribeExtensions(IReadOnlyCollection<string> extensions)
    {
        var selected = new HashSet<string>(extensions, StringComparer.OrdinalIgnoreCase);
        var imageCount = WindowsFileRegistration.ImageExtensions.Count(selected.Contains);
        var videoCount = WindowsFileRegistration.VideoExtensions.Count(selected.Contains);
        var descriptions = new List<string>(2);

        if (imageCount > 0)
            descriptions.Add(DescribeGroup("image", imageCount, WindowsFileRegistration.ImageAssociationCount));
        if (videoCount > 0)
            descriptions.Add(DescribeGroup("video", videoCount, WindowsFileRegistration.VideoAssociationCount));
        return descriptions.Count == 0 ? "no media formats" : string.Join(" and ", descriptions);
    }

    private static string DescribeGroup(string name, int selected, int total) =>
        selected == total
            ? $"all {total} {name} formats"
            : $"{selected} of {total} {name} formats";
}
