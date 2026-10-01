namespace AIStudio.Tools.PluginSystem;

/// <summary>
/// Represents a configuration object whose API key is managed by the user, although the object
/// itself is managed by a configuration plugin. Implemented by all provider kinds which support
/// the "AllowUserProvidedAPIKey" option, i.e., LLM, embedding, and transcription providers.
/// </summary>
public interface IUserProvidedAPIKey
{
    /// <summary>
    /// When set by a configuration plugin, the user may set their own API key for this otherwise
    /// locked, enterprise-managed object.
    /// </summary>
    public bool AllowUserProvidedAPIKey { get; }
}