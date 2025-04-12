namespace AIStudio.Tools.PluginSystem;

/// <summary>
/// Represents a contract for a language plugin.
/// </summary>
public interface ILanguagePlugin
{
    /// <summary>
    /// Tries to get a text from the language plugin.
    /// </summary>
    /// <remarks>
    /// When the key does not exist, the value will be an empty string.
    /// Please note that the key is case-sensitive. Furthermore, the keys
    /// are in the format "root::key". That means that the keys are
    /// hierarchical and separated by "::".
    /// </remarks>
    /// <param name="key">The key to use to get the text.</param>
    /// <param name="value">The desired text.</param>
    /// <returns>True if the key exists, false otherwise.</returns>
    public bool TryGetText(string key, out string value);
    
    /// <summary>
    /// Gets the IETF tag of the language plugin.
    /// </summary>
    public string IETFTag { get; }
}