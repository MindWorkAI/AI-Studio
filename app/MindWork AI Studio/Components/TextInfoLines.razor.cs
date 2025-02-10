using AIStudio.Settings;

using Microsoft.AspNetCore.Components;

namespace AIStudio.Components;

public partial class TextInfoLines : ComponentBase
{
    [Parameter]
    public string Label { get; set; } = string.Empty;

    [Parameter]
    public string Value { get; set; } = string.Empty;

    [Parameter]
    public string ClipboardTooltipSubject { get; set; } = "the text";

    [Parameter]
    public int MaxLines { get; set; } = 30;

    [Parameter]
    public bool ShowingCopyButton { get; set; } = true;

    [Parameter]
    public TextColor Color { get; set; } = TextColor.DEFAULT;
    
    [Inject]
    private RustService RustService { get; init; } = null!;
    
    [Inject]
    private ISnackbar Snackbar { get; init; } = null!;
    
    [Inject]
    private SettingsManager SettingsManager { get; init; } = null!;

    #region Overrides of ComponentBase

    protected override async Task OnInitializedAsync()
    {
        // Configure the spellchecking for the user input:
        this.SettingsManager.InjectSpellchecking(USER_INPUT_ATTRIBUTES);
        
        await base.OnInitializedAsync();
    }

    #endregion

    private static readonly Dictionary<string, object?> USER_INPUT_ATTRIBUTES = new();
    
    private string ClipboardTooltip => $"Copy {this.ClipboardTooltipSubject} to the clipboard";
    
    private async Task CopyToClipboard(string content) => await this.RustService.CopyText2Clipboard(this.Snackbar, content);
    
    private string GetColor()
    {
        var htmlColorCode = this.Color.GetHTMLColor(this.SettingsManager);
        if(string.IsNullOrWhiteSpace(htmlColorCode))
            return string.Empty;
        
        return $"color: {htmlColorCode} !important;";
    }
}