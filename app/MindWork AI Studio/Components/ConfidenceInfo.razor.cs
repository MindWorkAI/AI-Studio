using AIStudio.Provider;

using Microsoft.AspNetCore.Components;

namespace AIStudio.Components;

public partial class ConfidenceInfo : MSGComponentBase
{
    [Parameter]
    public PopoverTriggerMode Mode { get; set; } = PopoverTriggerMode.BUTTON;
    
    [Parameter]
    public LLMProviders LLMProvider { get; set; }

    private Confidence currentConfidence;
    private bool showConfidence;

    public ConfidenceInfo()
    {
        this.currentConfidence = LLMProviders.NONE.GetConfidence(this.SettingsManager);
    }

    #region Overrides of ComponentBase

    protected override async Task OnParametersSetAsync()
    {
        this.currentConfidence = this.LLMProvider.GetConfidence(this.SettingsManager);
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
            yield return (string.Format(T("Source {0}"), ++index), source);
    }

    private string GetCurrentConfidenceColor() => $"color: {this.currentConfidence.Level.GetColor(this.SettingsManager)};";
    
    private string GetPopoverStyle() => $"border-color: {this.currentConfidence.Level.GetColor(this.SettingsManager)};";
}