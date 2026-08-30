using Microsoft.AspNetCore.Components;

namespace AIStudio.Components;

/// <summary>
/// The selection a direct chat launcher needs: the workspace its chat is created in, and the
/// provider, profile, chat template, and data sources that chat starts with.
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
    /// does not exist yet, hence this is a free-text field and not a workspace ID.
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
    /// Validates the workspace name. The hosts differ here: the Builder requires a name only while
    /// its launcher switch is on, whereas the settings dialog always requires one.
    /// </summary>
    [Parameter]
    public Func<string, string?>? ValidateWorkspaceName { get; set; }

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

    private string GetSelectedDataSourceText(List<string?>? selectedValues)
    {
        if (selectedValues is null || selectedValues.Count == 0)
            return T("Use the normal chat data source defaults");

        return string.Format(T("{0} data source(s) selected"), selectedValues.Count);
    }
}