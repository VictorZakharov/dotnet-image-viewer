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

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CompareBadgeText), nameof(ShowCompareBadge))]
    private int _compareRating;

    public bool ShowCompareBadge => IsFile
                                    && (CompareMark != ImageViewer.Models.CompareMark.Neutral
                                        || CompareRating > 0);
    public bool IsComparePick => CompareMark == ImageViewer.Models.CompareMark.Pick;
    public bool IsCompareReject => CompareMark == ImageViewer.Models.CompareMark.Reject;
    public string CompareBadgeText
    {
        get
        {
            var mark = CompareMark switch
            {
                ImageViewer.Models.CompareMark.Pick => "PICK",
                ImageViewer.Models.CompareMark.Reject => "REJECT",
                _ => ""
            };
            var rating = CompareRating > 0 ? $"★ {CompareRating}" : "";
            return string.IsNullOrEmpty(mark) ? rating
                : string.IsNullOrEmpty(rating) ? mark
                : $"{mark} · {rating}";
        }
    }
}
