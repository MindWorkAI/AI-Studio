using AIStudio.Provider;

using Microsoft.AspNetCore.Components;

using MudBlazor;

namespace AIStudio.Settings;

public partial class ProviderDialog : ComponentBase
{
    [CascadingParameter]
    private MudDialogInstance MudDialog { get; set; } = null!;

    private bool dataIsValid;
    private string[] dataIssues = [];
    private string dataInstanceName = string.Empty;
    private Providers dataProvider = Providers.NONE;
    
    private MudForm form = null!;

    private async Task Add()
    {
        await this.form.Validate();
        if (!this.dataIsValid)
            return;
        
        var addedProvider = new Provider
        {
            Id = Guid.NewGuid().ToString(),
            InstanceName = this.dataInstanceName,
            UsedProvider = this.dataProvider,
        };
        
        this.MudDialog.Close(DialogResult.Ok(addedProvider));
    }
    
    private string? ValidatingProvider(Providers provider)
    {
        if (provider == Providers.NONE)
            return "Please select a provider.";
        
        return null;
    }

    private void Cancel() => this.MudDialog.Cancel();
}