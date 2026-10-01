using AIStudio.Provider;
using AIStudio.Settings;

using Microsoft.AspNetCore.Components;

namespace AIStudio.Components;

/// <summary>
/// Shows the icon of a provider.
/// </summary>
/// <remarks>
/// The icon is rendered as an image instead of inline SVG. That way the browser treats the icon as
/// a standalone, script-less document, which matters for the custom icons a configuration plugin
/// may supply.
/// </remarks>
public partial class ProviderIcon : ComponentBase
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
    /// Additional CSS class for the icon.
    /// </summary>
    [Parameter]
    public string Class { get; set; } = string.Empty;

    /// <summary>
    /// Additional inline style for the icon.
    /// </summary>
    [Parameter]
    public string Style { get; set; } = string.Empty;

    [Inject]
    private SettingsManager SettingsManager { get; init; } = null!;

    /// <summary>
    /// The provider-icon class carries the sizing from app.css. Callers add to it instead of
    /// replacing it, so an icon cannot lose its size by setting a class of its own.
    /// </summary>
    private string CssClass => $"provider-icon {this.Class}".TrimEnd();

    private string IconUrl => this.ProviderSettings?.GetIconUrl(this.SettingsManager.IsDarkMode)
                              ?? this.ProviderType.GetIconUrl(this.SettingsManager.IsDarkMode, this.CustomIconDataUrl);
}