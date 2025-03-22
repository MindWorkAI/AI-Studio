namespace AIStudio.Tools.PluginSystem;

/// <summary>
/// Represents the result of a plugin check.
/// </summary>
/// <param name="IsForbidden">In case the plugin is forbidden, this is true.</param>
/// <param name="Message">The message that describes why the plugin is forbidden.</param>
public readonly record struct PluginCheckResult(bool IsForbidden, string? Message);