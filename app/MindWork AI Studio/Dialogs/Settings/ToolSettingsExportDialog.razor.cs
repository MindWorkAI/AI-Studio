using AIStudio.Provider;
using AIStudio.Tools.PluginSystem;
using AIStudio.Tools.ToolCallingSystem;

using Microsoft.AspNetCore.Components;

namespace AIStudio.Dialogs.Settings;

public partial class ToolSettingsExportDialog : SettingsDialogBase
{
    [Parameter]
    public string ToolId { get; set; } = string.Empty;

    [Inject]
    private ToolRegistry ToolRegistry { get; init; } = null!;

    [Inject]
    private ToolSettingsService ToolSettingsService { get; init; } = null!;

    [Inject]
    private ILogger<ToolSettingsExportDialog> Logger { get; init; } = null!;

    private ToolDefinition? toolDefinition;
    private IToolImplementation? implementation;
    private IReadOnlyList<ExportableSettings> areas = [];
    private HashSet<string> selectedAreaIds = new(StringComparer.Ordinal);
    private HashSet<string> configuredSecretFields = new(StringComparer.Ordinal);
    private HashSet<string> emptyFieldNames = new(StringComparer.Ordinal);
    private ToolSettingsExportMode mode = ToolSettingsExportMode.LOCKED;
    private bool includeSecrets;
    private bool includeMinimumProviderConfidence = true;
    private bool isLoading = true;
    private bool isExporting;
    private bool isDisposed;
    private string message = string.Empty;
    private Severity messageSeverity = Severity.Error;

    private bool IsAdmin => this.SettingsManager.ConfigurationData.App.ShowAdminSettings;

    private bool AllAreasSelected => this.areas.Count > 0 && this.areas.All(area => this.selectedAreaIds.Contains(area.Id));

    private bool HasSelectedSecrets => this.areas.Any(area => this.selectedAreaIds.Contains(area.Id) && area.FieldNames.Any(this.configuredSecretFields.Contains));

    private bool CanIncludeSecrets => this.HasSelectedSecrets && PluginFactory.EnterpriseEncryption?.IsAvailable is true;

    /// <summary>
    /// How many of the selected settings hold no value, counting a field shared by two areas once.
    /// </summary>
    /// <remarks>
    /// Saving a tool's settings writes every field of its schema, empty ones included, so an area
    /// the administrator never filled in still exports. Locked, those empty values are what the
    /// recipient is left with and cannot change, which is worth saying before the export.
    /// </remarks>
    private int EmptySelectedFieldCount => this.areas
        .Where(area => this.selectedAreaIds.Contains(area.Id))
        .SelectMany(area => area.FieldNames)
        .Distinct(StringComparer.Ordinal)
        .Count(this.emptyFieldNames.Contains);

    private bool WarnAboutEmptyLockedSettings => this.mode is ToolSettingsExportMode.LOCKED && this.EmptySelectedFieldCount > 0;

    private bool CanExport => this.IsAdmin && !this.isLoading && !this.isExporting && !this.isDisposed &&
        this.toolDefinition is not null && this.implementation is not null && (this.selectedAreaIds.Count > 0 || this.includeMinimumProviderConfidence);

