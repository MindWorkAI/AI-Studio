using AIStudio.Provider;

using Microsoft.AspNetCore.Components;

namespace AIStudio.Components;

/// <summary>
/// A provider selection with the confidence of the selected provider next to it, for a page which lets the user choose
/// several providers: the confidence card in the footer of an assistant knows only one of them.
/// </summary>
/// <remarks>
/// The confidence shows only when the user wants to see it, see DataConfidence.ShowProviderConfidence. Like
/// ProviderSelection, this takes the component from the cascading parameter of its page.
/// </remarks>
public partial class ProviderSelectionWithConfidence : MSGComponentBase
{
    /// <summary>
    /// Gets or sets the selected provider.
    /// </summary>
    [Parameter]
    public AIStudio.Settings.Provider ProviderSettings { get; set; } = AIStudio.Settings.Provider.NONE;

    /// <summary>
    /// Gets or sets what happens when the user selects another provider.
    /// </summary>
    [Parameter]
    public EventCallback<AIStudio.Settings.Provider> ProviderSettingsChanged { get; set; }

    /// <summary>
    /// Gets or sets the validation of the selected provider, which returns an issue or null.
    /// </summary>
    [Parameter]
    public Func<AIStudio.Settings.Provider, string?> ValidateProvider { get; set; } = _ => null;

    /// <summary>
    /// Gets or sets the confidence a provider needs at least to be offered, when it is higher than the minimum of the
    /// component, see ProviderSelection.ExplicitMinimumConfidence.
    /// </summary>
    [Parameter]
    public ConfidenceLevel ExplicitMinimumConfidence { get; set; } = ConfidenceLevel.UNKNOWN;

    /// <summary>
    /// Gets or sets whether the selection is disabled, see ProviderSelection.Disabled.
    /// </summary>
    [Parameter]
    public bool Disabled { get; set; }
}