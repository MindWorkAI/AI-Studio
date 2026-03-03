namespace AIStudio.Tools;

/// <summary>
/// Represents the type of secret store used for API keys.
/// </summary>
/// <remarks>
/// Different provider types use different prefixes for storing API keys.
/// This prevents collisions when the same instance name is used across
/// different provider types (e.g., LLM, Embedding, Transcription).
/// </remarks>
public enum SecretStoreType
{
    /// <summary>
    /// LLM provider secrets. Uses the legacy "provider::" prefix for backward compatibility.
    /// </summary>
    LLM_PROVIDER = 0,

    /// <summary>
    /// Embedding provider secrets. Uses the "embedding::" prefix.
    /// </summary>
    EMBEDDING_PROVIDER,

    /// <summary>
    /// Transcription provider secrets. Uses the "transcription::" prefix.
    /// </summary>
    TRANSCRIPTION_PROVIDER,
    
    /// <summary>
    /// Image provider secrets. Uses the "image::" prefix.
    /// </summary>
    IMAGE_PROVIDER,
}
