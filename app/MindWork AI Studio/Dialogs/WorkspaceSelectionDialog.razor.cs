using System.Text;

using AIStudio.Components;
using AIStudio.Settings;

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
        
        // Get the workspace root directory: 
        var workspaceDirectories = Path.Join(SettingsManager.DataDirectory, "workspaces");
        if(!Directory.Exists(workspaceDirectories))
        {
            await base.OnInitializedAsync();
            return;
        }

        // Enumerate the workspace directories:
        foreach (var workspaceDirPath in Directory.EnumerateDirectories(workspaceDirectories))
        {
            // Read the `name` file:
            var workspaceNamePath = Path.Join(workspaceDirPath, "name");
            var workspaceName = await File.ReadAllTextAsync(workspaceNamePath, Encoding.UTF8);
            
            // Add the workspace to the list:
            this.workspaces.Add(workspaceName, Guid.Parse(Path.GetFileName(workspaceDirPath)));
        }

        this.StateHasChanged();
        await base.OnInitializedAsync();
    }

    #endregion

    private void Cancel() => this.MudDialog.Cancel();
    
    private void Confirm() => this.MudDialog.Close(DialogResult.Ok(this.selectedWorkspace));
}