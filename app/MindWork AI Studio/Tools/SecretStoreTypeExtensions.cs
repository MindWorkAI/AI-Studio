namespace AIStudio.Tools;

public static class SecretStoreTypeExtensions
{
    /// <param name="type">The SecretStoreType enum value.</param>
    extension(SecretStoreType type)
    {
        /// <summary>
        /// Gets the prefix string associated with the SecretStoreType.
        /// </summary>
        /// <remarks>
        /// LLM_PROVIDER uses the legacy "provider" prefix for backward compatibility.
        /// </remarks>
        /// <value>The corresponding prefix string.</value>
        public string Prefix => type switch
        {
            SecretStoreType.LLM_PROVIDER => "provider",
            SecretStoreType.EMBEDDING_PROVIDER => "embedding",
            SecretStoreType.TRANSCRIPTION_PROVIDER => "transcription",
            SecretStoreType.IMAGE_PROVIDER => "image",
            SecretStoreType.DATA_SOURCE => "data-source",
            SecretStoreType.TOOL_SETTINGS => "tool",
        
            _ => "provider",
        };
    }
}
