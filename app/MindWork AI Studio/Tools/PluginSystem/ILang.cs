namespace AIStudio.Tools.PluginSystem;

/// <summary>
/// Represents a contract to access text from a language plugin.
/// </summary>
public interface ILang
{
    /// <summary>
    /// Tries to get a text from the language plugin.
    /// </summary>
    /// <remarks>
    /// The given fallback text is used to determine the key for
    /// the language plugin. Base for the key is the namespace of
    /// the using component and the fallback text in English (US).
    /// The given text getting hashed. When the key does not exist,
    /// the fallback text will be returned.
    /// </remarks>
    /// <param name="fallbackEN">The fallback text in English (US).</param>
    /// <returns>The text from the language plugin or the fallback text.</returns>
    public string T(string fallbackEN);
}