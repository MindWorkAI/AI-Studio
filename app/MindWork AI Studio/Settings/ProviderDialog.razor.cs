using System.Text.RegularExpressions;

using AIStudio.Provider;

using Microsoft.AspNetCore.Components;

using MudBlazor;

namespace AIStudio.Settings;

public partial class ProviderDialog : ComponentBase
{
    [CascadingParameter]
    private MudDialogInstance MudDialog { get; set; } = null!;
    
    [Parameter]
    public IList<string> UsedInstanceNames { get; set; } = new List<string>();

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
    
    private string? ValidatingInstanceName(string instanceName)
    {
        if(instanceName.StartsWith(' '))
            return "The instance name must not start with a space.";
        
        if(instanceName.EndsWith(' '))
            return "The instance name must not end with a space.";
        
        // The instance name must only contain letters, numbers, and spaces:
        if (!InstanceNameRegex().IsMatch(instanceName))
            return "The instance name must only contain letters, numbers, and spaces.";
        
        if(instanceName.Contains("  "))
            return "The instance name must not contain consecutive spaces.";
        
        // The instance name must be unique:
        if (this.UsedInstanceNames.Contains(instanceName.ToLowerInvariant()))
            return "The instance name must be unique; the chosen name is already in use.";
        
        return null;
    }

    private void Cancel() => this.MudDialog.Cancel();
    
    [GeneratedRegex("^[a-zA-Z0-9 ]+$")]
    private static partial Regex InstanceNameRegex();
}