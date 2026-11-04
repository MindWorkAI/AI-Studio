using System.Diagnostics.CodeAnalysis;

using AIStudio.Settings;
using AIStudio.Settings.DataModel;

using Lua;

namespace AIStudio.Tools.PluginSystem;

/// <summary>
/// The descriptors of every type of configuration object which configuration plugins provide.
/// </summary>
/// <remarks>
/// A new type of configuration object gets its descriptor here and joins ALL. Reading the objects from
/// a configuration plugin and removing the left-over ones then handle it, and a test checks that no
/// type of PluginConfigurationObjectType is missing.
/// </remarks>
public static class PluginConfigurationObjectDescriptors
{
    public static readonly ConfigurationBaseObjectDescriptor<Settings.Provider> LLM_PROVIDER = new(PluginConfigurationObjectType.LLM_PROVIDER, "LLM_PROVIDERS", x => x.Providers, x => x.NextProviderNum, ParseLLMProvider, SecretStoreType.LLM_PROVIDER);

    public static readonly ConfigurationBaseObjectDescriptor<TranscriptionProvider> TRANSCRIPTION_PROVIDER = new(PluginConfigurationObjectType.TRANSCRIPTION_PROVIDER, "TRANSCRIPTION_PROVIDERS", x => x.TranscriptionProviders, x => x.NextTranscriptionNum, ParseTranscriptionProvider, SecretStoreType.TRANSCRIPTION_PROVIDER);

    public static readonly ConfigurationBaseObjectDescriptor<EmbeddingProvider> EMBEDDING_PROVIDER = new(PluginConfigurationObjectType.EMBEDDING_PROVIDER, "EMBEDDING_PROVIDERS", x => x.EmbeddingProviders, x => x.NextEmbeddingNum, ParseEmbeddingProvider, SecretStoreType.EMBEDDING_PROVIDER);

    public static readonly ConfigurationBaseObjectDescriptor<ChatTemplate> CHAT_TEMPLATE = new(PluginConfigurationObjectType.CHAT_TEMPLATE, "CHAT_TEMPLATES", x => x.ChatTemplates, x => x.NextChatTemplateNum, ParseChatTemplate);

    public static readonly DataSourceConfigurationObjectDescriptor DATA_SOURCE = new();

    public static readonly ConfigurationBaseObjectDescriptor<Profile> PROFILE = new(PluginConfigurationObjectType.PROFILE, "PROFILES", x => x.Profiles, x => x.NextProfileNum, ParseProfile);

    public static readonly ConfigurationBaseObjectDescriptor<DataDocumentAnalysisPolicy> DOCUMENT_ANALYSIS_POLICY = new(PluginConfigurationObjectType.DOCUMENT_ANALYSIS_POLICY, "DOCUMENT_ANALYSIS_POLICIES", x => x.DocumentAnalysis.Policies, x => x.NextDocumentAnalysisPolicyNum, ParseDocumentAnalysisPolicy);

    public static readonly ConfigurationBaseObjectDescriptor<FormatTemplate> FORMAT_TEMPLATE = new(PluginConfigurationObjectType.FORMAT_TEMPLATE, "FORMAT_TEMPLATES", x => x.FormatTemplates, x => x.NextFormatTemplateNum, ParseFormatTemplate);

    /// <summary>
    /// Every descriptor, in the order in which configuration plugins are read.
    /// </summary>
    public static readonly IReadOnlyList<PluginConfigurationObjectDescriptor> ALL =
    [
        LLM_PROVIDER,
        TRANSCRIPTION_PROVIDER,
        EMBEDDING_PROVIDER,
        CHAT_TEMPLATE,
        DATA_SOURCE,
        PROFILE,
        DOCUMENT_ANALYSIS_POLICY,
        FORMAT_TEMPLATE,
    ];

    /// <summary>
    /// The descriptor of a type of configuration object.
    /// </summary>
    /// <param name="type">The type, which must not be NONE or UNKNOWN.</param>
    /// <returns>The descriptor of the type.</returns>
    public static PluginConfigurationObjectDescriptor Get(PluginConfigurationObjectType type) =>
        ALL.FirstOrDefault(descriptor => descriptor.Type == type) ?? throw new ArgumentOutOfRangeException(nameof(type), type, "This type of configuration object has no descriptor.");

    /// <summary>
    /// Finds the descriptor of the configuration objects which a Lua table holds.
    /// </summary>
    /// <param name="luaTableName">The name of the Lua table, e.g., LLM_PROVIDERS.</param>
    /// <param name="descriptor">The descriptor, when one uses this table.</param>
    /// <returns>True when a descriptor uses this table.</returns>
    public static bool TryGet(string luaTableName, [NotNullWhen(true)] out PluginConfigurationObjectDescriptor? descriptor)
    {
        descriptor = ALL.FirstOrDefault(candidate => candidate.LuaTableName == luaTableName);
        return descriptor is not null;
    }

    private static bool ParseLLMProvider(int index, LuaTable table, Guid configPluginId, string pluginPath, bool dryRun, out ConfigurationBaseObject configObject) =>
        Settings.Provider.TryParseProviderTable(index, table, configPluginId, pluginPath, dryRun, out configObject) && configObject != Settings.Provider.NONE;

    private static bool ParseTranscriptionProvider(int index, LuaTable table, Guid configPluginId, string pluginPath, bool dryRun, out ConfigurationBaseObject configObject) =>
        TranscriptionProvider.TryParseTranscriptionProviderTable(index, table, configPluginId, pluginPath, dryRun, out configObject) && configObject != TranscriptionProvider.NONE;

    private static bool ParseEmbeddingProvider(int index, LuaTable table, Guid configPluginId, string pluginPath, bool dryRun, out ConfigurationBaseObject configObject) =>
        EmbeddingProvider.TryParseEmbeddingProviderTable(index, table, configPluginId, pluginPath, dryRun, out configObject) && configObject != EmbeddingProvider.NONE;

    private static bool ParseChatTemplate(int index, LuaTable table, Guid configPluginId, string pluginPath, bool dryRun, out ConfigurationBaseObject configObject) =>
        ChatTemplate.TryParseChatTemplateTable(index, table, configPluginId, pluginPath, out configObject) && configObject != ChatTemplate.NO_CHAT_TEMPLATE;

    private static bool ParseProfile(int index, LuaTable table, Guid configPluginId, string pluginPath, bool dryRun, out ConfigurationBaseObject configObject) =>
        Profile.TryParseProfileTable(index, table, configPluginId, out configObject) && configObject != Profile.NO_PROFILE;

    private static bool ParseDocumentAnalysisPolicy(int index, LuaTable table, Guid configPluginId, string pluginPath, bool dryRun, out ConfigurationBaseObject configObject) =>
        DataDocumentAnalysisPolicy.TryProcessConfiguration(index, table, configPluginId, out configObject) && configObject is DataDocumentAnalysisPolicy;

    private static bool ParseFormatTemplate(int index, LuaTable table, Guid configPluginId, string pluginPath, bool dryRun, out ConfigurationBaseObject configObject) =>
        FormatTemplate.TryParseFormatTemplateTable(index, table, configPluginId, pluginPath, out configObject);
}