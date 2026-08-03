using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ImageViewer.Models;
using ImageViewer.Services;

namespace ImageViewer.ViewModels;

public partial class ViewerViewModel
{
    private readonly List<BatchProcessOperation> _pendingOperations = [];
    private readonly SingleImageEditSessionService _editSession = new();
    private CancellationTokenSource? _editPreviewCancellation;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowImageTools))]
    private bool _isCropping;

    [ObservableProperty]
    private bool _hasPendingEdits;

    public IReadOnlyList<BatchProcessOperation> PendingOperations => _pendingOperations;

    [RelayCommand]
    private void RotateLeft() => AddPendingRotation(270);

    [RelayCommand]
    private void RotateRight() => AddPendingRotation(90);

    public bool BeginCrop()
    {
        if (IsVideo || IsImageLoading || Bitmap is null) return false;
        StopSlideshow();
        IsCropping = true;
        return true;
    }

    public void CancelCrop() => IsCropping = false;

    public async Task ApplyCropAsync(Rect selection)
    {
        if (!IsCropping || FilePath is null) return;
        var operation = new BatchProcessOperation
        {
            Kind = BatchProcessOperationKind.Crop,
            IsEnabled = true,
            CropX = (int)Math.Round(selection.X),
            CropY = (int)Math.Round(selection.Y),
            CropWidth = Math.Max(1, (int)Math.Round(selection.Width)),
            CropHeight = Math.Max(1, (int)Math.Round(selection.Height))
        };
        _pendingOperations.Add(operation);
        IsCropping = false;
        HasPendingEdits = true;
        UpdateStatus();

        var path = FilePath;
        var operations = ClonePendingOperations();
        var cancellation = ReplaceEditPreviewCancellation();
        IsImageLoading = true;
        try
        {
            var preview = await ImageEditPreviewRenderer.RenderAsync(
                path, operations, cancellation.Token);
            if (!ReferenceEquals(_editPreviewCancellation, cancellation)
                || !FileSystemPath.Equals(FilePath, path))
            {
                preview.Dispose();
                return;
            }

            ReplaceBitmap(preview);
            Rotation = 0;
            UpdateStatus();
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            if (ReferenceEquals(_editPreviewCancellation, cancellation))
            {
                _pendingOperations.Remove(operation);
                HasPendingEdits = _pendingOperations.Count > 0;
                StatusText = $"Crop failed: {ex.Message}";
            }
        }
        finally
        {
            if (ReferenceEquals(_editPreviewCancellation, cancellation))
            {
                _editPreviewCancellation = null;
                IsImageLoading = false;
                cancellation.Dispose();
            }
        }
    }

    public async Task<bool> SavePendingEditsAsync()
    {
        if (!HasPendingEdits || FilePath is null) return true;
        CancelEditPreview();
        try
        {
            StatusText = "Saving image edits...";
            await _editSession.SaveAsync(FilePath, ClonePendingOperations());
            ClearPendingEditState();
            return true;
        }
        catch (Exception ex)
        {
            StatusText = $"Could not save edits: {ex.Message}";
            return false;
        }
    }

    public void DiscardPendingEdits()
    {
        CancelEditPreview();
        ClearPendingEditState();
    }

    internal void ResetEditSession()
    {
        CancelEditPreview();
        ClearPendingEditState();
    }

    internal void ReplaceBitmap(Bitmap? bitmap)
    {
        var previous = Bitmap;
        Bitmap = bitmap;
        if (!ReferenceEquals(previous, bitmap)) previous?.Dispose();
    }

    private void AddPendingRotation(int degrees)
    {
        if (IsVideo || IsCropping || IsImageLoading || Bitmap is null) return;
        StopSlideshow();
        Rotation = (Rotation + degrees) % 360;
        if (_pendingOperations.Count > 0
            && _pendingOperations[^1].Kind == BatchProcessOperationKind.Rotate)
        {
            var combined = (_pendingOperations[^1].RotationDegrees + degrees) % 360;
            if (combined == 0) _pendingOperations.RemoveAt(_pendingOperations.Count - 1);
            else _pendingOperations[^1].RotationDegrees = combined;
        }
        else
        {
            _pendingOperations.Add(new BatchProcessOperation
            {
                Kind = BatchProcessOperationKind.Rotate,
                IsEnabled = true,
                RotationDegrees = degrees
            });
        }

        HasPendingEdits = _pendingOperations.Count > 0;
        UpdateStatus();
    }

    private List<BatchProcessOperation> ClonePendingOperations() =>
        _pendingOperations.ConvertAll(operation => operation.Clone());

    private CancellationTokenSource ReplaceEditPreviewCancellation()
    {
        _editPreviewCancellation?.Cancel();
        _editPreviewCancellation?.Dispose();
        var cancellation = new CancellationTokenSource();
        _editPreviewCancellation = cancellation;
        return cancellation;
    }

    private void CancelEditPreview()
    {
        var cancellation = _editPreviewCancellation;
        _editPreviewCancellation = null;
        if (cancellation is not null)
        {
            cancellation.Cancel();
            cancellation.Dispose();
        }
        IsImageLoading = false;
    }

    private void ClearPendingEditState()
    {
        _pendingOperations.Clear();
        HasPendingEdits = false;
        IsCropping = false;
        UpdateStatus();
    }
}
