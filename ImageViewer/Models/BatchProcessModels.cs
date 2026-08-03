using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace ImageViewer.Models;

public enum BatchOutputMode
{
    NewFolder,
    BesideOriginal,
    ReplaceOriginal
}

public enum BatchOverwritePolicy
{
    Skip,
    Replace,
    AutoRename
}

public enum BatchProcessOperationKind
{
    Resize,
    Convert,
    Rotate,
    Crop,
    Watermark,
    MetadataCleanup
}

public enum BatchResizeMode
{
    Fit,
    Exact
}

public enum BatchOutputFormat
{
    Keep,
    Jpeg,
    Png,
    WebP,
    Tiff
}

public enum BatchWatermarkPosition
{
    TopLeft,
    TopRight,
    Center,
    BottomLeft,
    BottomRight
}

public enum BatchMetadataCleanupMode
{
    RemoveExif,
    RemoveAll
}

public sealed class BatchProcessOperation
{
    public BatchProcessOperationKind Kind { get; set; }
    public bool IsEnabled { get; set; }
    public int ResizeWidth { get; set; } = 1920;
    public int ResizeHeight { get; set; } = 1080;
    public BatchResizeMode ResizeMode { get; set; }
    public bool AllowUpscale { get; set; }
    public BatchOutputFormat OutputFormat { get; set; } = BatchOutputFormat.Jpeg;
    public int RotationDegrees { get; set; } = 90;
    public bool LosslessJpeg { get; set; }
    public int CropX { get; set; }
    public int CropY { get; set; }
    public int CropWidth { get; set; } = 1000;
    public int CropHeight { get; set; } = 1000;
    public string WatermarkText { get; set; } = "";
    public BatchWatermarkPosition WatermarkPosition { get; set; } =
        BatchWatermarkPosition.BottomRight;
    public int WatermarkPointSize { get; set; } = 32;
    public int WatermarkOpacity { get; set; } = 70;
    public BatchMetadataCleanupMode MetadataCleanupMode { get; set; } =
        BatchMetadataCleanupMode.RemoveExif;

    [JsonIgnore]
    public string DisplayName => Kind switch
    {
        BatchProcessOperationKind.Resize => "Resize",
        BatchProcessOperationKind.Convert => "Format conversion",
        BatchProcessOperationKind.Rotate => "Rotate",
        BatchProcessOperationKind.Crop => "Crop",
        BatchProcessOperationKind.Watermark => "Watermark",
        _ => "Metadata cleanup"
    };

    public BatchProcessOperation Clone() => (BatchProcessOperation)MemberwiseClone();

    public static List<BatchProcessOperation> CreateDefaults() =>
    [
        new() { Kind = BatchProcessOperationKind.Resize },
        new() { Kind = BatchProcessOperationKind.Rotate },
        new() { Kind = BatchProcessOperationKind.Crop },
        new() { Kind = BatchProcessOperationKind.Watermark },
        new() { Kind = BatchProcessOperationKind.MetadataCleanup },
        new() { Kind = BatchProcessOperationKind.Convert }
    ];
}

public sealed record BatchProcessOptions(
    BatchOutputMode OutputMode,
    string DestinationFolder,
    string Suffix,
    BatchOverwritePolicy OverwritePolicy,
    int Quality,
    bool PreserveFileDates,
    bool PreserveIccProfile,
    int MaxConcurrency,
    IReadOnlyList<BatchProcessOperation> Operations);

public sealed class BatchProcessPreset
{
    public string Name { get; set; } = "";
    public BatchOutputMode OutputMode { get; set; } = BatchOutputMode.NewFolder;
    public string DestinationFolder { get; set; } = "";
    public string Suffix { get; set; } = "_processed";
    public BatchOverwritePolicy OverwritePolicy { get; set; }
    public int Quality { get; set; } = 90;
    public bool PreserveFileDates { get; set; } = true;
    public bool PreserveIccProfile { get; set; } = true;
    public int MaxConcurrency { get; set; } = Math.Clamp(Environment.ProcessorCount / 2, 1, 4);
    public List<BatchProcessOperation> Operations { get; set; } =
        BatchProcessOperation.CreateDefaults();

    public BatchProcessOptions ToOptions() => new(
        OutputMode,
        DestinationFolder,
        Suffix,
        OverwritePolicy,
        Quality,
        PreserveFileDates,
        PreserveIccProfile,
        MaxConcurrency,
        Operations.ConvertAll(operation => operation.Clone()));
}
