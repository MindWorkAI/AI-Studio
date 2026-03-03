namespace AIStudio.Tools;

public static class SecretStoreTypeExtensions
{
    /// <summary>
    /// Gets the prefix string associated with the SecretStoreType.
    /// </summary>
    /// <remarks>
    /// LLM_PROVIDER uses the legacy "provider" prefix for backward compatibility.
    /// </remarks>
    /// <param name="type">The SecretStoreType enum value.</param>
    /// <returns>>The corresponding prefix string.</returns>
    public static string Prefix(this SecretStoreType type) => type switch
    {
        SecretStoreType.LLM_PROVIDER => "provider",
        SecretStoreType.EMBEDDING_PROVIDER => "embedding",
        SecretStoreType.TRANSCRIPTION_PROVIDER => "transcription",
        
        _ => "provider",
    };
}