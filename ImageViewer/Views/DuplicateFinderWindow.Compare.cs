using System.Threading.Tasks;
using ImageViewer.ViewModels;

namespace ImageViewer.Views;

public partial class DuplicateFinderWindow
{
    public async Task CompareGroupAsync(DuplicateGroupViewModel group)
    {
        if (_viewModel.IsScanning) return;
        var paths = group.GetComparisonPaths();
        if (paths.Count < 2)
        {
            _viewModel.StatusText = "At least two readable images are required to compare.";
            return;
        }

        var compare = new ImageCompareWindow(paths);
        await compare.ShowDialog(this);
        if (compare.Result is not { } result) return;
        if (result.DeletedPaths.Count > 0)
        {
            _viewModel.RemoveDeletedPaths(result.DeletedPaths);
            return;
        }
        group.ApplyCompareResult(result);
        _viewModel.StatusText = result.PickedPath is null
            ? "Compare marks applied to this review session."
            : "Keeper choice applied; rejected candidates are selected for review.";
    }
}
