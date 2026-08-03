using ImageMagick;

namespace ImageViewer.Tests;

internal sealed class BatchTestFolder : IDisposable
{
    public string Root { get; } = Path.Combine(
        Path.GetTempPath(),
        $"ImageViewer.Batch.{Guid.NewGuid():N}");

    public BatchTestFolder() => Directory.CreateDirectory(Root);

    public string Folder(string name)
    {
        var path = Path.Combine(Root, name);
        Directory.CreateDirectory(path);
        return path;
    }

    public string File(string name, string content)
    {
        var path = Path.Combine(Root, name);
        System.IO.File.WriteAllText(path, content);
        return path;
    }

    public string Image(string name, uint width = 100, uint height = 50)
    {
        var path = Path.Combine(Root, name);
        using var image = new MagickImage(MagickColors.CornflowerBlue, width, height);
        image.Write(path);
        return path;
    }

    public string ImageWithMetadata(string name, ushort orientation = 1)
    {
        var path = Path.Combine(Root, name);
        using var image = new MagickImage(MagickColors.CornflowerBlue, 100, 50);
        var exif = new ExifProfile();
        exif.SetValue(ExifTag.Make, "Test Camera");
        exif.SetValue(ExifTag.Model, "Model One");
        exif.SetValue(ExifTag.LensModel, "Prime Lens");
        exif.SetValue(ExifTag.DateTimeOriginal, "2024:05:06 07:08:09");
        exif.SetValue(ExifTag.Orientation, orientation);
        image.SetProfile(exif);
        image.Orientation = orientation switch
        {
            3 => OrientationType.BottomRight,
            6 => OrientationType.RightTop,
            8 => OrientationType.LeftBottom,
            _ => OrientationType.TopLeft
        };
        image.SetProfile(ColorProfiles.SRGB);
        image.Write(path);
        return path;
    }

    public (uint Width, uint Height) Dimensions(string path)
    {
        var info = new MagickImageInfo(path);
        return (info.Width, info.Height);
    }

    public void Dispose()
    {
        if (Directory.Exists(Root)) Directory.Delete(Root, recursive: true);
    }
}
