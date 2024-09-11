using AIStudio.Provider;
using AIStudio.Settings;

using Microsoft.AspNetCore.Components;

namespace AIStudio.Components;

public partial class ConfidenceInfo : ComponentBase
{
    [Parameter]
    public ConfidenceInfoMode Mode { get; set; } = ConfidenceInfoMode.BUTTON;
    
    [Parameter]
    public Providers Provider { get; set; }
    
    [Inject]
    private SettingsManager SettingsManager { get; init; } = null!;

    private Confidence currentConfidence;
    private bool showConfidence;

    public ConfidenceInfo()
    {
        this.currentConfidence = Providers.NONE.GetConfidence(this.SettingsManager);
    }

    #region Overrides of ComponentBase

    protected override async Task OnParametersSetAsync()
    {
        this.currentConfidence = this.Provider.GetConfidence(this.SettingsManager);
        await base.OnParametersSetAsync();
    }

    #endregion
    
    private void ToggleConfidence()
    {
        this.showConfidence = !this.showConfidence;
    }
    
    private void HideConfidence()
    {
        this.showConfidence = false;
    }
    
    private IEnumerable<(string Index, string Source)> GetConfidenceSources()
    {
        var index = 0;
        foreach (var source in this.currentConfidence.Sources)
            yield return ($"Source {++index}", source);
    }

    private string GetCurrentConfidenceColor() => $"color: {this.currentConfidence.Level.GetColor()};";
    
    private string GetPopoverStyle() => $"border-color: {this.currentConfidence.Level.GetColor()}; max-width: calc(35vw);";
}