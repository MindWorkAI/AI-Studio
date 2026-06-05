using AIStudio.Components;

using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

namespace AIStudio.Dialogs;

public partial class WorkspaceSelectionDialog : MSGComponentBase
{
    private readonly record struct WorkspaceSelectionItem(Guid WorkspaceId, string Name);

    [CascadingParameter]
    private IMudDialogInstance MudDialog { get; set; } = null!;

    [Parameter]
    public string Message { get; set; } = string.Empty;
    
    [Parameter]
    public Guid SelectedWorkspace { get; set; } = Guid.Empty;
    
    [Parameter]
    public string ConfirmText { get; set; } = "OK";
    
    private readonly List<WorkspaceSelectionItem> workspaces = [];
    private MudForm createWorkspaceForm = null!;
    private Guid selectedWorkspace;
    private string newWorkspaceName = string.Empty;
    private bool isCreatingWorkspace;
    private string? createWorkspaceError;
    private string? createWorkspaceErrorName;

    #region Overrides of ComponentBase

    protected override async Task OnInitializedAsync()
    {
        this.selectedWorkspace = this.SelectedWorkspace;

        var snapshot = await WorkspaceBehaviour.GetOrLoadWorkspaceTreeShellAsync();
        foreach (var workspace in snapshot.Workspaces)
            this.workspaces.Add(new(workspace.WorkspaceId, workspace.Name));

        this.StateHasChanged();
        await base.OnInitializedAsync();
    }

    #endregion

    private string? ValidateNewWorkspaceName(string? workspaceName)
    {
        var normalizedWorkspaceName = WorkspaceBehaviour.NormalizeWorkspaceName(workspaceName ?? string.Empty);
        if (string.IsNullOrWhiteSpace(normalizedWorkspaceName))
            return T("Please enter a workspace name.");

        if (this.IsWorkspaceNameExisting(normalizedWorkspaceName))
            return T("There is already a workspace with this name. Please choose a different name.");

        if (this.createWorkspaceError is not null && string.Equals(this.createWorkspaceErrorName, normalizedWorkspaceName, StringComparison.OrdinalIgnoreCase))
            return this.createWorkspaceError;

        return null;
    }

    private bool IsWorkspaceNameExisting(string normalizedWorkspaceName)
    {
        return this.workspaces.Any(workspace =>
            string.Equals(WorkspaceBehaviour.NormalizeWorkspaceName(workspace.Name), normalizedWorkspaceName, StringComparison.OrdinalIgnoreCase));
    }

    private async Task HandleNewWorkspaceNameKeyDown(KeyboardEventArgs keyEvent)
    {
        var key = keyEvent.Key.ToLowerInvariant();
        var code = keyEvent.Code.ToLowerInvariant();
        if (key is not "enter" && code is not "enter" and not "numpadenter")
            return;

        if (keyEvent is { AltKey: true } or { CtrlKey: true } or { MetaKey: true })
            return;

        await this.CreateWorkspaceAsync();
    }

    private async Task CreateWorkspaceAsync()
    {
        this.createWorkspaceError = null;
        this.createWorkspaceErrorName = null;
        await this.createWorkspaceForm.Validate();
        if (!this.createWorkspaceForm.IsValid)
            return;

        this.isCreatingWorkspace = true;
        try
        {
            var workspace = await WorkspaceBehaviour.CreateWorkspaceAsync(this.newWorkspaceName);
            if (workspace is null)
            {
                this.createWorkspaceError = T("There is already a workspace with this name. Please choose a different name.");
                this.createWorkspaceErrorName = WorkspaceBehaviour.NormalizeWorkspaceName(this.newWorkspaceName);
                await this.createWorkspaceForm.Validate();
                return;
            }

            this.workspaces.Add(new(workspace.Value.WorkspaceId, workspace.Value.Name));
            this.selectedWorkspace = workspace.Value.WorkspaceId;
            this.newWorkspaceName = string.Empty;
            this.createWorkspaceForm.ResetValidation();
        }
        finally
        {
            this.isCreatingWorkspace = false;
        }
    }

    private void Cancel() => this.MudDialog.Cancel();
    
    private void Confirm() => this.MudDialog.Close(DialogResult.Ok(this.selectedWorkspace));
}