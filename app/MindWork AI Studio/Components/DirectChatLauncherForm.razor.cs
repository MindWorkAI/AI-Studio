using AIStudio.Settings;

using Microsoft.AspNetCore.Components;

namespace AIStudio.Components;

/// <summary>
/// The selection a direct chat launcher needs: the workspace its chat is created in — or no
/// workspace, for a disappearing chat — and the provider, profile, chat template, and data sources
/// that chat starts with.
/// </summary>
/// <remarks>
/// The Assistant Builder uses this form to describe a launcher it is about to generate, while the
/// launcher settings dialog uses it to change an installed launcher. Both keep their own state, so
/// every field is a two-way bound parameter here.
/// </remarks>
public partial class DirectChatLauncherForm : MSGComponentBase
{
    /// <summary>
    /// The name of the workspace the launcher opens its chat in. The workspace is created when it
    /// does not exist yet, hence this is a free-text field and not a workspace ID. An empty name is
    /// a choice of its own: the launcher then opens a disappearing chat.
    /// </summary>
    [Parameter]
    public string WorkspaceName { get; set; } = string.Empty;

    [Parameter]
    public EventCallback<string> WorkspaceNameChanged { get; set; }

    /// <summary>
    /// The provider ID for the chat, or an empty string to use the chat default.
    /// </summary>
    [Parameter]
    public string ProviderId { get; set; } = string.Empty;

    [Parameter]
    public EventCallback<string> ProviderIdChanged { get; set; }

    /// <summary>
    /// The profile ID for the chat, an empty GUID for explicitly no profile, or an empty string to
    /// use the chat default.
    /// </summary>
    [Parameter]
    public string ProfileId { get; set; } = string.Empty;

    [Parameter]
    public EventCallback<string> ProfileIdChanged { get; set; }

    /// <summary>
    /// The chat template ID, an empty GUID for explicitly no template, or an empty string to use
    /// the chat default.
    /// </summary>
    [Parameter]
    public string ChatTemplateId { get; set; } = string.Empty;

    [Parameter]
    public EventCallback<string> ChatTemplateIdChanged { get; set; }

    /// <summary>
    /// The data sources the chat starts with. An empty selection keeps the normal chat defaults.
    /// </summary>
    [Parameter]
    public IEnumerable<string> DataSourceIds { get; set; } = [];

    [Parameter]
    public EventCallback<IEnumerable<string>> DataSourceIdsChanged { get; set; }

    /// <summary>
    /// The tools preselected for the chat. An empty selection keeps the normal chat defaults.
    /// </summary>
    /// <remarks>
    /// A preselection, not a limit: the user can switch tools in the chat as usual. What a tool
    /// may actually do is decided there, by the confidence of the provider in use.
    /// </remarks>
    [Parameter]
    public HashSet<string> ToolIds { get; set; } = [];

    [Parameter]
    public EventCallback<HashSet<string>> ToolIdsChanged { get; set; }

    /// <summary>
    /// Whether the launcher currently describes a chat without a workspace.
    /// </summary>
    private bool OpensTemporaryChat => string.IsNullOrWhiteSpace(this.WorkspaceName);

    /// <summary>
    /// The chat template the launcher would open its chat with, as far as it is known here.
    /// </summary>
    /// <remarks>
    /// With "use chat default" chosen, this is whichever template the chat options name right now,
    /// and that may well be another one by the time somebody opens the launcher. The form therefore
    /// only says what such a template brings along instead of disabling the fields below it: a field
    /// which locks itself behind the user's back is worse than a sentence explaining the situation.
    /// </remarks>
    private ChatTemplate SelectedChatTemplate => string.IsNullOrWhiteSpace(this.ChatTemplateId)
        ? this.SettingsManager.GetPreselectedChatTemplate(Tools.Components.CHAT)
        : this.SettingsManager.GetChatTemplateById(this.ChatTemplateId);

    private IReadOnlyList<WorkspaceTreeWorkspace> availableWorkspaces = [];

    private static readonly Dictionary<string, object?> USER_INPUT_ATTRIBUTES = new();

    #region Overrides of MSGComponentBase

    protected override async Task OnInitializedAsync()
    {
        // Configure the spellchecking for the workspace name input:
        this.SettingsManager.InjectSpellchecking(USER_INPUT_ATTRIBUTES);

        await base.OnInitializedAsync();

        var workspaceSnapshot = await WorkspaceBehaviour.GetOrLoadWorkspaceTreeShellAsync();
        this.availableWorkspaces = workspaceSnapshot.Workspaces;
    }

    #endregion

    //
    // Picking an existing workspace fills the name field. Clearing the select must not wipe a name
    // the user typed, though, so an empty selection is ignored:
    //
    private async Task SelectExistingWorkspace(string workspaceName)
    {
        if (string.IsNullOrWhiteSpace(workspaceName))
            return;

        await this.SetWorkspaceName(workspaceName);
    }

    private async Task SetWorkspaceName(string workspaceName)
    {
        this.WorkspaceName = workspaceName;
        await this.WorkspaceNameChanged.InvokeAsync(workspaceName);
    }

    private async Task SetProviderId(string providerId)
    {
        this.ProviderId = providerId;
        await this.ProviderIdChanged.InvokeAsync(providerId);
    }

    private async Task SetProfileId(string profileId)
    {
        this.ProfileId = profileId;
        await this.ProfileIdChanged.InvokeAsync(profileId);
    }

    private async Task SetChatTemplateId(string chatTemplateId)
    {
        this.ChatTemplateId = chatTemplateId;
        await this.ChatTemplateIdChanged.InvokeAsync(chatTemplateId);
    }

    //
    // MudSelect hands out its selection as a lazy sequence of nullable strings. We materialize it
    // once and drop empty entries, so the host always receives a stable list of usable IDs:
    //
    private async Task SetDataSourceIds(IEnumerable<string?>? dataSourceIds)
    {
        var selectedDataSourceIds = dataSourceIds is null ? [] : dataSourceIds.Where(id => !string.IsNullOrWhiteSpace(id)).Select(id => id!).ToArray();

        this.DataSourceIds = selectedDataSourceIds;
        await this.DataSourceIdsChanged.InvokeAsync(selectedDataSourceIds);
    }

    private async Task SetToolIds(HashSet<string> toolIds)
    {
        this.ToolIds = toolIds;
        await this.ToolIdsChanged.InvokeAsync(toolIds);
    }

    private string GetSelectedDataSourceText(List<string?>? selectedValues)
    {
        if (selectedValues is null || selectedValues.Count == 0)
            return T("Use the normal chat data source defaults");

        return string.Format(T("{0} data source(s) selected"), selectedValues.Count);
    }
}