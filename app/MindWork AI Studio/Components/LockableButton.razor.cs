using Microsoft.AspNetCore.Components;

namespace AIStudio.Components;

public partial class LockableButton : ConfigurationBaseCore
{
    [Parameter]
    public string Icon { get; set; } = Icons.Material.Filled.Info;
    
    [Parameter]
    public Func<Task> OnClickAsync { get; set; } = () => Task.CompletedTask;
    
    [Parameter]
    public Action OnClick { get; set; } = () => { };
    
    [Parameter]
    public string Text { get; set; } = string.Empty;
    
    private async Task ClickAsync()
    {
        if (this.IsLocked() || this.Disabled())
            return;
        
        await this.OnClickAsync();
        this.OnClick();
    }
}