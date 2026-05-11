using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;

namespace ImageViewer.ViewModels;

public partial class FolderTreeItem : ObservableObject
{
    public string Path { get; }
    public string Name { get; }
    public ObservableCollection<FolderTreeItem> Children { get; } = new();

    private bool _loaded;

    [ObservableProperty] private bool _isExpanded;

    public FolderTreeItem(string path, string name, bool addPlaceholder = true)
    {
        Path = path;
        Name = name;
        if (addPlaceholder)
            Children.Add(new FolderTreeItem("", "", addPlaceholder: false));
    }

    partial void OnIsExpandedChanged(bool value)
    {
        if (value && !_loaded)
            LoadChildren();
    }

    private void LoadChildren()
    {
        _loaded = true;
        Children.Clear();
        if (string.IsNullOrEmpty(Path)) return;

        try
        {
            var dirs = Directory.EnumerateDirectories(Path)
                .Where(d => !IsHidden(d))
                .OrderBy(d => System.IO.Path.GetFileName(d), System.StringComparer.OrdinalIgnoreCase);
            foreach (var dir in dirs)
                Children.Add(new FolderTreeItem(dir, System.IO.Path.GetFileName(dir)));
        }
        catch
        {
            // Inaccessible folder — leave empty.
        }
    }

    private static bool IsHidden(string path)
    {
        try
        {
            var attrs = File.GetAttributes(path);
            return (attrs & FileAttributes.Hidden) != 0 || (attrs & FileAttributes.System) != 0;
        }
        catch { return true; }
    }
}
