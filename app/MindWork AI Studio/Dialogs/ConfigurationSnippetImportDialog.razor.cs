using AIStudio.Components;
using AIStudio.Tools.PluginSystem;

using Lua;
using Microsoft.AspNetCore.Components;

namespace AIStudio.Dialogs;

public partial class ConfigurationSnippetImportDialog : MSGComponentBase
{
    [CascadingParameter]
    private IMudDialogInstance MudDialog { get; set; } = null!;

    [Parameter]
    public string Section { get; set; } = string.Empty;

    [Parameter]
    public string ImportLabel { get; set; } = string.Empty;

    private string snippet = string.Empty;
    private string issue = string.Empty;

    private void Import()
    {
        if (!this.SettingsManager.ConfigurationData.App.CanImportConfigurationSnippet(this.Section))
        {
            this.issue = T("Import is locked by your organization.");
            return;
        }

        if (!ConfigurationSnippetParser.TryParse(this.snippet, this.Section, out var table, out this.issue))
            return;

        try
        {
            ConfigurationSnippetImportValidation.Validate(this.Section, table);
            this.MudDialog.Close(DialogResult.Ok(table));
        }
        catch (FormatException exception)
        {
            this.issue = exception.Message;
        }
    }

    private void Cancel() => this.MudDialog.Cancel();

    public static async Task<LuaTable?> ShowAsync(IDialogService service, string section, string title)
    {
        var parameters = new DialogParameters<ConfigurationSnippetImportDialog>
        {
            { x => x.Section, section },
            { x => x.ImportLabel, title },
        };
        var dialog = await service.ShowAsync<ConfigurationSnippetImportDialog>(title, parameters, DialogOptions.FULLSCREEN);
        var result = await dialog.Result;
        return result is { Canceled: false, Data: LuaTable table } ? table : null;
    }
}
