using CommunityToolkit.Mvvm.ComponentModel;

namespace ImageViewer.ViewModels;

public partial class CompareMetadataRow : ObservableObject
{
    public string Label { get; }
    public string Value { get; }

    [ObservableProperty] private bool _isDifferent;

    public CompareMetadataRow(string label, string value)
    {
        Label = label;
        Value = value;
    }
}
