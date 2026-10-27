namespace AIStudio.Tools.PluginSystem;

public static class PluginConfigurationObjectTypeExtensions
{
    private static string TB(string fallbackEN) => I18N.I.T(fallbackEN, typeof(PluginConfigurationObjectTypeExtensions).Namespace, nameof(PluginConfigurationObjectTypeExtensions));

    /// <summary>
    /// The name of a type of configuration object, as users read it, e.g., in a message.
    /// </summary>
    public static string GetName(this PluginConfigurationObjectType type) => type switch
    {
        PluginConfigurationObjectType.LLM_PROVIDER => TB("LLM Provider"),
        PluginConfigurationObjectType.EMBEDDING_PROVIDER => TB("Embedding Provider"),
        PluginConfigurationObjectType.TRANSCRIPTION_PROVIDER => TB("Transcription Provider"),
        PluginConfigurationObjectType.PROFILE => TB("Profile"),
        PluginConfigurationObjectType.CHAT_TEMPLATE => TB("Chat Template"),
        PluginConfigurationObjectType.DATA_SOURCE => TB("Data Source"),
        PluginConfigurationObjectType.DOCUMENT_ANALYSIS_POLICY => TB("Document Analysis Policy"),
        PluginConfigurationObjectType.FORMAT_TEMPLATE => TB("Format Template"),

        _ => TB("Unknown configuration"),
    };
}