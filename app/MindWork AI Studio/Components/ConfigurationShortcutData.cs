using AIStudio.Tools.Rust;

namespace AIStudio.Components;

/// <summary>
/// UI binding data for a configurable keyboard shortcut.
/// </summary>
public sealed class ConfigurationShortcutData
{
    /// <summary>
    /// Empty shortcut binding.
    /// </summary>
    public static ConfigurationShortcutData Empty { get; } = new();

    /// <summary>
    /// The name/identifier of the shortcut, used for conflict detection and registration.
    /// </summary>
    public Shortcut Id { get; init; } = Shortcut.NONE;

    /// <summary>
    /// The current shortcut value.
    /// </summary>
    public Func<string> Value { get; init; } = () => string.Empty;

    /// <summary>
    /// An action that is called when the shortcut was changed.
    /// </summary>
    public Action<string> ValueUpdate { get; init; } = _ => { };

    /// <summary>
    /// The optional user-facing shortcut label.
    /// </summary>
    public Func<string> DisplayName { get; init; } = () => string.Empty;

    /// <summary>
    /// The canonical shortcut value the optional user-facing label belongs to.
    /// </summary>
    public Func<string> DisplaySource { get; init; } = () => string.Empty;

    /// <summary>
    /// An action that is called when the user-facing shortcut label was changed.
    /// </summary>
    public Action<string, string> DisplayUpdate { get; init; } = (_, _) => { };
}