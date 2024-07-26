using AIStudio.Settings;

using Microsoft.AspNetCore.Components;

namespace AIStudio.Components;

/// <summary>
/// Configuration component for selecting a value from a list.
/// </summary>
/// <typeparam name="T">The type of the value to select.</typeparam>
public partial class ConfigurationSelect<T> : ConfigurationBase
{
    /// <summary>
    /// The data to select from.
    /// </summary>
    [Parameter]
    public IEnumerable<ConfigurationSelectData<T>> Data { get; set; } = [];
    
    /// <summary>
    /// The selected value.
    /// </summary>
    [Parameter]
    public Func<T> SelectedValue { get; set; } = () => default!;
    
    /// <summary>
    /// An action that is called when the selection changes.
    /// </summary>
    [Parameter]
    public Action<T> SelectionUpdate { get; set; } = _ => { };
    
    private async Task OptionChanged(T updatedValue)
    {
        this.SelectionUpdate(updatedValue);
        await this.SettingsManager.StoreSettings();
        await this.InformAboutChange();
    }
    
    private static string GetClass => $"{MARGIN_CLASS} rounded-lg";
}