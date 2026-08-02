using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;

namespace ImageViewer.ViewModels;

public partial class FolderTreeItem : ObservableObject
{
    public string Path { get; }
    public string Name { get; }
    public ObservableCollection<FolderTreeItem> Children { get; } = new();

    private bool _loaded;
    private Task? _loadTask;

    [ObservableProperty] private bool _isExpanded;

    public FolderTreeItem(
        string path,
        string name,
        bool addPlaceholder = true,
        bool probeForChildren = true)
    {
        Path = path;
        Name = name;

        // TreeView decides whether to render its expander from the item count.
        // Probe one level so leaf folders never receive a misleading chevron.
        if (addPlaceholder && (!probeForChildren || HasVisibleSubfolder(path)))
            Children.Add(new FolderTreeItem("", "", addPlaceholder: false));
    }

    partial void OnIsExpandedChanged(bool value)
    {
        if (value && !_loaded)
            _ = EnsureChildrenLoadedAsync();
    }

    public Task EnsureChildrenLoadedAsync()
    {
        if (_loaded || string.IsNullOrEmpty(Path))
            return Task.CompletedTask;

        return _loadTask ??= LoadChildrenAsync();
    }

    private async Task LoadChildrenAsync()
    {
        List<(string Path, string Name, bool HasChildren)> children;
        try
        {
            children = await Task.Run(() => Directory.EnumerateDirectories(Path)
                .Where(directory => !IsHidden(directory))
                .Select(directory => (
                    Path: directory,
                    Name: System.IO.Path.GetFileName(directory),
                    HasChildren: HasVisibleSubfolder(directory)))
                .OrderBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
                .ToList()).ConfigureAwait(false);
        }
        catch
        {
            children = new List<(string Path, string Name, bool HasChildren)>();
        }

        if (children.Count == 0)
        {
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                Children.Clear();
                _loaded = true;
            }, DispatcherPriority.Background);
            return;
        }

        const int batchSize = 32;
        for (var offset = 0; offset < children.Count; offset += batchSize)
        {
            var batchStart = offset;
            var batchEnd = Math.Min(offset + batchSize, children.Count);
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                if (batchStart == 0) Children.Clear();
                for (var index = batchStart; index < batchEnd; index++)
                {
                    var child = children[index];
                    Children.Add(new FolderTreeItem(
                        child.Path,
                        child.Name,
                        addPlaceholder: child.HasChildren,
                        probeForChildren: false));
                }
                if (batchEnd == children.Count) _loaded = true;
            }, DispatcherPriority.Background);
        }
    }

    private static bool HasVisibleSubfolder(string path)
    {
        if (string.IsNullOrEmpty(path)) return false;

        try
        {
            return Directory.EnumerateDirectories(path).Any(directory => !IsHidden(directory));
        }
        catch
        {
            return false;
        }
    }

    private static bool IsHidden(string path)
    {
        try
        {
            var attrs = File.GetAttributes(path);
            return (attrs & (FileAttributes.Hidden | FileAttributes.System)) != 0;
        }
        catch
        {
            return true;
        }
    }
}
