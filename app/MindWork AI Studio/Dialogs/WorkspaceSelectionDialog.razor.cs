using AIStudio.Components;

using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

namespace AIStudio.Dialogs;

public partial class WorkspaceSelectionDialog : MSGComponentBase
{
    private readonly record struct WorkspaceSelectionItem(Guid WorkspaceId, string Name);

    [CascadingParameter]
    private IMudDialogInstance MudDialog { get; set; } = null!;

    [Inject]
    private IJSRuntime JsRuntime { get; init; } = null!;

    [Parameter]
    public string Message { get; set; } = string.Empty;
    
    [Parameter]
    public Guid SelectedWorkspace { get; set; } = Guid.Empty;
    
    [Parameter]
    public string ConfirmText { get; set; } = "OK";
    
    private readonly List<WorkspaceSelectionItem> workspaces = [];
    private readonly string escapeHandlerId = $"workspace-selection-dialog-{Guid.NewGuid():N}";
    private MudForm? createWorkspaceForm;
    private MudTextField<string>? newWorkspaceNameField;
    private DotNetObjectReference<WorkspaceSelectionDialog>? dotNetReference;
    private Guid selectedWorkspace;
    private string newWorkspaceName = string.Empty;
    private bool isCreatingWorkspace;
    private bool showCreateWorkspaceForm;
    private bool shouldFocusNewWorkspaceName;
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

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            this.dotNetReference = DotNetObjectReference.Create(this);
            await this.JsRuntime.InvokeVoidAsync("registerEscapeHandler", this.escapeHandlerId, this.dotNetReference);
        }

        if (this.shouldFocusNewWorkspaceName && this.newWorkspaceNameField is not null)
        {
            this.shouldFocusNewWorkspaceName = false;
            await this.newWorkspaceNameField.FocusAsync();
        }

        await base.OnAfterRenderAsync(firstRender);
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

    private void ShowCreateWorkspaceForm()
    {
        this.createWorkspaceError = null;
        this.createWorkspaceErrorName = null;
        this.newWorkspaceName = string.Empty;
        this.showCreateWorkspaceForm = true;
        this.shouldFocusNewWorkspaceName = true;
    }

    private async Task CreateWorkspaceAsync()
    {
        if (this.createWorkspaceForm is null)
            return;

        this.createWorkspaceError = null;
        this.createWorkspaceErrorName = null;
        await this.createWorkspaceForm.Validate();
        if (!this.createWorkspaceForm.IsValid)
            return;

        this.isCreatingWorkspace = true;
        try
        {
            var result = await WorkspaceBehaviour.TryCreateWorkspaceAsync(this.newWorkspaceName);
            if (!result.Success)
            {
                this.createWorkspaceError = T("There is already a workspace with this name. Please choose a different name.");
                this.createWorkspaceErrorName = WorkspaceBehaviour.NormalizeWorkspaceName(this.newWorkspaceName);
                await this.createWorkspaceForm.Validate();
                return;
            }

            this.workspaces.Add(new(result.Workspace.WorkspaceId, result.Workspace.Name));
            this.selectedWorkspace = result.Workspace.WorkspaceId;
            this.newWorkspaceName = string.Empty;
            this.createWorkspaceForm?.ResetValidation();
            this.showCreateWorkspaceForm = false;
            await this.SendMessage(Event.WORKSPACE_CREATED, result.Workspace.WorkspaceId);
        }
        finally
        {
            this.isCreatingWorkspace = false;
        }
    }

    private void Cancel()
    {
        if (!this.showCreateWorkspaceForm)
        {
            this.MudDialog.Cancel();
            return;
        }

        this.createWorkspaceError = null;
        this.createWorkspaceErrorName = null;
        this.newWorkspaceName = string.Empty;
        this.createWorkspaceForm?.ResetValidation();
        this.showCreateWorkspaceForm = false;
        this.shouldFocusNewWorkspaceName = false;
    }

    [JSInvokable]
    public async Task HandleEscapeKeyAsync()
    {
        await this.InvokeAsync(() =>
        {
            this.Cancel();
            this.StateHasChanged();
        });
    }
    
    private void Confirm() => this.MudDialog.Close(DialogResult.Ok(this.selectedWorkspace));

    #region Overrides of MSGComponentBase

    protected override void DisposeResources()
    {
        try
        {
            _ = this.JsRuntime.InvokeVoidAsync("unregisterEscapeHandler", this.escapeHandlerId).AsTask();
        }
        catch
        {
            // Ignore JS cleanup errors while the dialog is being disposed.
        }

        this.dotNetReference?.Dispose();
        this.dotNetReference = null;

        base.DisposeResources();
    }

    #endregion
}