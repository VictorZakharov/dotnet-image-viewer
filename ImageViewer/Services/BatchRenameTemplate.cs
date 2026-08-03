using System;
using System.Globalization;
using System.IO;
using System.Text;
using ImageViewer.Models;

namespace ImageViewer.Services;

public static class BatchRenameTemplate
{
    public static string Expand(
        string sourcePath,
        int counter,
        BatchRenameOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.Template))
            throw new FormatException("The rename template is empty.");

        var context = RenameContext.Create(
            sourcePath,
            counter,
            options.CounterPadding,
            NeedsImageMetadata(options.Template));
        var result = ExpandTokens(options.Template, context);
        if (!string.IsNullOrEmpty(options.SearchText))
        {
            result = result.Replace(
                options.SearchText,
                options.ReplaceText ?? "",
                options.MatchCase
                    ? StringComparison.CurrentCulture
                    : StringComparison.CurrentCultureIgnoreCase);
        }

        return options.CaseMode switch
        {
            BatchNameCase.Lowercase => result.ToLower(CultureInfo.CurrentCulture),
            BatchNameCase.Uppercase => result.ToUpper(CultureInfo.CurrentCulture),
            BatchNameCase.TitleCase => CultureInfo.CurrentCulture.TextInfo.ToTitleCase(
                result.ToLower(CultureInfo.CurrentCulture)),
            _ => result
        };
    }

    private static string ExpandTokens(string template, RenameContext context)
    {
        var output = new StringBuilder(template.Length + 16);
        for (var index = 0; index < template.Length; index++)
        {
            var current = template[index];
            if (current == '{' && index + 1 < template.Length && template[index + 1] == '{')
            {
                output.Append('{');
                index++;
                continue;
            }
            if (current == '}' && index + 1 < template.Length && template[index + 1] == '}')
            {
                output.Append('}');
                index++;
                continue;
            }
            if (current == '}') throw new FormatException("The template contains an unmatched '}'.");
            if (current != '{')
            {
                output.Append(current);
                continue;
            }

            var end = template.IndexOf('}', index + 1);
            if (end < 0) throw new FormatException("The template contains an unmatched '{'.");
            output.Append(ResolveToken(template[(index + 1)..end], context));
            index = end;
        }
        return output.ToString();
    }

    private static string ResolveToken(string expression, RenameContext context)
    {
        var separator = expression.IndexOf(':');
        var name = (separator < 0 ? expression : expression[..separator]).Trim().ToLowerInvariant();
        var format = separator < 0 ? null : expression[(separator + 1)..];
        return name switch
        {
            "name" or "original" => context.OriginalName,
            "counter" => context.Counter.ToString(
                string.IsNullOrEmpty(format) ? new string('0', context.CounterPadding) : format,
                CultureInfo.InvariantCulture),
            "created" => FormatDate(context.CreatedAt, format, "Created date"),
            "modified" => FormatDate(context.ModifiedAt, format, "Modified date"),
            "taken" => FormatDate(context.Metadata.TakenAt, format, "Date taken"),
            "camera" => Require(context.Metadata.CameraSummary, "Camera"),
            "make" => Require(context.Metadata.CameraMake, "Camera make"),
            "model" => Require(context.Metadata.CameraModel, "Camera model"),
            "lens" => Require(context.Metadata.Lens, "Lens"),
            _ => throw new FormatException($"Unknown rename token '{{{expression}}}'.")
        };
    }

    private static string FormatDate(DateTime? value, string? format, string label)
    {
        if (value is null) throw new FormatException($"{label} is unavailable for this item.");
        try
        {
            return value.Value.ToString(
                string.IsNullOrEmpty(format) ? "yyyy-MM-dd" : format,
                CultureInfo.InvariantCulture);
        }
        catch (FormatException)
        {
            throw new FormatException($"The date format for {label.ToLowerInvariant()} is invalid.");
        }
    }

    private static string Require(string? value, string label) =>
        string.IsNullOrWhiteSpace(value)
            ? throw new FormatException($"{label} metadata is unavailable for this item.")
            : value.Trim();

    private static bool NeedsImageMetadata(string template) =>
        template.Contains("{taken", StringComparison.OrdinalIgnoreCase)
        || template.Contains("{camera", StringComparison.OrdinalIgnoreCase)
        || template.Contains("{make", StringComparison.OrdinalIgnoreCase)
        || template.Contains("{model", StringComparison.OrdinalIgnoreCase)
        || template.Contains("{lens", StringComparison.OrdinalIgnoreCase);

    private sealed record RenameContext(
        string OriginalName,
        int Counter,
        int CounterPadding,
        DateTime? CreatedAt,
        DateTime? ModifiedAt,
        ImageMetadata Metadata)
    {
        public static RenameContext Create(
            string path,
            int counter,
            int padding,
            bool needsImageMetadata)
        {
            var isDirectory = Directory.Exists(path);
            var original = isDirectory
                ? Path.GetFileName(Path.TrimEndingDirectorySeparator(path))
                : Path.GetFileNameWithoutExtension(path);
            DateTime? created = null;
            DateTime? modified = null;
            try
            {
                created = isDirectory ? Directory.GetCreationTime(path) : File.GetCreationTime(path);
                modified = isDirectory ? Directory.GetLastWriteTime(path) : File.GetLastWriteTime(path);
            }
            catch { }

            var metadata = needsImageMetadata && !isDirectory && MediaFileTypes.IsImage(path)
                ? ExifReader.Read(path)
                : new ImageMetadata();
            return new RenameContext(original, counter, Math.Clamp(padding, 1, 12), created, modified, metadata);
        }
    }
}
