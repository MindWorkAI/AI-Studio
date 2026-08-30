using System.Text;
using AIStudio.Agents.AssistantAudit;
using AIStudio.Components;
using AIStudio.Tools.PluginSystem;
using AIStudio.Tools.PluginSystem.Assistants;
using AIStudio.Tools.Services;
using Microsoft.AspNetCore.Components;

namespace AIStudio.Dialogs;

public sealed record DirectChatLauncherSettingsDialogResult(Guid PluginId, string PluginName, PluginAssistantAudit? Audit);

/// <summary>
/// Changes the settings of an installed direct chat launcher without asking a model.
/// </summary>
/// <remarks>
/// A launcher has no prompt and no form, so every change a user can make here is a different pick
/// from a drop-down. The dialog therefore writes the plugin itself through
/// DirectChatLauncherLuaWriter and reuses the regular assistant update path for validating,
/// writing, and rolling back.
/// </remarks>
public partial class DirectChatLauncherSettingsDialog : MSGComponentBase
{
    private const string PLUGIN_FILE_NAME = "plugin.lua";
    private static readonly ILogger LOGGER = Program.LOGGER_FACTORY.CreateLogger(nameof(DirectChatLauncherSettingsDialog));

    [CascadingParameter]
    private IMudDialogInstance MudDialog { get; set; } = null!;

    [Inject]
    private PluginInstallService PluginInstallService { get; init; } = null!;

    [Inject]
    private AssistantPluginAuditService AssistantPluginAuditService { get; init; } = null!;

    [Parameter]
    public Guid PluginId { get; set; }

    [Parameter]
    public string PluginLocalPath { get; set; } = string.Empty;

    private IAvailablePlugin? availablePlugin;
    private PluginAssistants? assistantPlugin;
    private MudForm? form;
    private string pluginName = string.Empty;
    private string title = string.Empty;
    private string description = string.Empty;
    private string workspaceName = string.Empty;
    private string providerId = string.Empty;
    private string profileId = string.Empty;
    private string chatTemplateId = string.Empty;
    private IEnumerable<string> dataSourceIds = [];
    private string issue = string.Empty;
    private bool canEdit;
    private bool isLoading = true;
    private bool isSaving;
    private bool isAuditing;

    private bool IsBusy => this.isSaving || this.isAuditing;

    private bool CanSave => this.canEdit && this.assistantPlugin is not null && this.availablePlugin is not null && !this.isLoading && !this.IsBusy;

    #region Overrides of MSGComponentBase

    protected override async Task OnInitializedAsync()
    {
        try
        {
            this.availablePlugin = PluginFactory.AvailablePlugins
                .OfType<IAvailablePlugin>()
                .FirstOrDefault(x => x.Id == this.PluginId && AreSamePath(x.LocalPath, this.PluginLocalPath));

            this.assistantPlugin = PluginFactory.RunningPlugins
                .OfType<PluginAssistants>()
                .FirstOrDefault(x => x.Id == this.PluginId && AreSamePath(x.PluginPath, this.PluginLocalPath));

            if (this.availablePlugin is null || this.assistantPlugin is null)
            {
                this.issue = T("The assistant plugin could not be resolved.");
                return;
            }

            if (!DirectChatLauncherLuaWriter.CanRewrite(this.assistantPlugin) || this.assistantPlugin.ChatLaunchConfiguration is not { } launch)
            {
                this.issue = T("Only locally managed direct chat launchers can be edited here.");
                return;
            }

            //
            // Saving replaces the whole plugin.lua. Anything the file carries beyond the canonical
            // launcher shape would be lost, so those plugins keep the code editor and the AI
            // revision instead of this dialog:
            //
            var pluginFile = Path.Join(this.availablePlugin.LocalPath, PLUGIN_FILE_NAME);
            if (!File.Exists(pluginFile))
            {
                this.issue = T("The plugin.lua file could not be found.");
                return;
            }

            var currentLua = await File.ReadAllTextAsync(pluginFile, Encoding.UTF8);
            if (DirectChatLauncherLuaWriter.HasCompanionLuaFiles(this.assistantPlugin) || !DirectChatLauncherLuaWriter.IsCanonicalSource(currentLua))
            {
                this.issue = T("This launcher contains its own icon or additional Lua code. Please edit it with the plugin code editor, so nothing of it gets lost.");
                return;
            }

            this.pluginName = this.assistantPlugin.Name;
            this.title = this.assistantPlugin.AssistantTitle;
            this.description = string.IsNullOrWhiteSpace(this.assistantPlugin.Description)
                ? this.assistantPlugin.AssistantDescription
                : this.assistantPlugin.Description;

            this.workspaceName = launch.WorkspaceName;
            this.providerId = launch.ProviderId?.ToString() ?? string.Empty;
            this.profileId = launch.ProfileId?.ToString() ?? string.Empty;
            this.chatTemplateId = launch.ChatTemplateId?.ToString() ?? string.Empty;
            this.dataSourceIds = launch.DataSourceIds?.Select(id => id.ToString()).ToArray() ?? [];
            this.canEdit = true;
        }
        catch (Exception e)
        {
            this.issue = string.Format(T("The assistant plugin could not be loaded: {0}"), e.Message);
        }
        finally
        {
            this.isLoading = false;
        }

        await base.OnInitializedAsync();
    }

    #endregion

    private string BuildLua() => this.assistantPlugin is null
        ? string.Empty
        : DirectChatLauncherLuaWriter.Write(this.assistantPlugin, this.BuildDefinition());

