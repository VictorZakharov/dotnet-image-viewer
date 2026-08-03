using CommunityToolkit.Mvvm.ComponentModel;
using ImageViewer.Models;

namespace ImageViewer.ViewModels;

public partial class ThumbnailItem
{
    [ObservableProperty]
    [NotifyPropertyChangedFor(
        nameof(CompareBadgeText),
        nameof(ShowCompareBadge),
        nameof(IsComparePick),
        nameof(IsCompareReject))]
    private CompareMark _compareMark;

    public bool ShowCompareBadge => IsFile
                                    && CompareMark != ImageViewer.Models.CompareMark.Neutral;
    public bool IsComparePick => CompareMark == ImageViewer.Models.CompareMark.Pick;
    public bool IsCompareReject => CompareMark == ImageViewer.Models.CompareMark.Reject;
    public string CompareBadgeText => CompareMark switch
    {
        ImageViewer.Models.CompareMark.Pick => "PICK",
        ImageViewer.Models.CompareMark.Reject => "REJECT",
        _ => ""
    };
}
