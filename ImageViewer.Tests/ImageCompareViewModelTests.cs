using ImageViewer.Models;
using ImageViewer.ViewModels;

namespace ImageViewer.Tests;

public sealed class ImageCompareViewModelTests
{
    [Fact]
    public void KeepActiveMarksOnePickAndAllOthersReject()
    {
        using var viewModel = new ImageCompareViewModel([
            "one.jpg", "two.jpg", "three.jpg"
        ]);
        viewModel.SetActive(viewModel.Candidates[1]);

        viewModel.KeepActiveRejectOthers();
        var result = viewModel.CreateResult();

        Assert.Equal("two.jpg", result.PickedPath);
        Assert.Equal(new[] { "one.jpg", "three.jpg" }, result.RejectedPaths);
        Assert.True(viewModel.HasRejected);
    }

    [Fact]
    public void MetadataDifferencesAreFlaggedAcrossCandidates()
    {
        using var viewModel = new ImageCompareViewModel(["one.jpg", "two.jpg"]);
        viewModel.Candidates[0].SetMetadata(new ImageMetadata
        {
            Width = 4000,
            Height = 3000,
            CameraModel = "Camera A"
        });
        viewModel.Candidates[1].SetMetadata(new ImageMetadata
        {
            Width = 2000,
            Height = 1500,
            CameraModel = "Camera A"
        });

        viewModel.RefreshDifferences();

        Assert.True(viewModel.Candidates[0].MetadataRows[0].IsDifferent);
        Assert.True(viewModel.Candidates[1].MetadataRows[0].IsDifferent);
        Assert.False(viewModel.Candidates[0].MetadataRows[3].IsDifferent);
    }

    [Fact]
    public void RemovedCandidatesAreTrackedAndLayoutReflows()
    {
        using var viewModel = new ImageCompareViewModel([
            "one.jpg", "two.jpg", "three.jpg", "four.jpg"
        ]);

        viewModel.RemoveDeleted(["three.jpg", "four.jpg"]);
        var result = viewModel.CreateResult();

        Assert.Equal(2, viewModel.Candidates.Count);
        Assert.Equal(1, viewModel.Rows);
        Assert.Equal(2, viewModel.Columns);
        Assert.Equal(new[] { "three.jpg", "four.jpg" }, result.DeletedPaths);
    }
}