    private DirectChatLauncherDefinition BuildDefinition() => new(
        this.pluginName.Trim(),
        this.title.Trim(),
        this.description.Trim(),
        this.BuildLaunchConfiguration());

    private AssistantChatLaunchConfiguration BuildLaunchConfiguration()
    {
        var selectedDataSourceIds = this.dataSourceIds
            .Select(id => Guid.TryParse(id, out var parsed) ? parsed : Guid.Empty)
            .Where(id => id != Guid.Empty)
            .Distinct()
            .ToArray();

        //
        // An empty selection means "use the chat defaults" and is left out of the plugin, whereas
        // the empty GUID explicitly selects no profile or no chat template:
        //
        return new(
            this.workspaceName.Trim(),
            ParseOptionalGuid(this.providerId),
            ParseOptionalGuid(this.profileId),
            ParseOptionalGuid(this.chatTemplateId),
            selectedDataSourceIds.Length == 0 ? null : selectedDataSourceIds);
    }

    private async Task SaveAsync()
    {
        if (!this.CanSave || this.assistantPlugin is null || this.availablePlugin is null || this.form is null)
            return;

        await this.form.Validate();
        if (!this.form.IsValid)
            return;

        this.isSaving = true;
        this.issue = string.Empty;
        await this.InvokeAsync(this.StateHasChanged);

        try
        {
            var lua = DirectChatLauncherLuaWriter.Write(this.assistantPlugin, this.BuildDefinition());

            //
            // The writer produces the plugin deterministically, but the update path is still the
            // authority: it validates the Lua, writes it atomically with a backup, and restores the
            // previous file when the reload fails.
            //
            var checkResult = await this.PluginInstallService.CheckInstalledAssistantUpdateAsync(this.availablePlugin, lua, CancellationToken.None);
            if (!checkResult.Success)
            {
                LOGGER.LogError($"The rewritten chat launcher '{this.pluginName}' ({this.PluginId}) is not valid. Issue: {checkResult.Issue}");
                this.issue = checkResult.Issue;
                return;
            }

            var updateResult = await this.PluginInstallService.UpdateInstalledAssistantAsync(this.availablePlugin, lua, CancellationToken.None);
            if (!updateResult.Success)
            {
                LOGGER.LogError($"Failed to save the chat launcher '{updateResult.PluginName}' ({updateResult.PluginId}) in '{updateResult.PluginDirectory}'. Issue: {updateResult.Issue}");
                this.issue = updateResult.Issue;
                return;
            }

            //
            // Writing the file changes the audit hash, so a stored audit no longer applies:
            //
            PluginAssistantAudit? audit = null;
            if (this.SettingsManager.ConfigurationData.AssistantPluginAudit.AutomaticallyAuditAssistants)
                audit = await this.TryRunAuditAsync(updateResult.PluginId);

            this.MudDialog.Close(DialogResult.Ok(new DirectChatLauncherSettingsDialogResult(updateResult.PluginId, updateResult.PluginName, audit)));
        }
        finally
        {
            this.isSaving = false;
            if (!string.IsNullOrWhiteSpace(this.issue))
                await this.InvokeAsync(this.StateHasChanged);
        }
    }

    private async Task<PluginAssistantAudit?> TryRunAuditAsync(Guid pluginId)
    {
        var updatedPlugin = PluginFactory.RunningPlugins.OfType<PluginAssistants>().FirstOrDefault(x => x.Id == pluginId);
        if (updatedPlugin is null)
            return null;

        this.isAuditing = true;
        await this.InvokeAsync(this.StateHasChanged);
        try
        {
            var audit = await this.AssistantPluginAuditService.RunAuditAsync(updatedPlugin);
            if (audit.Level is AssistantAuditLevel.UNKNOWN)
                return audit;

            UpsertAudit(this.SettingsManager.ConfigurationData.AssistantPluginAudits, audit);
            await this.SettingsManager.StoreSettings();
            return audit;
        }
        finally
        {
            this.isAuditing = false;
        }
    }

    private string? ValidatePluginName(string value) => string.IsNullOrWhiteSpace(value) ? T("Please provide a name for this plugin.") : null;

    private string? ValidateTitle(string value) => string.IsNullOrWhiteSpace(value) ? T("Please provide a title for this tile.") : null;

    private string? ValidateDescription(string value) => string.IsNullOrWhiteSpace(value) ? T("Please provide a description for this tile.") : null;

    private string? ValidateWorkspaceName(string value) => string.IsNullOrWhiteSpace(value) ? T("Please select or enter a workspace name for this tile.") : null;

    private void Cancel() => this.MudDialog.Cancel();

    private static Guid? ParseOptionalGuid(string value) => Guid.TryParse(value, out var parsed) ? parsed : null;

    private static void UpsertAudit(IList<PluginAssistantAudit> audits, PluginAssistantAudit audit)
    {
        var existingIndex = audits.ToList().FindIndex(x => x.PluginId == audit.PluginId);
        if (existingIndex >= 0)
            audits[existingIndex] = audit;
        else
            audits.Add(audit);
    }

    private static bool AreSamePath(string left, string right)
    {
        if (string.IsNullOrWhiteSpace(left) || string.IsNullOrWhiteSpace(right))
            return false;

        var comparison = OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;

        return string.Equals(
            Path.GetFullPath(left).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
            Path.GetFullPath(right).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
            comparison);
    }
}