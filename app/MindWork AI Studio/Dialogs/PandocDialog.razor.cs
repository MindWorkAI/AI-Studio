using Microsoft.AspNetCore.Components;

namespace AIStudio.Dialogs;

public partial class PandocDialog : ComponentBase
{
    [CascadingParameter]
    private IMudDialogInstance MudDialog { get; set; } = null!;

    private bool isPandocAvailable;
    private bool showSkeleton;
    private bool showInstallPage;
    

    #region Overrides of ComponentBase
    
    protected override async Task OnInitializedAsync()
    {
        await base.OnInitializedAsync();
        this.showSkeleton = true;
        await this.CheckPandocAvailabilityAsync();
    }

    #endregion
    
    private void Cancel() => this.MudDialog.Cancel();

    private async Task CheckPandocAvailabilityAsync()
    {
        await Task.Delay(2500);
        this.isPandocAvailable = await Pandoc.CheckAvailabilityAsync();
        this.showSkeleton = false;
        await this.InvokeAsync(this.StateHasChanged);
    }

    private void ProceedToInstallation() => this.showInstallPage = true;
}