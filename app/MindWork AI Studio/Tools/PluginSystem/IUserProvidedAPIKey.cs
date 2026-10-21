namespace AIStudio.Tools.PluginSystem;

/// <summary>
/// Represents a configuration object whose API key is managed by the user, although the object
/// itself is managed by a configuration plugin. Implemented by all provider kinds which support
/// the options "AllowUserProvidedAPIKey" and "PromptForUserAPIKey", i.e., LLM, embedding, and
/// transcription providers. UserProvidedAPIKeyOptions reads both options from a plugin.
/// </summary>
public interface IUserProvidedAPIKey
{
    /// <summary>
    /// When set by a configuration plugin, the user may set their own API key for this otherwise
    /// locked, enterprise-managed object.
    /// </summary>
    public bool AllowUserProvidedAPIKey { get; }

    /// <summary>
    /// When set by a configuration plugin, AI Studio asks the user for the API key of this object
    /// on the home page as long as none is stored. Implies AllowUserProvidedAPIKey.
    /// </summary>
    public bool PromptForUserAPIKey { get; }
}