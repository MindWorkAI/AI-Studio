using System.Text.Json.Serialization;

using AIStudio.Provider;
using AIStudio.Provider.HuggingFace;
using AIStudio.Tools.PluginSystem;

using Lua;

using Host = AIStudio.Provider.SelfHosted.Host;

namespace AIStudio.Settings;

/// <summary>
/// Data model for configured providers.
/// </summary>
/// <param name="Num">The provider's number.</param>
/// <param name="Id">The provider's ID.</param>
/// <param name="InstanceName">The provider's instance name. Useful for multiple instances of the same provider, e.g., to distinguish between different OpenAI API keys.</param>
/// <param name="UsedLLMProvider">The provider used.</param>
/// <param name="IsSelfHosted">Whether the provider is self-hosted.</param>
/// <param name="Hostname">The hostname of the provider. Useful for self-hosted providers.</param>
/// <param name="Model">The LLM model to use for chat.</param>
public sealed record Provider(
    uint Num,
    string Id,
    string InstanceName,
    LLMProviders UsedLLMProvider,
    Model Model,
    bool IsSelfHosted = false,
    bool IsEnterpriseConfiguration = false,
    Guid EnterpriseConfigurationPluginId = default,
    string Hostname = "http://localhost:1234",
    Host Host = Host.NONE,
    HFInferenceProvider HFInferenceProvider = HFInferenceProvider.NONE,
    string AdditionalJsonApiParameters = "") : ConfigurationBaseObject, ISecretId
{
    private static readonly ILogger<Provider> LOGGER = Program.LOGGER_FACTORY.CreateLogger<Provider>();
    
    public static readonly Provider NONE = new();

    public Provider() : this(
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
    
    #region Overrides of ValueType

    /// <summary>
    /// Returns a string that represents the current provider in a human-readable format.
    /// We use this to display the provider in the chat UI.
    /// </summary>
    /// <returns>A string that represents the current provider in a human-readable format.</returns>
    public override string ToString()
    {
        if(this.IsSelfHosted)
            return $"{this.InstanceName} ({this.UsedLLMProvider.ToName()}, {this.Host}, {this.Hostname}, {this.Model})";

        return $"{this.InstanceName} ({this.UsedLLMProvider.ToName()}, {this.Model})";
    }

    #endregion
    
    #region Implementation of ISecretId
    
    /// <inheritdoc />
    [JsonIgnore]
    public string SecretId => this.Id;
    
    /// <inheritdoc />
    [JsonIgnore]
    public string SecretName => this.InstanceName;
    
    #endregion

    #region Implementation of IConfigurationObject

    public override string Name
    {
        get => this.InstanceName;
        init => this.InstanceName = value;
    }

    #endregion
    
    public static bool TryParseProviderTable(int idx, LuaTable table, Guid configPluginId, out ConfigurationBaseObject provider)
    {
        provider = NONE;
        if (!table.TryGetValue("Id", out var idValue) || !idValue.TryRead<string>(out var idText) || !Guid.TryParse(idText, out var id))
        {
            LOGGER.LogWarning($"The configured provider {idx} does not contain a valid ID. The ID must be a valid GUID.");
            return false;
        }

        if (!table.TryGetValue("InstanceName", out var instanceNameValue) || !instanceNameValue.TryRead<string>(out var instanceName))
        {
            LOGGER.LogWarning($"The configured provider {idx} does not contain a valid instance name.");
            return false;
        }

        if (!table.TryGetValue("UsedLLMProvider", out var usedLLMProviderValue) || !usedLLMProviderValue.TryRead<string>(out var usedLLMProviderText) || !Enum.TryParse<LLMProviders>(usedLLMProviderText, true, out var usedLLMProvider))
        {
            LOGGER.LogWarning($"The configured provider {idx} does not contain a valid LLM provider enum value.");
            return false;
        }
        
        if (!table.TryGetValue("Host", out var hostValue) || !hostValue.TryRead<string>(out var hostText) || !Enum.TryParse<Host>(hostText, true, out var host))
        {
            LOGGER.LogWarning($"The configured provider {idx} does not contain a valid host enum value.");
            return false;
        }
        
        if (!table.TryGetValue("Hostname", out var hostnameValue) || !hostnameValue.TryRead<string>(out var hostname))
        {
            LOGGER.LogWarning($"The configured provider {idx} does not contain a valid hostname.");
            return false;
        }

        var hfInferenceProvider = HFInferenceProvider.NONE;
        if (table.TryGetValue("HFInferenceProvider", out var hfInferenceProviderValue) && hfInferenceProviderValue.TryRead<string>(out var hfInferenceProviderText))
        {
            if (!Enum.TryParse<HFInferenceProvider>(hfInferenceProviderText, true, out hfInferenceProvider))
            {
                LOGGER.LogWarning($"The configured provider {idx} does not contain a valid Hugging Face inference provider enum value.");
                hfInferenceProvider = HFInferenceProvider.NONE;
            }
        }
        
        if (!table.TryGetValue("Model", out var modelValue) || !modelValue.TryRead<LuaTable>(out var modelTable))
        {
            LOGGER.LogWarning($"The configured provider {idx} does not contain a valid model table.");
            return false;
        }
        
        if (!TryReadModelTable(idx, modelTable, out var model))
        {
            LOGGER.LogWarning($"The configured provider {idx} does not contain a valid model configuration.");
            return false;
        }
        
        if (!table.TryGetValue("AdditionalJsonApiParameters", out var additionalJsonApiParametersValue) || !additionalJsonApiParametersValue.TryRead<string>(out var additionalJsonApiParameters))
        {
            // In this case, no reason exists to reject this provider, though.
            LOGGER.LogWarning($"The configured provider {idx} does not contain valid additional JSON API parameters.");
            additionalJsonApiParameters = string.Empty;
        }

        provider = new Provider
        {
            Num = 0, // will be set later by the PluginConfigurationObject
            Id = id.ToString(),
            InstanceName = instanceName,
            UsedLLMProvider = usedLLMProvider,
            Model = model,
            IsSelfHosted = usedLLMProvider is LLMProviders.SELF_HOSTED,
            IsEnterpriseConfiguration = true,
            EnterpriseConfigurationPluginId = configPluginId,
            Hostname = hostname,
            Host = host,
            HFInferenceProvider = hfInferenceProvider,
            AdditionalJsonApiParameters = additionalJsonApiParameters,
        };
        
        return true;
    }

    private static bool TryReadModelTable(int idx, LuaTable table, out Model model)
    {
        model = default;
        if (!table.TryGetValue("Id", out var idValue) || !idValue.TryRead<string>(out var id))
        {
            LOGGER.LogWarning($"The configured provider {idx} does not contain a valid model ID.");
            return false;
        }
        
        if (!table.TryGetValue("DisplayName", out var displayNameValue) || !displayNameValue.TryRead<string>(out var displayName))
        {
            LOGGER.LogWarning($"The configured provider {idx} does not contain a valid model display name.");
            return false;
        }
        
        model = new(id, displayName);
        return true;
    }

    public string ExportAsConfigurationSection()
    {
        var hfInferenceProviderLine = string.Empty;
        if (this.HFInferenceProvider is not HFInferenceProvider.NONE)
        {
            hfInferenceProviderLine = $"""
                                       ["HFInferenceProvider"] = "{this.HFInferenceProvider}",
                                       """;
        }
            
        return $$"""
                 CONFIG["LLM_PROVIDERS"][#CONFIG["LLM_PROVIDERS"]+1] = {
                     ["Id"] = "{{EscapeLuaString(NormalizeId(this.Id))}}",
                     ["InstanceName"] = "{{EscapeLuaString(this.InstanceName)}}",
                     ["UsedLLMProvider"] = "{{this.UsedLLMProvider}}",
                 
                     ["Host"] = "{{this.Host}}",
                     ["Hostname"] = "{{EscapeLuaString(this.Hostname)}}",
                     {{hfInferenceProviderLine}}
                     ["AdditionalJsonApiParameters"] = "{{EscapeLuaString(this.AdditionalJsonApiParameters)}}",
                     ["Model"] = {
                         ["Id"] = "{{EscapeLuaString(this.Model.Id)}}",
                         ["DisplayName"] = "{{EscapeLuaString(this.Model.DisplayName ?? string.Empty)}}",
                     },
                 }
                 """;
    }

    private static string NormalizeId(string? id)
    {
        if (!string.IsNullOrWhiteSpace(id))
            return id;

        return Guid.NewGuid().ToString();
    }

    private static string EscapeLuaString(string? value)
    {
        if (string.IsNullOrEmpty(value))
            return string.Empty;

        return value
            .Replace("\\", "\\\\")
            .Replace("\"", "\\\"")
            .Replace("\r", "\\r")
            .Replace("\n", "\\n");
    }
}
