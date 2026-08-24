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

    /// <summary>
    /// An asynchronous action that is called when the selection changes.
    /// </summary>
    [Parameter]
    public Func<TConfig, Task> SelectionUpdateAsync { get; set; } = _ => Task.CompletedTask;

    /// <summary>
    /// Optional template used to render an item in the list.
    /// </summary>
    [Parameter]
    public RenderFragment<ConfigurationSelectData<TConfig>>? ItemTemplate { get; set; }

    /// <summary>
    /// Additional CSS class for the select element.
    /// </summary>
    [Parameter]
    public string SelectClass { get; set; } = string.Empty;

    /// <summary>
    /// Additional inline style for the select element.
    /// </summary>
    [Parameter]
    public string SelectStyle { get; set; } = string.Empty;

    private string SelectCssClass => $"rounded-lg mb-0 {this.SelectClass}".Trim();
    
    #region Overrides of ConfigurationBase

    /// <inheritdoc />
    protected override bool Stretch => true;

    /// <inheritdoc />
    protected override string Label => this.OptionDescription;

    /// <inheritdoc />
    protected override Variant Variant => Variant.Outlined;

    #endregion
    
    private async Task OptionChanged(TConfig updatedValue)
    {
        this.SelectionUpdate(updatedValue);
        await this.SelectionUpdateAsync(updatedValue);
        await this.SettingsManager.StoreSettings();
        await this.InformAboutChange();
    }
}
