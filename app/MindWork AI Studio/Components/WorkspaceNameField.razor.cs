using Microsoft.AspNetCore.Components;

namespace AIStudio.Components;

/// <summary>
/// Names a workspace by its name: an existing one to pick from, or a new name to type.
/// </summary>
/// <remarks>
/// Assistant chat launchers and chat templates both name their workspace this way, because a
/// workspace has an ID of its own on every computer, and both create it when it does not exist
/// yet. The field offers the existing workspaces only as a shortcut; the name it hands out is
/// whatever the text field says. All texts come from the form around it, which knows what the
/// name is used for.
/// </remarks>
public partial class WorkspaceNameField : MSGComponentBase
{
    /// <summary>
    /// The name of the workspace. An empty name is up to the form around the field, e.g., a
    /// disappearing chat for an assistant chat launcher.
    /// </summary>
    [Parameter]
    public string WorkspaceName { get; set; } = string.Empty;

    [Parameter]
    public EventCallback<string> WorkspaceNameChanged { get; set; }

    /// <summary>
    /// The label of the selection of existing workspaces.
    /// </summary>
    [Parameter]
    public string SelectLabel { get; set; } = string.Empty;

    /// <summary>
    /// The label of the text field for the name.
    /// </summary>
    [Parameter]
    public string Label { get; set; } = string.Empty;

    [Parameter]
    public string HelperText { get; set; } = string.Empty;

    [Parameter]
    public string AdornmentIcon { get; set; } = Icons.Material.Filled.CreateNewFolder;

    [Parameter]
    public Size AdornmentIconSize { get; set; } = Size.Medium;

    /// <summary>
    /// The validation of the name, or null for none.
    /// </summary>
    [Parameter]
    public Func<string, string?>? Validation { get; set; }

    /// <summary>
    /// Whether every keystroke counts as a change, e.g., so that a validation follows the typing.
    /// </summary>
    [Parameter]
    public bool Immediate { get; set; }

    /// <summary>
    /// Shows the name without letting the user change it. The selection of existing workspaces
    /// is left out then, since picking from it would change the name.
    /// </summary>
    [Parameter]
    public bool ReadOnly { get; set; }

    [Parameter]
    public string SelectClass { get; set; } = "mb-3 rounded-lg";

    [Parameter]
    public string TextFieldClass { get; set; } = "mb-3";

    private static readonly Dictionary<string, object?> SPELLCHECK_ATTRIBUTES = new();

    private IReadOnlyList<WorkspaceTreeWorkspace> availableWorkspaces = [];

    #region Overrides of MSGComponentBase

    protected override async Task OnInitializedAsync()
    {
        this.SettingsManager.InjectSpellchecking(SPELLCHECK_ATTRIBUTES);
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
}