using AIStudio.Settings;

using Microsoft.AspNetCore.Components;

namespace AIStudio.Components;

public partial class EnumSelection<T> : EnumSelectionBase where T : struct, Enum
{
    [Parameter]
    public T Value { get; set; }
    
    [Parameter]
    public EventCallback<T> ValueChanged { get; set; }
    
    [Parameter]
    public bool AllowOther { get; set; }
    
    [Parameter]
    public T OtherValue { get; set; }
    
    [Parameter]
    public string OtherInput { get; set; } = string.Empty;
    
    [Parameter]
    public EventCallback<string> OtherInputChanged { get; set; }
    
    [Parameter]
    public string Label { get; set; } = string.Empty;
    
    [Parameter]
    public string LabelOther { get; set; } = "Other";
    
    [Parameter]
    public Func<T, string?> ValidateSelection { get; set; } = _ => null;
    
    [Parameter]
    public Func<string, string?> ValidateOther { get; set; } = _ => null;
    
    [Parameter]
    public string Icon { get; set; } = Icons.Material.Filled.ArrowDropDown;
    
    /// <summary>
    /// Gets or sets the custom name function for selecting the display name of an enum value.
    /// </summary>
    /// <typeparam name="T">The enum type.</typeparam>
    /// <param name="value">The enum value.</param>
    /// <returns>The display name of the enum value.</returns>
    [Parameter]
    public Func<T, string> NameFunc { get; set; } = value => value.ToString();
    
    [Parameter]
    public Func<T, Task> SelectionUpdated { get; set; } = _ => Task.CompletedTask;
    
    [Inject]
    private SettingsManager SettingsManager { get; set; } = null!;
    
    #region Overrides of ComponentBase

    protected override async Task OnInitializedAsync()
    {
        // Configure the spellchecking for the user input:
        this.SettingsManager.InjectSpellchecking(USER_INPUT_ATTRIBUTES);
        await base.OnInitializedAsync();
    }

    #endregion

    private async Task SelectionChanged(T value)
    {
        await this.ValueChanged.InvokeAsync(value);
        await this.SelectionUpdated(value);
    }
    
    private async Task OtherValueChanged(string value)
    {
        await this.OtherInputChanged.InvokeAsync(value);
        await this.SelectionUpdated(this.Value);
    }
}