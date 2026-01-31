using System.Text.Json.Serialization;

using AIStudio.Provider;
using AIStudio.Tools.PluginSystem;

using Lua;

using Host = AIStudio.Provider.SelfHosted.Host;

namespace AIStudio.Settings;

public sealed record EmbeddingProvider(
    uint Num,
    string Id,
    string Name,
    LLMProviders UsedLLMProvider,
    Model Model,
    bool IsSelfHosted = false,
    bool IsEnterpriseConfiguration = false,
    Guid EnterpriseConfigurationPluginId = default,
    string Hostname = "http://localhost:1234",
    Host Host = Host.NONE) : ConfigurationBaseObject, ISecretId
{
    private static readonly ILogger<EmbeddingProvider> LOGGER = Program.LOGGER_FACTORY.CreateLogger<EmbeddingProvider>();

    public static readonly EmbeddingProvider NONE = new();

    public EmbeddingProvider() : this(
        0,
        Guid.Empty.ToString(),
        string.Empty,
        LLMProviders.NONE,
        default,
        false,
        false,
        Guid.Empty)
    {
    }

    public override string ToString() => this.Name;

    #region Implementation of ISecretId

    /// <inheritdoc />
    [JsonIgnore]
    public string SecretId => this.Id;

    /// <inheritdoc />
    [JsonIgnore]
    public string SecretName => this.Name;

    #endregion

    public static bool TryParseEmbeddingProviderTable(int idx, LuaTable table, Guid configPluginId, out ConfigurationBaseObject provider)
    {
        provider = NONE;
        if (!table.TryGetValue("Id", out var idValue) || !idValue.TryRead<string>(out var idText) || !Guid.TryParse(idText, out var id))
        {
            LOGGER.LogWarning($"The configured embedding provider {idx} does not contain a valid ID. The ID must be a valid GUID.");
            return false;
        }

        if (!table.TryGetValue("Name", out var nameValue) || !nameValue.TryRead<string>(out var name))
        {
            LOGGER.LogWarning($"The configured embedding provider {idx} does not contain a valid name.");
            return false;
        }

        if (!table.TryGetValue("UsedLLMProvider", out var usedLLMProviderValue) || !usedLLMProviderValue.TryRead<string>(out var usedLLMProviderText) || !Enum.TryParse<LLMProviders>(usedLLMProviderText, true, out var usedLLMProvider))
        {
            LOGGER.LogWarning($"The configured embedding provider {idx} does not contain a valid LLM provider enum value.");
            return false;
        }

        if (!table.TryGetValue("Host", out var hostValue) || !hostValue.TryRead<string>(out var hostText) || !Enum.TryParse<Host>(hostText, true, out var host))
        {
            LOGGER.LogWarning($"The configured embedding provider {idx} does not contain a valid host enum value.");
            return false;
        }

        if (!table.TryGetValue("Hostname", out var hostnameValue) || !hostnameValue.TryRead<string>(out var hostname))
        {
            LOGGER.LogWarning($"The configured embedding provider {idx} does not contain a valid hostname.");
            return false;
        }

        if (!table.TryGetValue("Model", out var modelValue) || !modelValue.TryRead<LuaTable>(out var modelTable))
        {
            LOGGER.LogWarning($"The configured embedding provider {idx} does not contain a valid model table.");
            return false;
        }

        if (!TryReadModelTable(idx, modelTable, out var model))
        {
            LOGGER.LogWarning($"The configured embedding provider {idx} does not contain a valid model configuration.");
            return false;
        }

        provider = new EmbeddingProvider
        {
            Num = 0, // will be set later by the PluginConfigurationObject
            Id = id.ToString(),
            Name = name,
            UsedLLMProvider = usedLLMProvider,
            Model = model,
            IsSelfHosted = usedLLMProvider is LLMProviders.SELF_HOSTED,
            IsEnterpriseConfiguration = true,
            EnterpriseConfigurationPluginId = configPluginId,
            Hostname = hostname,
            Host = host,
        };

        return true;
    }

    private static bool TryReadModelTable(int idx, LuaTable table, out Model model)
    {
        model = default;
        if (!table.TryGetValue("Id", out var idValue) || !idValue.TryRead<string>(out var id))
        {
            LOGGER.LogWarning($"The configured embedding provider {idx} does not contain a valid model ID.");
            return false;
        }

        if (!table.TryGetValue("DisplayName", out var displayNameValue) || !displayNameValue.TryRead<string>(out var displayName))
        {
            LOGGER.LogWarning($"The configured embedding provider {idx} does not contain a valid model display name.");
            return false;
        }

        model = new(id, displayName);
        return true;
    }
}