using Microsoft.AspNetCore.Components;

namespace AIStudio.Components;

public partial class ConfigurationTrigger : ConfigurationBase
{
    [Parameter]
    public string TriggerText { get; set; } = string.Empty;

    [Parameter]
    public string TriggerIcon { get; set; } = Icons.Material.Filled.AddBox;
    
    [Parameter]
    public Action OnClickSync { get; set; } = () => { };
    
    [Parameter]
    public Func<Task> OnClickAsync { get; set; } = () => Task.CompletedTask;

    private async Task Click()
    {
        this.OnClickSync();
        await this.OnClickAsync();
    }
}