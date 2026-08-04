namespace ImageViewer.Models;

public sealed record ConversionPreview(
    byte[] EncodedBytes,
    long SourceSizeBytes,
    BatchOutputFormat Format,
    int Quality)
{
    public long ConvertedSizeBytes => EncodedBytes.LongLength;
}
