using AIStudio.Settings;

using Microsoft.AspNetCore.Components;

namespace AIStudio.Components;

/// <summary>
/// Configuration component for selecting many values from a list.
/// </summary>
/// <typeparam name="TData">The type of the value to select.</typeparam>
public partial class ConfigurationMultiSelect<TData> : ConfigurationBase
{
    /// <summary>
    /// The data to select from.
    /// </summary>
    [Parameter]
    public IEnumerable<ConfigurationSelectData<TData>> Data { get; set; } = [];
    
    /// <summary>
    /// The selected values.
    /// </summary>
    [Parameter]
    public Func<HashSet<TData>> SelectedValues { get; set; } = () => [];
    
    /// <summary>
    /// An action that is called when the selection changes.
    /// </summary>
    [Parameter]
    public Action<HashSet<TData>> SelectionUpdate { get; set; } = _ => { };
    
    private async Task OptionChanged(IEnumerable<TData?>? updatedValues)
    {
        if(updatedValues is null)
            this.SelectionUpdate([]);
        else
            this.SelectionUpdate(updatedValues.Where(n => n is not null).ToHashSet()!);
        
        await this.SettingsManager.StoreSettings();
        await this.InformAboutChange();
    }
    
    private static string GetClass => $"{MARGIN_CLASS} rounded-lg";
    
    private string GetMultiSelectionText(List<TData?>? selectedValues)
    {
        if(selectedValues is null || selectedValues.Count == 0)
            return T("No preview features selected.");
        
        if(selectedValues.Count == 1)
            return T("You have selected 1 preview feature.");
        
        return string.Format(T("You have selected {0} preview features."), selectedValues.Count);
    }
}