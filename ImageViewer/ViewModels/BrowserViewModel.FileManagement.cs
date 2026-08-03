using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ImageViewer.Models;

namespace ImageViewer.ViewModels;

public partial class BrowserViewModel
{
    [ObservableProperty] private string _fileOperationStatus = "";
    [ObservableProperty] private bool _canUndoFileOperation;
    [ObservableProperty] private string _undoFileOperationLabel = "Undo";

    public event Action<BrowserFileCommand>? FileCommandRequested;
    public event Action<string, string>? RenameCompleted;

    public IReadOnlyList<string> SelectedFilePaths =>
        SelectedFiles.Select(item => item.Path).ToList();

    [RelayCommand]
    private void CopyFiles() => RequestFileCommand(BrowserFileCommand.Copy, needsSelection: true);

    [RelayCommand]
    private void CutFiles() => RequestFileCommand(BrowserFileCommand.Cut, needsSelection: true);

    [RelayCommand]
    private void PasteFiles() => RequestFileCommand(BrowserFileCommand.Paste, needsSelection: false);

    [RelayCommand]
    private void MoveFiles() => RequestFileCommand(BrowserFileCommand.Move, needsSelection: true);

    [RelayCommand]
    private void DeleteSelected() => RequestFileCommand(BrowserFileCommand.Delete, needsSelection: true);

    [RelayCommand]
    private void UndoFileOperation()
    {
        if (CanUndoFileOperation) FileCommandRequested?.Invoke(BrowserFileCommand.Undo);
    }

    public void SetUndoFileOperation(bool canUndo, string label = "Undo")
    {
        CanUndoFileOperation = canUndo;
        UndoFileOperationLabel = string.IsNullOrEmpty(label) ? "Undo" : label;
    }

    public void ReportFileOperation(string status) => FileOperationStatus = status;

    public Task ReloadCurrentFolderAsync() =>
        string.IsNullOrEmpty(CurrentFolder)
            ? Task.CompletedTask
            : LoadFolderCoreAsync(CurrentFolder, force: true);

    private void RequestFileCommand(BrowserFileCommand command, bool needsSelection)
    {
        if (needsSelection && !HasSelectedFiles) return;
        FileCommandRequested?.Invoke(command);
    }

    private void ReportRename(string oldPath, string? newPath)
    {
        if (!string.IsNullOrEmpty(newPath)) RenameCompleted?.Invoke(oldPath, newPath);
    }
}
