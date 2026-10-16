using AIStudio.Components;
using AIStudio.Tools.PluginSystem;
using AIStudio.Tools.ToolCallingSystem;

using Lua;
using Microsoft.AspNetCore.Components;

namespace AIStudio.Dialogs;

public partial class ConfigurationSnippetImportDialog : MSGComponentBase
{
    [CascadingParameter]
    private IMudDialogInstance MudDialog { get; set; } = null!;

    [Parameter]
    public PluginConfigurationObjectType Type { get; set; } = PluginConfigurationObjectType.NONE;

    [Parameter]
    public string ImportLabel { get; set; } = string.Empty;

    /// <summary>
    /// Whether the dialog hands over the table which the snippet assigns instead of an imported object.
    /// </summary>
    [Parameter]
    public bool ReturnsLuaTable { get; set; }

    [Inject]
    private ToolRegistry ToolRegistry { get; init; } = null!;

    private string snippet = string.Empty;
    private string issue = string.Empty;

    /// <summary>
    /// What the snippet imports, while the user still has to read its hints.
    /// </summary>
    private ConfigurationSnippetImportResult? importWithHints;

    private void SnippetChanged(string text)
    {
        // The issue and the hints belong to the snippet they were found in:
        this.snippet = text;
        this.issue = string.Empty;
        this.importWithHints = null;
    }

    private async Task Import()
    {
        if (this.ReturnsLuaTable)
        {
            this.ImportLuaTable();
            return;
        }

        if (!ConfigurationSnippetImport.TryImport(this.snippet, this.Type, out var imported, out this.issue))
            return;

        imported = await ConfigurationSnippetImport.ResolveReferencesAsync(imported, this.SettingsManager, this.ToolRegistry);
        if (imported.Hints.Count > 0)
        {
            this.importWithHints = imported;
            return;
        }

        this.Close(imported);
    }

    private void ContinueAnyway()
    {
        if (this.importWithHints is not null)
            this.Close(this.importWithHints);
    }

    private void Close(ConfigurationSnippetImportResult imported)
    {
        //
        // An organization can lock the import at any time, e.g., with a new version of its
        // configuration plugin, and the user may take a while to read the hints. Hence, the
        // permission is checked when the dialog hands over the imported object:
        //
        if (!this.SettingsManager.ConfigurationData.App.CanImportConfigurationSnippet(this.Type))
        {
            this.issue = T("Import is locked by your organization.");
            this.importWithHints = null;
            return;
        }

        this.MudDialog.Close(DialogResult.Ok(imported));
    }

    private void ImportLuaTable()
    {
        if (!this.SettingsManager.ConfigurationData.App.CanImportConfigurationSnippet(this.Type))
        {
            this.issue = T("Import is locked by your organization.");
            return;
        }

        if (!ConfigurationSnippetParser.TryParse(this.snippet, this.Type, out var table, out this.issue))
            return;

        try
        {
            ConfigurationSnippetImportValidation.Validate(this.Type, table);
            this.MudDialog.Close(DialogResult.Ok(table));
        }
        catch (FormatException exception)
        {
            this.issue = exception.Message;
        }
    }

    private void Cancel() => this.MudDialog.Cancel();

    /// <summary>
    /// Lets the user paste a configuration snippet and imports it as an object of the user's own.
    /// </summary>
    /// <param name="service">The dialog service.</param>
    /// <param name="type">The type of configuration object to import.</param>
    /// <param name="title">The title of the dialog, which also labels its import button.</param>
    /// <returns>The imported object, or null when the user canceled.</returns>
    public static async Task<ConfigurationSnippetImportResult?> ShowAsync(IDialogService service, PluginConfigurationObjectType type, string title)
    {
        var result = await ShowDialogAsync(service, type, title, returnsLuaTable: false);
        return result is { Canceled: false, Data: ConfigurationSnippetImportResult imported } ? imported : null;
    }

    /// <summary>
    /// Lets the user paste a configuration snippet and returns the table it assigns, for the forms
    /// of providers and data sources, which still fill themselves from it.
    /// </summary>
    public static async Task<LuaTable?> ShowForLuaTableAsync(IDialogService service, PluginConfigurationObjectType type, string title)
    {
        var result = await ShowDialogAsync(service, type, title, returnsLuaTable: true);
        return result is { Canceled: false, Data: LuaTable table } ? table : null;
    }

    private static async Task<DialogResult?> ShowDialogAsync(IDialogService service, PluginConfigurationObjectType type, string title, bool returnsLuaTable)
    {
        var parameters = new DialogParameters<ConfigurationSnippetImportDialog>
        {
            { x => x.Type, type },
            { x => x.ImportLabel, title },
            { x => x.ReturnsLuaTable, returnsLuaTable },
        };
        var dialog = await service.ShowAsync<ConfigurationSnippetImportDialog>(title, parameters, DialogOptions.FULLSCREEN);
        return await dialog.Result;
    }
}