    protected override async Task OnInitializedAsync()
    {
        await base.OnInitializedAsync();
        if (!this.IsAdmin)
        {
            this.Close();
            return;
        }

        try
        {
            this.toolDefinition = this.ToolRegistry.GetDefinition(this.ToolId);
            if (this.toolDefinition is null)
                return;

            this.implementation = this.ToolRegistry.GetImplementation(this.toolDefinition.ImplementationKey);
            if (this.implementation is null)
                return;

            this.areas = this.implementation.GetExportableSettings(this.toolDefinition);
            this.selectedAreaIds = this.areas.Select(area => area.Id).ToHashSet(StringComparer.Ordinal);

            // Retain only field names, never the values themselves, so no plaintext secret lives
            // in this component. ExportAsync reads effective settings again when the
            // administrator exports.
            var values = await this.ToolSettingsService.GetSettingsAsync(this.toolDefinition);
            this.configuredSecretFields = this.toolDefinition.SettingsSchema.Properties
                .Where(property => property.Value.Secret && values.TryGetValue(property.Key, out var value) && !string.IsNullOrWhiteSpace(value))
                .Select(property => property.Key)
                .ToHashSet(StringComparer.Ordinal);

            // A field the export writes as an empty value: it has to be present, because a
            // missing one is skipped rather than exported, and it has to be a non-secret,
            // because an empty secret is skipped as well.
            this.emptyFieldNames = this.toolDefinition.SettingsSchema.Properties
                .Where(property => !property.Value.Secret && values.TryGetValue(property.Key, out var value) && string.IsNullOrWhiteSpace(value))
                .Select(property => property.Key)
                .ToHashSet(StringComparer.Ordinal);
        }
        catch (Exception e)
        {
            // A runtime error may contain secret data, so it goes to the log for diagnosis but
            // never into the dialog:
            this.Logger.LogError(e, "Failed to load the configuration of the tool '{ToolId}' for export.", this.ToolId);
            this.toolDefinition = null;
            this.message = T("The tool configuration could not be loaded. Please close this dialog and try again.");
        }
        finally
        {
            this.isLoading = false;
        }
    }

    private void SelectArea(string areaId, bool selected)
    {
        if (selected)
            this.selectedAreaIds.Add(areaId);
        else
            this.selectedAreaIds.Remove(areaId);

        this.SelectionChanged();
    }

    private void SelectAllAreas(bool selected)
    {
        this.selectedAreaIds = selected ? this.areas.Select(area => area.Id).ToHashSet(StringComparer.Ordinal) : new(StringComparer.Ordinal);
        this.SelectionChanged();
    }

    private void SelectionChanged()
    {
        // A new selection must not keep an invisible opt-in to secrets it no longer contains.
        if (!this.CanIncludeSecrets)
            this.includeSecrets = false;

        this.message = string.Empty;
    }

    private string GetMinimumProviderConfidenceName()
    {
        var confidence = this.toolDefinition is null ? ConfidenceLevel.NONE : this.ToolRegistry.GetMinimumProviderConfidence(this.toolDefinition);
        return confidence is ConfidenceLevel.NONE ? T("No minimum confidence level chosen") : confidence.GetName();
    }

    private async Task Export()
    {
        if (!this.CanExport || this.toolDefinition is null || this.implementation is null)
            return;

        this.isExporting = true;
        this.message = string.Empty;
        this.messageSeverity = Severity.Error;
        try
        {
            var options = new ToolSettingsExportOptions
            {
                SelectedAreaIds = new HashSet<string>(this.selectedAreaIds, StringComparer.Ordinal),
                Mode = this.mode,
                IncludeSecrets = this.includeSecrets,
                IncludeMinimumProviderConfidence = this.includeMinimumProviderConfidence,
            };

            var result = await this.ToolSettingsService.ExportAsync(this.toolDefinition, this.implementation, options);
            if (this.isDisposed || !this.IsAdmin)
                return;

            if (!result.Success)
            {
                this.message = result.ErrorMessage;
                return;
            }

            if (string.IsNullOrWhiteSpace(result.LuaCode))
            {
                this.messageSeverity = Severity.Info;
                this.message = T("The selected areas contain no settings to export.");
                return;
            }

            // The runtime reports clipboard success or failure. Keep the dialog open so that
            // administrators can retry or export another selection from the same tool.
            await this.RustService.CopyText2Clipboard(result.LuaCode);
        }
        catch (Exception e)
        {
            this.Logger.LogError(e, "Failed to export the configuration of the tool '{ToolId}'.", this.ToolId);
            this.message = T("The tool configuration could not be exported. Please try again.");
        }
        finally
        {
            this.isExporting = false;
        }
    }

    protected override void DisposeResources()
    {
        this.isDisposed = true;
        base.DisposeResources();
    }
}