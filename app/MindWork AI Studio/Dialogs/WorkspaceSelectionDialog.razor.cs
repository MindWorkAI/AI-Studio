using AIStudio.Components;

using Microsoft.AspNetCore.Components;

namespace AIStudio.Dialogs;

public partial class WorkspaceSelectionDialog : MSGComponentBase
{
    [CascadingParameter]
    private IMudDialogInstance MudDialog { get; set; } = null!;

    [Parameter]
    public string Message { get; set; } = string.Empty;
    
    [Parameter]
    public Guid SelectedWorkspace { get; set; } = Guid.Empty;
    
    [Parameter]
    public string ConfirmText { get; set; } = "OK";
    
    private readonly Dictionary<string, Guid> workspaces = new();
    private Guid selectedWorkspace;

    #region Overrides of ComponentBase

    protected override async Task OnInitializedAsync()
    {
        this.selectedWorkspace = this.SelectedWorkspace;

        var snapshot = await WorkspaceBehaviour.GetOrLoadWorkspaceTreeShellAsync();
        foreach (var workspace in snapshot.Workspaces)
            this.workspaces[workspace.Name] = workspace.WorkspaceId;

        this.StateHasChanged();
        await base.OnInitializedAsync();
    }

    #endregion

    private void Cancel() => this.MudDialog.Cancel();
    
    private void Confirm() => this.MudDialog.Close(DialogResult.Ok(this.selectedWorkspace));
}