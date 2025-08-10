using AIStudio.Settings;

using Microsoft.AspNetCore.Components;

namespace AIStudio.Components;

/// <summary>
/// Configuration component for selecting a value from a list.
/// </summary>
/// <typeparam name="TConfig">The type of the value to select.</typeparam>
public partial class ConfigurationSelect<TConfig> : ConfigurationBaseCore
{
    /// <summary>
    /// The data to select from.
    /// </summary>
    [Parameter]
    public IEnumerable<ConfigurationSelectData<TConfig>> Data { get; set; } = [];
    
    /// <summary>
    /// The selected value.
    /// </summary>
    [Parameter]
    public Func<TConfig> SelectedValue { get; set; } = () => default!;
    
    /// <summary>
    /// An action that is called when the selection changes.
    /// </summary>
    [Parameter]
    public Action<TConfig> SelectionUpdate { get; set; } = _ => { };
    
    #region Overrides of ConfigurationBase

    /// <inheritdoc />
    protected override bool Stretch => true;

    /// <inheritdoc />
    protected override string Label => this.OptionDescription;

    protected override Variant Variant => Variant.Outlined;

    #endregion
    
    private async Task OptionChanged(TConfig updatedValue)
    {
        this.SelectionUpdate(updatedValue);
        await this.SettingsManager.StoreSettings();
        await this.InformAboutChange();
    }
}