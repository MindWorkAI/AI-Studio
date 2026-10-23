using AIStudio.Settings;
using AIStudio.Tools.PowerPoint;

using Microsoft.AspNetCore.Components;

namespace AIStudio.Dialogs.Settings;

public partial class SettingsDialogSlideBuilder : SettingsDialogBase
{
    [Inject]
    private ILogger<SettingsDialogSlideBuilder> Logger { get; init; } = null!;

    /// <summary>
    /// What the preselections of layouts offer: no preference, then the layouts of the preselected template.
    /// </summary>
    /// <remarks>
    /// Empty while no template is preselected or the template cannot be read; the dialog then says so
    /// instead of offering layouts.
    /// </remarks>
    private List<ConfigurationSelectData<string>> layoutOptions = [];

    /// <summary>
    /// The template the layout options were read from; null before the first reading.
    /// </summary>
    private string? layoutOptionsTemplatePath;

    #region Overrides of SettingsDialogBase

    /// <inheritdoc />
    protected override async Task OnInitializedAsync()
    {
        this.ReadLayoutOptions();
        await base.OnInitializedAsync();
    }

    /// <inheritdoc />
    protected override async Task ProcessIncomingMessage<T>(ComponentBase? sendingComponent, Event triggeredEvent, T? data) where T : default
    {
        // The template may change in this dialog, or through a configuration plugin:
        if (triggeredEvent is Event.CONFIGURATION_CHANGED or Event.PLUGINS_RELOADED)
            this.ReadLayoutOptions();

        await base.ProcessIncomingMessage(sendingComponent, triggeredEvent, data);
    }

    #endregion

    /// <summary>
    /// Reads the layouts of the preselected template, unless they were read from it already.
    /// </summary>
    private void ReadLayoutOptions()
    {
        var templatePath = this.SettingsManager.ConfigurationData.SlideBuilder.TemplatePath;
        if (string.Equals(templatePath, this.layoutOptionsTemplatePath, StringComparison.Ordinal))
            return;

        this.layoutOptionsTemplatePath = templatePath;
        this.layoutOptions = [];
        if (string.IsNullOrWhiteSpace(templatePath) || !File.Exists(templatePath))
            return;

        PresentationTemplate template;
        try
        {
            template = PresentationTemplate.ReadFile(templatePath);
        }
        catch (Exception e)
        {
            this.Logger.LogWarning(e, "The PowerPoint template '{TemplatePath}' cannot be read.", templatePath);
            return;
        }

        this.layoutOptions = [new(LayoutPurposeExtensions.NoPreference, string.Empty), ..template.FamilyNames.Select(name => new ConfigurationSelectData<string>(name, name))];
    }

    /// <summary>
    /// The name of the layout preselected for a kind of slide, as the dialog shows it.
    /// </summary>
    /// <returns>The name, or empty when the LLM decides, which it also does when the template has no layout of this name.</returns>
    private string PreselectedLayoutOf(LayoutPurpose purpose) => this.SettingsManager.ConfigurationData.SlideBuilder.PreselectedLayouts.TryGetValue(purpose, out var name) && this.layoutOptions.Any(option => string.Equals(option.Value, name, StringComparison.Ordinal)) ? name : string.Empty;

    /// <summary>
    /// Preselects a layout for a kind of slide, or no preference for an empty name.
    /// </summary>
    /// <remarks>
    /// The dictionary is replaced instead of changed, so that no other holder of it, such as the
    /// default of a configuration plugin, changes along.
    /// </remarks>
    private void SetPreselectedLayout(LayoutPurpose purpose, string name)
    {
        var layouts = new Dictionary<LayoutPurpose, string>(this.SettingsManager.ConfigurationData.SlideBuilder.PreselectedLayouts);
        if (name.Length == 0)
            layouts.Remove(purpose);
        else
            layouts[purpose] = name;

        this.SettingsManager.ConfigurationData.SlideBuilder.PreselectedLayouts = layouts;
    }
}