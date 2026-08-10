using System;
using System.Buffers;
using System.IO;
using System.Numerics;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using ImageMagick;

namespace ImageViewer.Services;

public sealed class DuplicateImageHasher
{
    public const int PerceptualHashVersion = 2;
    internal const int ColorSignatureLength = 4 * 4 * 3;
    private const int MaximumAverageColorDifference = 32;
    private const double MaximumAspectRatioScale = 1.15;

    public async Task<string> ComputeContentHashAsync(
        string path,
        CancellationToken cancellationToken)
    {
        await using var stream = new FileStream(
            path, FileMode.Open, FileAccess.Read,
            FileShare.Read | FileShare.Delete,
            1024 * 1024,
            FileOptions.Asynchronous | FileOptions.SequentialScan);
        var hash = await SHA256.HashDataAsync(stream, cancellationToken).ConfigureAwait(false);
        return Convert.ToHexString(hash);
    }

    public Task<PerceptualHashResult> ComputePerceptualHashAsync(
        string path,
        CancellationToken cancellationToken) => Task.Run(() =>
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var image = new MagickImage(path);
        image.AutoOrient();
        var width = checked((int)image.Width);
        var height = checked((int)image.Height);
        image.BackgroundColor = MagickColors.White;
        image.Alpha(AlphaOption.Remove);

        using var colorImage = image.Clone();
        colorImage.ColorSpace = ColorSpace.sRGB;
        colorImage.Depth = 8;
        colorImage.Resize(new MagickGeometry(4, 4) { IgnoreAspectRatio = true });
        var colorSignature = colorImage.ToByteArray(MagickFormat.Rgb);
        if (colorSignature.Length != ColorSignatureLength)
            throw new InvalidDataException("Could not create a perceptual color signature.");

        image.ColorSpace = ColorSpace.Gray;
        image.Depth = 8;
        image.Resize(new MagickGeometry(9, 9) { IgnoreAspectRatio = true });
        var pixels = image.ToByteArray(MagickFormat.Gray);
        if (pixels.Length < 81)
            throw new InvalidDataException("Could not create a perceptual image hash.");

        ulong horizontalHash = 0;
        ulong verticalHash = 0;
        for (var row = 0; row < 8; row++)
        {
            var rowOffset = row * 9;
            for (var column = 0; column < 8; column++)
            {
                var offset = rowOffset + column;
                horizontalHash <<= 1;
                if (pixels[rowOffset + column] > pixels[rowOffset + column + 1])
                    horizontalHash |= 1;

                verticalHash <<= 1;
                if (pixels[offset] > pixels[offset + 9])
                    verticalHash |= 1;
            }
        }
        return new PerceptualHashResult(
            horizontalHash,
            verticalHash,
            colorSignature,
            width,
            height);
    }, cancellationToken);

    internal static bool TryRestorePerceptualHash(
        DuplicateHashCacheEntry entry,
        out PerceptualHashResult result)
    {
        if (entry.PerceptualHashVersion == PerceptualHashVersion &&
            entry.PerceptualHash is { } horizontalHash &&
            entry.VerticalPerceptualHash is { } verticalHash &&
            entry.ColorSignature is { Length: ColorSignatureLength } colorSignature &&
            entry.Width > 0 && entry.Height > 0)
        {
            result = new PerceptualHashResult(
                horizontalHash,
                verticalHash,
                colorSignature,
                entry.Width,
                entry.Height);
            return true;
        }

        result = PerceptualHashResult.Empty;
        return false;
    }

    internal static bool AreVisuallyCompatible(
        PerceptualHashResult left,
        PerceptualHashResult right,
        int threshold) =>
        StructuralDistance(left, right) <= threshold &&
        AspectRatiosAreCompatible(left, right) &&
        AverageColorDistance(left.ColorSignature, right.ColorSignature) <=
            MaximumAverageColorDifference;

    internal static int StructuralDistance(
        PerceptualHashResult left,
        PerceptualHashResult right) => Math.Max(
        Distance(left.HorizontalHash, right.HorizontalHash),
        Distance(left.VerticalHash, right.VerticalHash));

    internal static int AverageColorDistance(byte[] left, byte[] right)
    {
        if (left.Length != ColorSignatureLength || right.Length != ColorSignatureLength)
            return int.MaxValue;

        var total = 0;
        for (var index = 0; index < ColorSignatureLength; index++)
            total += Math.Abs(left[index] - right[index]);
        return (int)Math.Ceiling(total / (double)ColorSignatureLength);
    }

    private static bool AspectRatiosAreCompatible(
        PerceptualHashResult left,
        PerceptualHashResult right)
    {
        if (left.Width <= 0 || left.Height <= 0 || right.Width <= 0 || right.Height <= 0)
            return false;

        var leftRatio = left.Width / (double)left.Height;
        var rightRatio = right.Width / (double)right.Height;
        var scale = Math.Max(leftRatio / rightRatio, rightRatio / leftRatio);
        return scale <= MaximumAspectRatioScale;
    }

    public async Task<bool> FilesAreEqualAsync(
        string leftPath,
        string rightPath,
        CancellationToken cancellationToken)
    {
        const int bufferSize = 1024 * 1024;
        var leftBuffer = ArrayPool<byte>.Shared.Rent(bufferSize);
        var rightBuffer = ArrayPool<byte>.Shared.Rent(bufferSize);
        try
        {
            await using var left = OpenForComparison(leftPath, bufferSize);
            await using var right = OpenForComparison(rightPath, bufferSize);
            if (left.Length != right.Length) return false;

            while (true)
            {
                var leftCount = await left.ReadAsync(
                    leftBuffer.AsMemory(0, bufferSize), cancellationToken).ConfigureAwait(false);
                var rightCount = await right.ReadAsync(
                    rightBuffer.AsMemory(0, bufferSize), cancellationToken).ConfigureAwait(false);
                if (leftCount != rightCount) return false;
                if (leftCount == 0) return true;
                if (!leftBuffer.AsSpan(0, leftCount)
                    .SequenceEqual(rightBuffer.AsSpan(0, rightCount))) return false;
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(leftBuffer);
            ArrayPool<byte>.Shared.Return(rightBuffer);
        }
    }

    public static int Distance(ulong left, ulong right) =>
        BitOperations.PopCount(left ^ right);

    private static FileStream OpenForComparison(string path, int bufferSize) => new(
        path, FileMode.Open, FileAccess.Read,
        FileShare.Read | FileShare.Delete,
        bufferSize,
        FileOptions.Asynchronous | FileOptions.SequentialScan);
}

public readonly record struct PerceptualHashResult(
    ulong HorizontalHash,
    ulong VerticalHash,
    byte[] ColorSignature,
    int Width,
    int Height)
{
    internal static PerceptualHashResult Empty { get; } = new(
        0,
        0,
        Array.Empty<byte>(),
        0,
        0);
}
