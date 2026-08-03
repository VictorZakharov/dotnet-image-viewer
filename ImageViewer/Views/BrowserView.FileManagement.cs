using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using ImageViewer.Models;
using ImageViewer.Services;
using ImageViewer.ViewModels;

namespace ImageViewer.Views;

public partial class BrowserView
{
    private readonly BulkFileOperationService _bulkFileOperations = new();
    private readonly FileClipboardState _fileClipboard = new();
    private readonly FileOperationJournal _fileOperationJournal = new();
    private bool _fileOperationRunning;

    private void AttachFileManagement(BrowserViewModel vm)
    {
        vm.FileCommandRequested += OnFileCommandRequested;
        vm.RenameCompleted += OnRenameCompleted;
    }

    private void DetachFileManagement(BrowserViewModel vm)
    {
        vm.FileCommandRequested -= OnFileCommandRequested;
        vm.RenameCompleted -= OnRenameCompleted;
    }

    private async void OnFileCommandRequested(BrowserFileCommand command)
    {
        if (_vm is null || _fileOperationRunning) return;
        try
        {
            switch (command)
            {
                case BrowserFileCommand.Copy:
                    await CopySelectionToClipboardAsync(isCut: false);
                    break;
                case BrowserFileCommand.Cut:
                    await CopySelectionToClipboardAsync(isCut: true);
                    break;
                case BrowserFileCommand.Paste:
                    await PasteClipboardAsync();
                    break;
                case BrowserFileCommand.Move:
                    await MoveSelectionAsync();
                    break;
                case BrowserFileCommand.Delete:
                    await DeleteSelectionAsync();
                    break;
                case BrowserFileCommand.Undo:
                    await UndoLastFileOperationAsync();
                    break;
            }
        }
        catch (Exception ex)
        {
            _vm?.ReportFileOperation($"File operation could not start: {ex.Message}");
        }
    }

