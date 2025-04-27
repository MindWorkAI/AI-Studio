using Microsoft.AspNetCore.Components;

namespace AIStudio.Components;

public partial class Changelog : MSGComponentBase
{
    [Inject]
    private HttpClient HttpClient { get; set; } = null!;

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
        using var response = await this.HttpClient.GetAsync($"changelog/{this.SelectedLog.Filename}");
        this.LogContent = await response.Content.ReadAsStringAsync();
    }
}