namespace AIStudio.Provider;

/// <summary>
/// Describes whether the provider selection should show the reasoning capability icon.
/// </summary>
public enum ReasoningIndicatorState
{
    /// <summary>
    /// Do not show a reasoning indicator for the configured provider.
    /// </summary>
    NONE,

    /// <summary>
    /// Show that the selected model always performs reasoning.
    /// </summary>
    ALWAYS_ON,

    /// <summary>
    /// Show that reasoning is enabled by the provider or model default.
    /// </summary>
    DEFAULT_ON,

    /// <summary>
    /// Show that reasoning was explicitly enabled through the provider settings.
    /// </summary>
    CONFIGURED,
}