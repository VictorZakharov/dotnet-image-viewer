using System;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Interactivity;

namespace ImageViewer.Views;

public partial class BatchRenameView
{
    private void RefreshPresetNames(string? select = null)
    {
        var names = _presetStore.Load().RenamePresets
            .Select(preset => preset.Name)
            .OrderBy(name => name, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
        PresetCombo.ItemsSource = names;
        if (!string.IsNullOrEmpty(select)) PresetCombo.SelectedItem = select;
    }

    private void OnPresetSelected(object? sender, SelectionChangedEventArgs e)
    {
        if (!_initialized || PresetCombo.SelectedItem is not string name) return;
        var preset = _presetStore.Load().RenamePresets.FirstOrDefault(candidate =>
            string.Equals(candidate.Name, name, StringComparison.OrdinalIgnoreCase));
        if (preset is null) return;
        PresetNameBox.Text = preset.Name;
        ApplyOptions(preset.ToOptions());
    }

    private void OnSavePreset(object? sender, RoutedEventArgs e)
    {
        var name = (PresetNameBox.Text ?? PresetCombo.SelectedItem as string ?? "").Trim();
        if (!_presetStore.SaveRename(name, ReadOptions()))
        {
            PreviewStatusText.Text = "Enter a preset name; the preset could not be saved.";
            return;
        }
        PresetNameBox.Text = name;
        RefreshPresetNames(name);
        PreviewStatusText.Text = $"Saved preset “{name}”.";
    }

    private void OnRemovePreset(object? sender, RoutedEventArgs e)
    {
        var name = PresetCombo.SelectedItem as string;
        if (string.IsNullOrEmpty(name) || !_presetStore.RemoveRename(name)) return;
        PresetNameBox.Text = "";
        RefreshPresetNames();
        PreviewStatusText.Text = $"Removed preset “{name}”.";
    }
}
