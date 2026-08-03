using System.Collections.Generic;
using System.Linq;
using ImageViewer.Models;

namespace ImageViewer.Services;

public sealed class FileOperationJournal
{
    private IReadOnlyList<FileTransferPair> _undoTransfers = System.Array.Empty<FileTransferPair>();

    public bool CanUndo => _undoTransfers.Count > 0;
    public string Description { get; private set; } = "";

    public void RecordMove(FileOperationResult result)
    {
        _undoTransfers = result.Successful
            .Where(item => !string.IsNullOrEmpty(item.DestinationPath))
            .Select(item => new FileTransferPair(item.DestinationPath!, item.SourcePath))
            .ToList();
        Description = _undoTransfers.Count == 1
            ? "Undo move"
            : $"Undo {_undoTransfers.Count} moves";
    }

    public void RecordRename(string oldPath, string newPath)
    {
        _undoTransfers = new[] { new FileTransferPair(newPath, oldPath) };
        Description = "Undo rename";
    }

    public IReadOnlyList<FileTransferPair> TakeUndoPlan()
    {
        var plan = _undoTransfers;
        Clear();
        return plan;
    }

    public void Clear()
    {
        _undoTransfers = System.Array.Empty<FileTransferPair>();
        Description = "";
    }
}
