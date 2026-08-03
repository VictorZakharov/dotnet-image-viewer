using System;
using System.Collections.Generic;
using System.Linq;

namespace ImageViewer.Services;

public sealed class FileClipboardState
{
    private readonly HashSet<string> _paths = new(StringComparer.OrdinalIgnoreCase);

    public bool IsCut { get; private set; }
    public IReadOnlyCollection<string> Paths => _paths;

    public void Set(IEnumerable<string> paths, bool isCut)
    {
        _paths.Clear();
        _paths.UnionWith(paths);
        IsCut = isCut;
    }

    public bool Matches(IEnumerable<string> paths) => _paths.SetEquals(paths);

    public void RemoveSuccessful(IEnumerable<string> paths)
    {
        _paths.ExceptWith(paths);
        if (_paths.Count == 0) Clear();
    }

    public void Clear()
    {
        _paths.Clear();
        IsCut = false;
    }
}
