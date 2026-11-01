using Microsoft.AspNetCore.Components;

namespace AIStudio.Components;

public partial class Changelog : MSGComponentBase
{
    [Inject]
    private WebRootFiles WebRootFiles { get; set; } = null!;

    #region Overrides of ComponentBase

    protected override async Task OnInitializedAsync()
    {
        await this.ReadLogAsync();
        await base.OnInitializedAsync();
    }

    #endregion

    private Log SelectedLog { get; set; } = LOGS.MaxBy(n => n.Build);
    
    private string LogContent { get; set; } = string.Empty;

    private async Task ReadLogAsync()
    {
        this.LogContent = await this.WebRootFiles.ReadTextAsync($"changelog/{this.SelectedLog.Filename}");
    }
}