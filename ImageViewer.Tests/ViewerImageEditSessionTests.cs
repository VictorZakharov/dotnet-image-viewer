using ImageViewer.Models;
using ImageViewer.Services;
using ImageViewer.ViewModels;

namespace ImageViewer.Tests;

public sealed class ViewerImageEditSessionTests
{
    [Fact]
    public async Task FourQuarterTurnsReturnViewerToACleanSession()
    {
        using var folder = new BatchTestFolder();
        var source = folder.Image("photo.png", 100, 60);
        using var viewModel = new ViewerViewModel(new AppSettings());
        await viewModel.LoadAsync(source);

        for (var turn = 0; turn < 4; turn++)
            viewModel.RotateRightCommand.Execute(null);

        Assert.Equal(0, viewModel.Rotation);
        Assert.False(viewModel.HasPendingEdits);
        Assert.Empty(viewModel.PendingOperations);
    }

    [Fact]
    public async Task SavingCanvasOperationsCommitsTheirCombinedDimensions()
    {
        using var folder = new BatchTestFolder();
        var source = folder.Image("photo.png", 100, 60);
        var operations = new BatchProcessOperation[]
        {
            new()
            {
                Kind = BatchProcessOperationKind.Rotate,
                IsEnabled = true,
                RotationDegrees = 90
            },
            new()
            {
                Kind = BatchProcessOperationKind.Crop,
                IsEnabled = true,
                CropX = 5,
                CropY = 10,
                CropWidth = 40,
                CropHeight = 70
            }
        };

        await new SingleImageEditSessionService().SaveAsync(source, operations);

        Assert.Equal((40u, 70u), folder.Dimensions(source));
    }
}
