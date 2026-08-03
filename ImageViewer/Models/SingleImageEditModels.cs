using System.IO;

namespace ImageViewer.Models;

public enum SingleImageEditKind
{
    RotateLeft,
    RotateRight,
    Resize,
    Crop,
    Convert,
    Watermark,
    RemoveMetadata
}

public sealed record SingleImageEditResult(
    string SourcePath,
    string OutputPath,
    bool ReplacedOriginal,
    SingleImageEditKind Kind)
{
    public string OutputName => Path.GetFileName(OutputPath);
}

public static class SingleImageEditKindExtensions
{
    public static string DisplayName(this SingleImageEditKind kind) => kind switch
    {
        SingleImageEditKind.RotateLeft => "Rotate left",
        SingleImageEditKind.RotateRight => "Rotate right",
        SingleImageEditKind.Resize => "Resize",
        SingleImageEditKind.Crop => "Crop",
        SingleImageEditKind.Convert => "Convert format",
        SingleImageEditKind.Watermark => "Add watermark",
        _ => "Remove metadata"
    };

    public static string DefaultSuffix(this SingleImageEditKind kind) => kind switch
    {
        SingleImageEditKind.RotateLeft or SingleImageEditKind.RotateRight => "_rotated",
        SingleImageEditKind.Resize => "_resized",
        SingleImageEditKind.Crop => "_cropped",
        SingleImageEditKind.Convert => "_converted",
        SingleImageEditKind.Watermark => "_watermarked",
        _ => "_clean"
    };
}
