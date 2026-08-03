using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Input.Platform;
using Avalonia.Platform.Storage;
using ImageViewer.Models;

namespace ImageViewer.Views;

public partial class BrowserView
{
    private async Task CopySelectionToClipboardAsync(bool isCut)
    {
        if (_vm is not { } vm ||
            GetOwnerWindow() is not { Clipboard: { } clipboard }) return;
        var paths = vm.SelectedFilePaths;
        if (paths.Count == 0) return;
        var storageItems = await ResolveStorageFilesAsync(paths);
        if (storageItems.Count == 0)
        {
            vm.ReportFileOperation("No selected files are still available.");
            return;
        }

        var availablePaths = storageItems
            .Select(item => item.TryGetLocalPath())
            .Where(path => !string.IsNullOrEmpty(path))
            .Cast<string>()
            .ToList();
        await clipboard.SetFilesAsync(storageItems);
        _fileClipboard.Set(availablePaths, isCut);
        vm.ReportFileOperation(
            $"{availablePaths.Count} file{(availablePaths.Count == 1 ? "" : "s")} " +
            (isCut ? "ready to move" : "copied"));
    }

    private async Task PasteClipboardAsync()
    {
        if (_vm is not { CurrentFolder: { } destination } vm ||
            GetOwnerWindow() is not { Clipboard: { } clipboard }) return;
        var storageItems = await clipboard.TryGetFilesAsync();
        var paths = storageItems?
            .Select(item => item.TryGetLocalPath())
            .Where(path => !string.IsNullOrEmpty(path) && File.Exists(path))
            .Cast<string>()
            .Distinct(System.StringComparer.OrdinalIgnoreCase)
            .ToList() ?? new List<string>();
        if (paths.Count == 0)
        {
            vm.ReportFileOperation("The clipboard contains no files to paste.");
            return;
        }

        var isCut = _fileClipboard.IsCut && _fileClipboard.Matches(paths);
        await RunFileOperationAsync(
            new FileOperationRequest(
                isCut ? FileOperationKind.Move : FileOperationKind.Copy,
                paths,
                destination),
            recordMove: isCut,
            consumeCutClipboard: isCut);
    }

    private async Task RemoveSuccessfulCutFilesAsync(IEnumerable<string> successfulPaths)
    {
        if (GetOwnerWindow() is not { Clipboard: { } clipboard }) return;
        _fileClipboard.RemoveSuccessful(successfulPaths);
        if (_fileClipboard.Paths.Count == 0)
        {
            await clipboard.ClearAsync();
            return;
        }

        var remaining = await ResolveStorageFilesAsync(_fileClipboard.Paths);
        await clipboard.SetFilesAsync(remaining);
    }

    private async Task<List<IStorageItem>> ResolveStorageFilesAsync(IEnumerable<string> paths)
    {
        var result = new List<IStorageItem>();
        if (GetOwnerWindow() is not { } owner) return result;
        foreach (var path in paths)
        {
            var file = await owner.StorageProvider.TryGetFileFromPathAsync(path);
            if (file is not null) result.Add(file);
        }
        return result;
    }
}
