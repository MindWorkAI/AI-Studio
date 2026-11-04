using AIStudio.Dialogs.Settings;
using AIStudio.Settings;
using AIStudio.Tools.Rust;

using Microsoft.AspNetCore.Components;

namespace AIStudio.Components;

/// <summary>
/// Chooses one of the format templates of a kind, e.g., the PowerPoint template of the Slide
/// Assistant, and leads to their management.
/// </summary>
/// <remarks>
/// The choice is the path of the file of the template, see ConfigurationSelectDataFactory.GetFormatTemplatesData,
/// which the settings of an assistant share. The export menu of an answer lists the Word templates
/// itself, as entries of a menu, with the same helpers, see FormatTemplateExtensions.
/// </remarks>
public partial class FormatTemplateSelection : MSGComponentBase
{
    /// <summary>
    /// The path of the file of the chosen template; empty for no template.
    /// </summary>
    [Parameter]
    public string FilePath { get; set; } = string.Empty;

    [Parameter]
    public EventCallback<string> FilePathChanged { get; set; }

    /// <summary>
    /// The kind of template to choose from, one of the children of FileTypes.FORMAT_TEMPLATE.
    /// </summary>
    [Parameter]
    public FileTypeFilter Kind { get; set; } = FileTypes.FORMAT_TEMPLATE;

    [Parameter]
    public string Label { get; set; } = string.Empty;

    [Parameter]
    public string Icon { get; set; } = Icons.Material.Filled.Description;

    [Parameter]
    public Func<string, string?> Validation { get; set; } = _ => null;

    [Parameter]
    public bool Disabled { get; set; }

    [Inject]
    private IDialogService DialogService { get; init; } = null!;

    private IReadOnlyList<ConfigurationSelectData<string>> Options => ConfigurationSelectDataFactory.GetFormatTemplatesData(this.SettingsManager.ConfigurationData.FormatTemplates, this.Kind, this.FilePath).ToList();

    #region Overrides of ComponentBase

    protected override async Task OnInitializedAsync()
    {
        this.ApplyFilters([], [ Event.CONFIGURATION_CHANGED ]);
        await base.OnInitializedAsync();
    }

    #endregion

    #region Overrides of MSGComponentBase

    protected override Task ProcessIncomingMessage<T>(ComponentBase? sendingComponent, Event triggeredEvent, T? data) where T : default
    {
        // A template may come, go, or get another name meanwhile, e.g., in the management this selection leads to:
        if (triggeredEvent is Event.CONFIGURATION_CHANGED or Event.PLUGINS_RELOADED)
            this.StateHasChanged();

        return Task.CompletedTask;
    }

    #endregion

    private async Task SelectionChanged(string? filePath)
    {
        // A choice locked by the organization stays as it is:
        if (this.Disabled)
            return;

        this.FilePath = filePath ?? string.Empty;
        await this.FilePathChanged.InvokeAsync(this.FilePath);
    }

    private string NameOf(string? filePath)
    {
        var path = filePath ?? string.Empty;
        foreach (var option in this.Options)
            if (option.Value == path)
                return option.Name;

        return Path.GetFileName(path);
    }

    private string IconOf(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
            return Icons.Material.Filled.LayersClear;

        return this.SettingsManager.ConfigurationData.FormatTemplates.Serving(this.Kind).FirstOrDefault(formatTemplate => formatTemplate.FilePath == filePath)?.Icon ?? Icons.Material.Filled.Description;
    }

    private Task OpenSettingsDialog() => SettingsDialogFormatTemplates.ShowAsync(this.DialogService);
}