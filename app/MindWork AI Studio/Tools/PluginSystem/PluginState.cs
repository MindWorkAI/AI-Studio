namespace AIStudio.Tools.PluginSystem;

/// <summary>
/// Represents the state of a plugin.
/// </summary>
/// <param name="Valid">True, when the plugin is valid.</param>
/// <param name="Message">When the plugin is invalid, this contains the error message.</param>
public readonly record struct PluginState(bool Valid, string Message);