using ImageViewer.Models;
using ImageViewer.Services;

namespace ImageViewer.ViewModels;

public partial class BrowserViewModel
{
    public void ApplyImageEditResult(SingleImageEditResult result)
    {
        if (result.ReplacedOriginal
            && !FileSystemPath.Equals(result.SourcePath, result.OutputPath))
        {
            ApplyDeletedPaths([result.SourcePath]);
        }

        ApplyTransferredDestinations([result.OutputPath]);
        RestoreSelectionByPaths([result.OutputPath], result.OutputPath);
    }
}
