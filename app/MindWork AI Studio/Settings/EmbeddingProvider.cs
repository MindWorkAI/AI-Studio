using System.Text.Json.Serialization;

using AIStudio.Provider;
using AIStudio.Tools.PluginSystem;

using SharedTools;

using Host = AIStudio.Provider.SelfHosted.Host;
using LuaTable = Lua.LuaTable;

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
    Host Host = Host.NONE,
    string TokenizerPath = "",
    int EmbeddingBatchSize = 0,
    int TokenLimit = 0) : ConfigurationBaseObject, ISecretId
{
    public const int DEFAULT_TOKEN_LIMIT = 8192;
    public const int DEFAULT_EMBEDDING_BATCH_SIZE = 1;

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
    public string SecretId => this.IsEnterpriseConfiguration ? $"{ISecretId.ENTERPRISE_KEY_PREFIX}::{this.UsedLLMProvider.ToSecretId()}" : this.UsedLLMProvider.ToSecretId();

    /// <inheritdoc />
    [JsonIgnore]
    public string SecretName => this.Name;

    [JsonIgnore]
    public int EffectiveTokenLimit => this.TokenLimit > 0 ? this.TokenLimit : DEFAULT_TOKEN_LIMIT;

    [JsonIgnore]
    public int EffectiveEmbeddingBatchSize => this.EmbeddingBatchSize > 0 ? this.EmbeddingBatchSize : DEFAULT_EMBEDDING_BATCH_SIZE;

    #endregion

    public static bool TryParseEmbeddingProviderTable(int idx, LuaTable table, Guid configPluginId, out ConfigurationBaseObject provider)
    {
        provider = NONE;
        if (!table.TryGetValue("Id", out var idValue) || !idValue.TryRead<string>(out var idText) || !Guid.TryParse(idText, out var id))
        {
            LOGGER.LogWarning($"The configured embedding provider {idx} does not contain a valid ID. The ID must be a valid GUID. (Plugin ID: {configPluginId})");
            return false;
        }

        if (!table.TryGetValue("Name", out var nameValue) || !nameValue.TryRead<string>(out var name))
        {
            LOGGER.LogWarning($"The configured embedding provider {idx} does not contain a valid name. (Plugin ID: {configPluginId})");
            return false;
        }

        if (!table.TryGetValue("UsedLLMProvider", out var usedLLMProviderValue) || !usedLLMProviderValue.TryRead<string>(out var usedLLMProviderText) || !Enum.TryParse<LLMProviders>(usedLLMProviderText, true, out var usedLLMProvider))
        {
            LOGGER.LogWarning($"The configured embedding provider {idx} does not contain a valid LLM provider enum value. (Plugin ID: {configPluginId})");
            return false;
        }

        if (!table.TryGetValue("Host", out var hostValue) || !hostValue.TryRead<string>(out var hostText) || !Enum.TryParse<Host>(hostText, true, out var host))
        {
            LOGGER.LogWarning($"The configured embedding provider {idx} does not contain a valid host enum value. (Plugin ID: {configPluginId})");
            return false;
        }

        if (!table.TryGetValue("Hostname", out var hostnameValue) || !hostnameValue.TryRead<string>(out var hostname))
        {
            LOGGER.LogWarning($"The configured embedding provider {idx} does not contain a valid hostname. (Plugin ID: {configPluginId})");
            return false;
        }

        if (!table.TryGetValue("Model", out var modelValue) || !modelValue.TryRead<LuaTable>(out var modelTable))
        {
            LOGGER.LogWarning($"The configured embedding provider {idx} does not contain a valid model table. (Plugin ID: {configPluginId})");
            return false;
        }

        if (!TryReadModelTable(idx, modelTable, configPluginId, out var model))
        {
            LOGGER.LogWarning($"The configured embedding provider {idx} does not contain a valid model configuration. (Plugin ID: {configPluginId})");
            return false;
        }

        var tokenizerPath = string.Empty;
        if (table.TryGetValue("TokenizerPath", out var tokenizerPathValue) && !tokenizerPathValue.TryRead<string>(out tokenizerPath))
        {
            LOGGER.LogWarning($"The configured embedding provider {idx} does not contain a valid tokenizer path. (Plugin ID: {configPluginId})");
            tokenizerPath = string.Empty;
        }

        var tokenLimit = DEFAULT_TOKEN_LIMIT;
        if (table.TryGetValue("TokenLimit", out var tokenLimitValue) && (!tokenLimitValue.TryRead<int>(out tokenLimit) || tokenLimit < 1))
        {
            LOGGER.LogWarning($"The configured embedding provider {idx} does not contain a valid token limit. Falling back to {DEFAULT_TOKEN_LIMIT}. (Plugin ID: {configPluginId})");
            tokenLimit = DEFAULT_TOKEN_LIMIT;
        }

        var embeddingBatchSize = DEFAULT_EMBEDDING_BATCH_SIZE;
        if (table.TryGetValue("EmbeddingBatchSize", out var embeddingBatchSizeValue) && (!embeddingBatchSizeValue.TryRead<int>(out embeddingBatchSize) || embeddingBatchSize < 1))
        {
            LOGGER.LogWarning($"The configured embedding provider {idx} does not contain a valid embedding batch size. Falling back to {DEFAULT_EMBEDDING_BATCH_SIZE}. (Plugin ID: {configPluginId})");
            embeddingBatchSize = DEFAULT_EMBEDDING_BATCH_SIZE;
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
            TokenizerPath = tokenizerPath,
            EmbeddingBatchSize = embeddingBatchSize,
            TokenLimit = tokenLimit,
        };

        // Handle encrypted API key if present:
        if (table.TryGetValue("APIKey", out var apiKeyValue) && apiKeyValue.TryRead<string>(out var apiKeyText) && !string.IsNullOrWhiteSpace(apiKeyText))
        {
            if (!EnterpriseEncryption.IsEncrypted(apiKeyText))
                LOGGER.LogWarning($"The configured embedding provider {idx} contains a plaintext API key. Only encrypted API keys (starting with 'ENC:v1:') are supported. (Plugin ID: {configPluginId})");
            else
            {
                var encryption = PluginFactory.EnterpriseEncryption;
                if (encryption?.IsAvailable == true)
                {
                    if (encryption.TryDecrypt(apiKeyText, out var decryptedApiKey))
                    {
                        // Queue the API key for storage in the OS keyring:
                        PendingEnterpriseApiKeys.Add(new(
                            $"{ISecretId.ENTERPRISE_KEY_PREFIX}::{usedLLMProvider.ToSecretId()}",
                            name,
                            decryptedApiKey,
                            SecretStoreType.EMBEDDING_PROVIDER));
                        LOGGER.LogDebug($"Successfully decrypted API key for embedding provider {idx}. It will be stored in the OS keyring. (Plugin ID: {configPluginId})");
                    }
                    else
                        LOGGER.LogWarning($"Failed to decrypt API key for embedding provider {idx}. The encryption secret may be incorrect. (Plugin ID: {configPluginId})");
                }
                else
                    LOGGER.LogWarning($"The configured embedding provider {idx} contains an encrypted API key, but no encryption secret is configured. (Plugin ID: {configPluginId})");
            }
        }

        return true;
    }

    private static bool TryReadModelTable(int idx, LuaTable table, Guid configPluginId, out Model model)
    {
        model = default;
        if (!table.TryGetValue("Id", out var idValue) || !idValue.TryRead<string>(out var id))
        {
            LOGGER.LogWarning($"The configured embedding provider {idx} does not contain a valid model ID. (Plugin ID: {configPluginId})");
            return false;
        }

        if (!table.TryGetValue("DisplayName", out var displayNameValue) || !displayNameValue.TryRead<string>(out var displayName))
        {
            LOGGER.LogWarning($"The configured embedding provider {idx} does not contain a valid model display name. (Plugin ID: {configPluginId})");
            return false;
        }

        model = new(id, displayName);
        return true;
    }

    /// <summary>
    /// Exports the embedding provider configuration as a Lua configuration section.
    /// </summary>
    /// <param name="encryptedApiKey">Optional encrypted API key to include in the export.</param>
    /// <returns>A Lua configuration section string.</returns>
    public string ExportAsConfigurationSection(string? encryptedApiKey = null)
    {
        var apiKeyLine = string.Empty;
        if (!string.IsNullOrWhiteSpace(encryptedApiKey))
        {
            apiKeyLine = $"""
                          ["APIKey"] = "{LuaTools.EscapeLuaString(encryptedApiKey)}",
                          """;
        }

        return $$"""
                CONFIG["EMBEDDING_PROVIDERS"][#CONFIG["EMBEDDING_PROVIDERS"]+1] = {
                    ["Id"] = "{{Guid.NewGuid().ToString()}}",
                    ["Name"] = "{{LuaTools.EscapeLuaString(this.Name)}}",
                    ["UsedLLMProvider"] = "{{this.UsedLLMProvider}}",
                    
                    ["TokenizerPath"] = "{{this.TokenizerPath}}",
                    ["TokenLimit"] = {{this.EffectiveTokenLimit}},
                    ["EmbeddingBatchSize"] = {{this.EffectiveEmbeddingBatchSize}},
                 
                    ["Host"] = "{{this.Host}}",
                    ["Hostname"] = "{{LuaTools.EscapeLuaString(this.Hostname)}}",
                    {{apiKeyLine}}
                    ["Model"] = {
                        ["Id"] = "{{LuaTools.EscapeLuaString(this.Model.Id)}}",
                        ["DisplayName"] = "{{LuaTools.EscapeLuaString(this.Model.DisplayName ?? string.Empty)}}",
                    },
                }
                """;
    }
}
