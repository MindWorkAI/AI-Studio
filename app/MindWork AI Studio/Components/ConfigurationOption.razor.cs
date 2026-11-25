using Microsoft.AspNetCore.Components;

namespace AIStudio.Components;

/// <summary>
/// Configuration component for any boolean option.
/// </summary>
public partial class ConfigurationOption : ConfigurationBaseCore
{
    /// <summary>
    /// Text to display when the option is true.
    /// </summary>
    [Parameter]
    public string LabelOn { get; set; } = string.Empty;
    
    /// <summary>
    /// Text to display when the option is false.
    /// </summary>
    [Parameter]
    public string LabelOff { get; set; } = string.Empty;
    
    /// <summary>
    /// The boolean state of the option.
    /// </summary>
    [Parameter]
    public Func<bool> State { get; set; } = () => false;
    
    /// <summary>
    /// An action which is called when the option is changed.
    /// </summary>
    [Parameter]
    public Action<bool> StateUpdate { get; set; } = _ => { };

    /// <summary>
    /// Asks before the option takes the new state, e.g., whether the user really wants to turn off a protection.
    /// </summary>
    /// <remarks>
    /// It gets the new state and returns false to keep the old one.
    /// </remarks>
    [Parameter]
    public Func<bool, Task<bool>> ConfirmChange { get; set; } = _ => Task.FromResult(true);

    /// <summary>
    /// The key of the switch, which changes whenever a change was rejected.
    /// </summary>
    /// <remarks>
    /// The switch shows the state the user clicked, until its value changes. A rejected change leaves the value
    /// as it was, so the switch is built anew to show it.
    /// </remarks>
    private int switchKey;

    #region Overrides of ConfigurationBase

    /// <inheritdoc />
    protected override bool Stretch => true;

    /// <inheritdoc />
    protected override Variant Variant => Variant.Outlined;

    /// <inheritdoc />
    protected override string Label => this.OptionDescription;

    #endregion
    
    private async Task OptionChanged(bool updatedState)
    {
        if (!await this.ConfirmChange(updatedState))
        {
            this.switchKey++;
            return;
        }

        this.StateUpdate(updatedState);
        await this.SettingsManager.StoreSettings();
        await this.InformAboutChange();
    }
}