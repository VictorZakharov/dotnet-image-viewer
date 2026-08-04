namespace ImageViewer.Models;

public enum BatchNameCase
{
    Unchanged,
    Lowercase,
    Uppercase,
    TitleCase
}

public sealed record BatchRenameOptions(
    string Template,
    string SearchText,
    string ReplaceText,
    bool MatchCase,
    BatchNameCase CaseMode,
    int CounterStart,
    int CounterPadding);

public sealed class BatchRenamePreset
{
    public string Name { get; set; } = "";
    public string Template { get; set; } = "{name}_{counter}";
    public string SearchText { get; set; } = "";
    public string ReplaceText { get; set; } = "";
    public bool MatchCase { get; set; }
    public BatchNameCase CaseMode { get; set; }
    public int CounterStart { get; set; } = 1;
    public int CounterPadding { get; set; } = 3;

    public BatchRenameOptions ToOptions() => new(
        Template,
        SearchText,
        ReplaceText,
        MatchCase,
        CaseMode,
        CounterStart,
        CounterPadding);
}
