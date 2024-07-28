using Microsoft.AspNetCore.Components;

namespace AIStudio.Components.Blocks;

public partial class ConfigurationText : ConfigurationBase
{
    /// <summary>
    /// The text used for the textfield.
    /// </summary>
    [Parameter]
    public Func<string> Text { get; set; } = () => string.Empty;
    
    /// <summary>
    /// An action which is called when the option is changed.
    /// </summary>
    [Parameter]
    public Action<string> TextUpdate { get; set; } = _ => { };

    /// <summary>
    /// The icon to display next to the textfield.
    /// </summary>
    [Parameter]
    public string Icon { get; set; } = Icons.Material.Filled.Info;

    /// <summary>
    /// The color of the icon to use.
    /// </summary>
    [Parameter]
    public Color IconColor { get; set; } = Color.Default;
    
    private async Task OptionChanged(string updatedText)
    {
        this.TextUpdate(updatedText);
        await this.SettingsManager.StoreSettings();
        await this.InformAboutChange();
    }
}