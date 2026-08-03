using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using ImageViewer.Models;

namespace ImageViewer.Services;

public sealed class BatchPresetStore
{
    private static readonly string PresetPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "ImageViewer",
        "batch-presets.json");

    public BatchPresetDocument Load()
    {
        try
        {
            if (!File.Exists(PresetPath)) return new BatchPresetDocument();
            var json = File.ReadAllText(PresetPath);
            return JsonSerializer.Deserialize(json, BatchPresetJsonContext.Default.BatchPresetDocument)
                   ?? new BatchPresetDocument();
        }
        catch
        {
            return new BatchPresetDocument();
        }
    }

    public bool SaveRename(string name, BatchRenameOptions options)
    {
        var cleanName = name.Trim();
        if (cleanName.Length == 0) return false;
        var document = Load();
        document.RenamePresets.RemoveAll(preset =>
            string.Equals(preset.Name, cleanName, StringComparison.OrdinalIgnoreCase));
        document.RenamePresets.Add(new BatchRenamePreset
        {
            Name = cleanName,
            Template = options.Template,
            SearchText = options.SearchText,
            ReplaceText = options.ReplaceText,
            MatchCase = options.MatchCase,
            CaseMode = options.CaseMode,
            CounterStart = options.CounterStart,
            CounterPadding = options.CounterPadding
        });
        return Save(document);
    }

    public bool SaveProcess(string name, BatchProcessOptions options)
    {
        var cleanName = name.Trim();
        if (cleanName.Length == 0) return false;
        var document = Load();
        document.ProcessPresets.RemoveAll(preset =>
            string.Equals(preset.Name, cleanName, StringComparison.OrdinalIgnoreCase));
        document.ProcessPresets.Add(new BatchProcessPreset
        {
            Name = cleanName,
            OutputMode = options.OutputMode,
            DestinationFolder = options.DestinationFolder,
            Suffix = options.Suffix,
            OverwritePolicy = options.OverwritePolicy,
            Quality = options.Quality,
            PreserveFileDates = options.PreserveFileDates,
            PreserveIccProfile = options.PreserveIccProfile,
            MaxConcurrency = options.MaxConcurrency,
            Operations = options.Operations.Select(operation => operation.Clone()).ToList()
        });
        return Save(document);
    }

    public bool RemoveRename(string name)
    {
        var document = Load();
        var removed = document.RenamePresets.RemoveAll(preset =>
            string.Equals(preset.Name, name, StringComparison.OrdinalIgnoreCase)) > 0;
        return removed && Save(document);
    }

    public bool RemoveProcess(string name)
    {
        var document = Load();
        var removed = document.ProcessPresets.RemoveAll(preset =>
            string.Equals(preset.Name, name, StringComparison.OrdinalIgnoreCase)) > 0;
        return removed && Save(document);
    }

    private static bool Save(BatchPresetDocument document)
    {
        try
        {
            var directory = Path.GetDirectoryName(PresetPath)!;
            Directory.CreateDirectory(directory);
            var temporary = PresetPath + ".tmp";
            var json = JsonSerializer.Serialize(
                document,
                BatchPresetJsonContext.Default.BatchPresetDocument);
            File.WriteAllText(temporary, json);
            File.Move(temporary, PresetPath, overwrite: true);
            return true;
        }
        catch
        {
            return false;
        }
    }
}

public sealed class BatchPresetDocument
{
    public List<BatchRenamePreset> RenamePresets { get; set; } = [];
    public List<BatchProcessPreset> ProcessPresets { get; set; } = [];
}

[JsonSourceGenerationOptions(WriteIndented = true, PropertyNameCaseInsensitive = true)]
[JsonSerializable(typeof(BatchPresetDocument))]
internal partial class BatchPresetJsonContext : JsonSerializerContext
{
}
