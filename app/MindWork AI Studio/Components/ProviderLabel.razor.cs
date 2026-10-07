using AIStudio.Provider;

using Microsoft.AspNetCore.Components;

namespace AIStudio.Components;

/// <summary>
/// Shows a provider icon next to its name.
/// </summary>
/// <remarks>
/// Providers appear in select items, in table cells, and in group headers. All of them need the
/// same icon and text pairing, so this component owns that layout once instead of repeating it at
/// every call site.
/// </remarks>
public partial class ProviderLabel : ComponentBase
{
    /// <summary>
    /// The configured provider whose icon should be shown. Takes precedence over ProviderType.
    /// </summary>
    [Parameter]
    public AIStudio.Settings.Provider? ProviderSettings { get; set; }

    /// <summary>
    /// The LLM provider whose icon should be shown when no ProviderSettings was given.
    /// </summary>
    [Parameter]
    public LLMProviders ProviderType { get; set; } = LLMProviders.NONE;

    /// <summary>
    /// The validated custom icon supplied by a configuration plugin.
    /// </summary>
    [Parameter]
    public string CustomIconDataUrl { get; set; } = string.Empty;

    /// <summary>
    /// The text shown next to the icon.
    /// </summary>
    [Parameter]
    public string Text { get; set; } = string.Empty;

    /// <summary>
    /// Additional CSS class for the text.
    /// </summary>
    [Parameter]
    public string TextClass { get; set; } = string.Empty;
}