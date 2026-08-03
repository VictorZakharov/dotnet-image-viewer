using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace ImageViewer.Services;

public sealed class DuplicateHashCacheDocument
{
    public List<DuplicateHashCacheEntry> Entries { get; set; } = [];
}

public sealed class DuplicateHashCacheEntry
{
    public string Path { get; set; } = "";
    public string Identity { get; set; } = "";
    public long SizeBytes { get; set; }
    public long ModifiedUtcTicks { get; set; }
    public string ContentHash { get; set; } = "";
    public ulong? PerceptualHash { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
}

[JsonSourceGenerationOptions(WriteIndented = false)]
[JsonSerializable(typeof(DuplicateHashCacheDocument))]
internal partial class DuplicateHashCacheJsonContext : JsonSerializerContext
{
}
