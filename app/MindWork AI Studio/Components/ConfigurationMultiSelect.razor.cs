using AIStudio.Settings;

using Microsoft.AspNetCore.Components;

namespace AIStudio.Components;

/// <summary>
/// Configuration component for selecting many values from a list.
/// </summary>
/// <typeparam name="TData">The type of the value to select.</typeparam>
public partial class ConfigurationMultiSelect<TData> : ConfigurationBaseCore
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

    /// <summary>
    /// Determines whether a specific item is locked by a configuration plugin.
    /// </summary>
    [Parameter]
    public Func<TData, bool> IsItemLocked { get; set; } = _ => false;

    [Parameter]
    public string? EmptySelectionText { get; set; }

    [Parameter]
    public string? SingleSelectionText { get; set; }

    [Parameter]
    public string? MultipleSelectionText { get; set; }
    
    #region Overrides of ConfigurationBase

    /// <inheritdoc />
    protected override bool Stretch => true;

    /// <inheritdoc />
    protected override Variant Variant => Variant.Outlined;

    /// <inheritdoc />
    protected override string Label => this.OptionDescription;

    #endregion
    
    private async Task OptionChanged(IEnumerable<TData?>? updatedValues)
    {
        if(updatedValues is null)
            this.SelectionUpdate([]);
        else
            this.SelectionUpdate(updatedValues.Where(n => n is not null).ToHashSet()!);
        
        await this.SettingsManager.StoreSettings();
        await this.InformAboutChange();
    }
    
    private string GetMultiSelectionText(List<TData?>? selectedValues)
    {
        if(selectedValues is null || selectedValues.Count == 0)
            return this.EmptySelectionText ?? T("No items selected.");
        
        if(selectedValues.Count == 1)
            return this.SingleSelectionText ?? T("You have selected 1 item.");
        
        return string.Format(this.MultipleSelectionText ?? T("You have selected {0} items."), selectedValues.Count);
    }

    private bool IsLockedValue(TData value) => this.IsItemLocked(value);

    private string LockedTooltip() =>
        this.T(
            "This feature is managed by your organization and has therefore been disabled.",
            typeof(ConfigurationBase).Namespace,
            nameof(ConfigurationBase));
}