    private async Task MoveSelectionAsync()
    {
        if (_vm is not { } vm || GetOwnerWindow() is not { } owner) return;
        var paths = vm.SelectedPaths;
        if (paths.Count == 0) return;
        var folders = await owner.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = $"Move {paths.Count} selected item{(paths.Count == 1 ? "" : "s")} to...",
            AllowMultiple = false
        });
        var destination = folders.FirstOrDefault()?.TryGetLocalPath();
        if (string.IsNullOrEmpty(destination)) return;
        await RunFileOperationAsync(new FileOperationRequest(
            FileOperationKind.Move,
            paths,
            destination),
            recordMove: true);
    }

    private async Task DeleteSelectionAsync()
    {
        if (_vm is not { } vm || GetOwnerWindow() is not { } owner) return;
        var paths = vm.SelectedPaths;
        if (paths.Count == 0 || !await FileDeleteConfirmationDialog.ConfirmAsync(owner, paths)) return;
        await RunFileOperationAsync(new FileOperationRequest(
            FileOperationKind.Delete,
            paths));
    }

    private Task MoveFilesToFolderAsync(IReadOnlyList<string> paths, string destination) =>
        RunFileOperationAsync(new FileOperationRequest(
            FileOperationKind.Move,
            paths,
            destination),
            recordMove: true);

    private async Task UndoLastFileOperationAsync()
    {
        if (!_fileOperationJournal.CanUndo) return;
        var plan = _fileOperationJournal.TakeUndoPlan();
        UpdateUndoState();
        await RunFileOperationAsync(plan);
    }

    private async Task RunFileOperationAsync(
        FileOperationRequest request,
        bool recordMove = false,
        bool consumeCutClipboard = false)
    {
        await RunFileOperationCoreAsync(
            (resolver, progress, token) => _bulkFileOperations.ExecuteAsync(
                request, resolver, progress, token),
            request.Kind,
            recordMove,
            consumeCutClipboard);
    }

    private async Task RunFileOperationAsync(IReadOnlyList<FileTransferPair> undoPlan)
    {
        await RunFileOperationCoreAsync(
            (resolver, progress, token) => _bulkFileOperations.UndoMovesAsync(
                undoPlan, resolver, progress, token),
            FileOperationKind.Move,
            recordMove: false,
            consumeCutClipboard: false);
    }

    private async Task RunFileOperationCoreAsync(
        Func<Func<FileCollision, CancellationToken, Task<FileCollisionChoice>>,
            IProgress<FileOperationProgress>, CancellationToken, Task<FileOperationResult>> run,
        FileOperationKind kind,
        bool recordMove,
        bool consumeCutClipboard)
    {
        if (_vm is not { } vm || GetOwnerWindow() is not { } owner || _fileOperationRunning) return;
        _fileOperationRunning = true;
        var progressDialog = new FileOperationProgressDialog(GetOperationHeading(kind));
        FileCollisionChoice? remainingChoice = null;
        progressDialog.Show(owner);
        FileOperationResult? result = null;
        try
        {
            async Task<FileCollisionChoice> ResolveCollision(
                FileCollision collision,
                CancellationToken token)
            {
                token.ThrowIfCancellationRequested();
                if (remainingChoice is { } choice) return choice;
                var decision = await FileCollisionDialog.ShowAsync(owner, collision);
                if (decision.ApplyToRemaining && decision.Choice != FileCollisionChoice.Cancel)
                    remainingChoice = decision.Choice;
                return decision.Choice;
            }

            var progress = new Progress<FileOperationProgress>(progressDialog.Report);
            result = await run(ResolveCollision, progress, progressDialog.CancellationToken);
        }
        catch (OperationCanceledException)
        {
            result = new FileOperationResult(
                kind,
                Array.Empty<FileOperationSuccess>(),
                Array.Empty<string>(),
                Array.Empty<FileOperationFailure>(),
                IsCanceled: true);
        }
        catch (Exception ex)
        {
            result = new FileOperationResult(
                kind,
                Array.Empty<FileOperationSuccess>(),
                Array.Empty<string>(),
                new[] { new FileOperationFailure("File operation", null, ex.Message) },
                IsCanceled: false);
        }
        finally
        {
            progressDialog.Finish();
            _fileOperationRunning = false;
        }

        if (result is null) return;
        if (recordMove && result.Successful.Count > 0)
        {
            _fileOperationJournal.RecordMove(result);
            UpdateUndoState();
        }
        if (consumeCutClipboard)
            await RemoveSuccessfulCutFilesAsync(result.Successful.Select(item => item.SourcePath));

        vm.ApplyFileOperationChanges(result);
        vm.ReportFileOperation(FormatOperationStatus(result));
        if (result.Failures.Count > 0 || result.SkippedPaths.Count > 0 || result.IsCanceled)
            await new FileOperationResultDialog(result).ShowDialog(owner);
    }

    private void OnRenameCompleted(string oldPath, string newPath)
    {
        _fileOperationJournal.RecordRename(oldPath, newPath);
        UpdateUndoState();
    }

    private void UpdateUndoState() => _vm?.SetUndoFileOperation(
        _fileOperationJournal.CanUndo,
        _fileOperationJournal.Description);

    private Window? GetOwnerWindow() => TopLevel.GetTopLevel(this) as Window;

    private static string GetOperationHeading(FileOperationKind kind) => kind switch
    {
        FileOperationKind.Copy => "Copying items",
        FileOperationKind.Move => "Moving items",
        _ => $"Moving items to the {FileOperations.TrashDisplayName}"
    };

    private static string FormatOperationStatus(FileOperationResult result) =>
        $"{result.Successful.Count} succeeded · {result.SkippedPaths.Count} skipped · " +
        $"{result.Failures.Count} failed" + (result.IsCanceled ? " · canceled" : "");

    private void OnCopyFilesClicked(object? sender, RoutedEventArgs e) =>
        _vm?.CopyFilesCommand.Execute(null);
    private void OnCutFilesClicked(object? sender, RoutedEventArgs e) =>
        _vm?.CutFilesCommand.Execute(null);
    private void OnMoveFilesClicked(object? sender, RoutedEventArgs e) =>
        _vm?.MoveFilesCommand.Execute(null);
    private void OnDeleteFilesClicked(object? sender, RoutedEventArgs e) =>
        _vm?.DeleteSelectedCommand.Execute(null);
}
