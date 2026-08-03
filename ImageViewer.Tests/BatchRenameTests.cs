using ImageViewer.Models;
using ImageViewer.Services;

namespace ImageViewer.Tests;

public sealed class BatchRenameTests
{
    [Fact]
    public void TemplateCombinesOriginalCounterSearchAndCase()
    {
        using var folder = new BatchTestFolder();
        var source = folder.Image("Holiday Photo.jpg");
        var options = new BatchRenameOptions(
            "{name}-{counter}",
            "photo",
            "image",
            MatchCase: false,
            BatchNameCase.Uppercase,
            CounterStart: 7,
            CounterPadding: 4);

        var result = BatchRenameTemplate.Expand(source, 7, options);

        Assert.Equal("HOLIDAY IMAGE-0007", result);
    }

    [Fact]
    public void UnknownTemplateTokenIsRejected()
    {
        using var folder = new BatchTestFolder();
        var source = folder.File("note.txt", "text");
        var options = Options("{mystery}");

        var error = Assert.Throws<FormatException>(() =>
            BatchRenameTemplate.Expand(source, 1, options));

        Assert.Contains("Unknown rename token", error.Message);
    }

    [Fact]
    public void TemplateReadsDateTakenAndCameraMetadata()
    {
        using var folder = new BatchTestFolder();
        var source = folder.ImageWithMetadata("source.jpg");
        var options = Options("{taken:yyyyMMdd}-{make}-{model}-{lens}");

        var result = BatchRenameTemplate.Expand(source, 1, options);

        Assert.Equal("20240506-Test Camera-Model One-Prime Lens", result);
    }

    [Fact]
    public void PreviewFlagsDuplicateAndExistingOutputs()
    {
        using var folder = new BatchTestFolder();
        var first = folder.Image("first.jpg");
        var second = folder.Image("second.jpg");
        var service = new BatchRenameService();

        var duplicates = service.BuildPreview([first, second], Options("same"));
        Assert.All(duplicates, item => Assert.Equal(BatchPreviewStatus.Collision, item.Status));

        folder.Image("occupied.jpg");
        var existing = service.BuildPreview([first], Options("occupied"));
        Assert.Equal(BatchPreviewStatus.Collision, Assert.Single(existing).Status);
    }

    [Fact]
    public async Task ExecutionSafelyHandlesANameSwap()
    {
        using var folder = new BatchTestFolder();
        var first = folder.File("a.txt", "A");
        var second = folder.File("b.txt", "B");
        var preview = new[]
        {
            new BatchPreviewItem(first, second, BatchPreviewStatus.Ready, "Ready"),
            new BatchPreviewItem(second, first, BatchPreviewStatus.Ready, "Ready")
        };

        var result = await new BatchRenameService().ExecuteAsync(
            preview,
            progress: null,
            CancellationToken.None);

        Assert.Equal(2, result.Successful.Count);
        Assert.Equal("B", File.ReadAllText(first));
        Assert.Equal("A", File.ReadAllText(second));
        Assert.Empty(Directory.GetFiles(folder.Root, ".imageviewer-rename-*"));
    }

    private static BatchRenameOptions Options(string template) => new(
        template,
        "",
        "",
        MatchCase: false,
        BatchNameCase.Unchanged,
        CounterStart: 1,
        CounterPadding: 3);
}
