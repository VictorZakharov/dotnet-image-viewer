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
        image.ColorSpace = ColorSpace.Gray;
        image.Depth = 8;
        image.Resize(new MagickGeometry(9, 8) { IgnoreAspectRatio = true });
        image.Alpha(AlphaOption.Off);
        var pixels = image.ToByteArray(MagickFormat.Gray);
        if (pixels.Length < 72)
            throw new InvalidDataException("Could not create a perceptual image hash.");

        ulong hash = 0;
        for (var row = 0; row < 8; row++)
        {
            var rowOffset = row * 9;
            for (var column = 0; column < 8; column++)
            {
                hash <<= 1;
                if (pixels[rowOffset + column] > pixels[rowOffset + column + 1])
                    hash |= 1;
            }
        }
        return new PerceptualHashResult(hash, width, height);
    }, cancellationToken);

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

public readonly record struct PerceptualHashResult(ulong Hash, int Width, int Height);
