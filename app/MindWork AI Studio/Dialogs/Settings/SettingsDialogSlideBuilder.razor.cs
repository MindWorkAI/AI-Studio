using AIStudio.Assistants.SlideBuilder;
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
    /// The preselected template, whose slides the dialog offers; null while none is preselected or it cannot be read.
    /// </summary>
    private PresentationTemplate? template;

    /// <summary>
    /// The path the template and the layout options were read from; null before the first reading.
    /// </summary>
    private string? templatePathRead;

    #region Overrides of SettingsDialogBase

    /// <inheritdoc />
    protected override async Task OnInitializedAsync()
    {
        this.ReadTemplate();
        await base.OnInitializedAsync();
    }

    /// <inheritdoc />
    protected override async Task ProcessIncomingMessage<T>(ComponentBase? sendingComponent, Event triggeredEvent, T? data) where T : default
    {
        // The template may change in this dialog, or through a configuration plugin:
        if (triggeredEvent is Event.CONFIGURATION_CHANGED or Event.PLUGINS_RELOADED)
            this.ReadTemplate();

        await base.ProcessIncomingMessage(sendingComponent, triggeredEvent, data);
    }

    #endregion

    /// <summary>
    /// Reads the layouts and the slides of the preselected template, unless they were read from it already.
    /// </summary>
    private void ReadTemplate()
    {
        var templatePath = this.SettingsManager.ConfigurationData.SlideBuilder.TemplatePath;
        if (string.Equals(templatePath, this.templatePathRead, StringComparison.Ordinal))
            return;

        this.templatePathRead = templatePath;
        this.template = null;
        this.layoutOptions = [];
        if (string.IsNullOrWhiteSpace(templatePath) || !File.Exists(templatePath))
            return;

        try
        {
            this.template = PresentationTemplate.ReadFile(templatePath);
        }
        catch (Exception e)
        {
            this.Logger.LogWarning(e, "The PowerPoint template '{TemplatePath}' cannot be read.", templatePath);
            return;
        }

        this.layoutOptions = [new(LayoutPurposeExtensions.NoPreference, string.Empty), ..this.template.FamilyNames.Select(name => new ConfigurationSelectData<string>(name, name))];
    }

    /// <summary>
    /// Preselects a template, and forgets what was preselected for the slides of the previous one.
    /// </summary>
    /// <remarks>
    /// The choices number the slides of a template, so they mean nothing for another one. A lock of
    /// the organization keeps them, though: its template and its choices belong together.
    /// </remarks>
    private void SetTemplatePath(string path)
    {
        var settings = this.SettingsManager.ConfigurationData.SlideBuilder;
        if (string.Equals(settings.TemplatePath, path, StringComparison.Ordinal))
            return;

        settings.TemplatePath = path;
        if (!AreTemplateSlideUsesLocked())
            settings.PreselectedTemplateSlideUses = new();
    }

    private static bool AreTemplateSlideUsesLocked() => ManagedConfiguration.TryGet(x => x.SlideBuilder, x => x.PreselectedTemplateSlideUses, out var meta) && meta.IsLocked;

    /// <summary>
    /// What the dialog offers for a slide of the template: editing only where the slide has placeholders for content.
    /// </summary>
    private static List<ConfigurationSelectData<TemplateSlideUse>> UseOptionsOf(TemplateSlide slide) => Enum.GetValues<TemplateSlideUse>()
        .Where(use => use is not TemplateSlideUse.EDIT || slide.IsEditable)
        .Select(use => new ConfigurationSelectData<TemplateSlideUse>(use.Name(), use))
        .ToList();

    /// <summary>
    /// What is preselected for a slide of the template, as the assistant applies it.
    /// </summary>
    /// <returns>The use; a slide which is missing is left out, and one which cannot be edited is kept unchanged instead.</returns>
    private TemplateSlideUse PreselectedUseOf(TemplateSlide slide) => this.template is null ? TemplateSlideUse.OMIT : this.SettingsManager.ConfigurationData.SlideBuilder.PreselectedTemplateSlideUses.ToTemplateSlideUses(this.template).UseOf(slide.Number);

    /// <summary>
    /// Preselects what the presentation does with a slide of the template.
    /// </summary>
    /// <remarks>
    /// Leaving a slide out is the default, so it is stored as no entry. The dictionary is replaced
    /// instead of changed, so that no other holder of it, such as the default of a configuration
    /// plugin, changes along.
    /// </remarks>
    private void SetPreselectedUse(int number, TemplateSlideUse use)
    {
        var uses = new Dictionary<int, TemplateSlideUse>(this.SettingsManager.ConfigurationData.SlideBuilder.PreselectedTemplateSlideUses);
        if (use is TemplateSlideUse.OMIT)
            uses.Remove(number);
        else
            uses[number] = use;

        this.SettingsManager.ConfigurationData.SlideBuilder.PreselectedTemplateSlideUses = uses;
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