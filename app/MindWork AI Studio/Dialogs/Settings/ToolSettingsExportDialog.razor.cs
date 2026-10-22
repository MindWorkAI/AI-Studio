using AIStudio.Provider;
using AIStudio.Tools.PluginSystem;
using AIStudio.Tools.ToolCallingSystem;

using Microsoft.AspNetCore.Components;

namespace AIStudio.Dialogs.Settings;

public partial class ToolSettingsExportDialog : SettingsDialogBase
{
    /// <summary>
    /// The ID of the collection whose settings to export. The ID of one of its tools stands for the whole collection.
    /// </summary>
    [Parameter]
    public string CollectionId { get; set; } = string.Empty;

    [Inject]
    private ToolRegistry ToolRegistry { get; init; } = null!;

    [Inject]
    private ToolSettingsService ToolSettingsService { get; init; } = null!;

    [Inject]
    private ILogger<ToolSettingsExportDialog> Logger { get; init; } = null!;

    private ToolCatalogItem? item;
    private IReadOnlyList<ToolSettingsExportArea> areas = [];
    private HashSet<string> selectedAreaIds = new(StringComparer.Ordinal);
    private readonly HashSet<ToolField> configuredSecretFields = [];
    private readonly HashSet<ToolField> emptyFields = [];
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

    private bool HasSelectedSecrets => this.SelectedFields.Any(this.configuredSecretFields.Contains);

    private bool CanIncludeSecrets => this.HasSelectedSecrets && PluginFactory.EnterpriseEncryption?.IsAvailable is true;

    /// <summary>
    /// How many of the selected settings hold no value, counting a field shared by two areas once.
    /// </summary>
    /// <remarks>
    /// Saving a tool's settings writes every field of its schema, empty ones included, so an area
    /// the administrator never filled in still exports. Locked, those empty values are what the
    /// recipient is left with and cannot change, which is worth saying before the export.
    /// </remarks>
    private int EmptySelectedFieldCount => this.SelectedFields.Count(this.emptyFields.Contains);

    /// <summary>
    /// The fields of all selected areas, each once.
    /// </summary>
    private IEnumerable<ToolField> SelectedFields => this.areas
        .Where(area => this.selectedAreaIds.Contains(area.Id))
        .SelectMany(area => area.Area.FieldNames.Select(fieldName => new ToolField(area.Tool.Definition.Id, fieldName)))
        .Distinct();

    private bool WarnAboutEmptyLockedSettings => this.mode is ToolSettingsExportMode.LOCKED && this.EmptySelectedFieldCount > 0;

    private bool CanExport => this.IsAdmin && !this.isLoading && !this.isExporting && !this.isDisposed &&
        this.item is not null && (this.selectedAreaIds.Count > 0 || this.includeMinimumProviderConfidence);

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
            this.item = await this.ToolRegistry.GetCatalogItemAsync(this.CollectionId);
            if (this.item is null)
                return;

            this.areas = ToolSettingsService.GetExportAreas(this.item.Tools);
            this.selectedAreaIds = this.areas.Select(area => area.Id).ToHashSet(StringComparer.Ordinal);

            // Retain only field names, never the values themselves, so no plaintext secret lives
            // in this component. ExportAsync reads effective settings again when the
            // administrator exports.
            foreach (var tool in this.item.Tools)
            {
                var values = await this.ToolSettingsService.GetSettingsAsync(tool.Definition);
                var properties = tool.Definition.SettingsSchema.Properties;
                this.configuredSecretFields.UnionWith(properties
                    .Where(property => property.Value.Secret && values.TryGetValue(property.Key, out var value) && !string.IsNullOrWhiteSpace(value))
                    .Select(property => new ToolField(tool.Definition.Id, property.Key)));

                // A field the export writes as an empty value: it has to be present, because a
                // missing one is skipped rather than exported, and it has to be a non-secret,
                // because an empty secret is skipped as well.
                this.emptyFields.UnionWith(properties
                    .Where(property => !property.Value.Secret && values.TryGetValue(property.Key, out var value) && string.IsNullOrWhiteSpace(value))
                    .Select(property => new ToolField(tool.Definition.Id, property.Key)));
            }
        }
        catch (Exception e)
        {
            // A runtime error may contain secret data, so it goes to the log for diagnosis but
            // never into the dialog:
            this.Logger.LogError(e, "Failed to load the configuration of the tool collection '{CollectionId}' for export.", this.CollectionId);
            this.item = null;
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
        var confidence = this.item is null ? ConfidenceLevel.NONE : this.ToolRegistry.GetMinimumProviderConfidence(this.item.Id);
        return confidence is ConfidenceLevel.NONE ? T("No minimum confidence level chosen") : confidence.GetName();
    }

    private async Task Export()
    {
        if (!this.CanExport || this.item is null)
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

            var result = await this.ToolSettingsService.ExportAsync(this.item.Tools, options, this.item.Id, this.ToolRegistry.GetMinimumProviderConfidence(this.item.Id));
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
            this.Logger.LogError(e, "Failed to export the configuration of the tool collection '{CollectionId}'.", this.CollectionId);
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

    /// <summary>
    /// One settings field of one tool. A collection may hold two tools whose fields share a name.
    /// </summary>
    private readonly record struct ToolField(string ToolId, string FieldName);
}