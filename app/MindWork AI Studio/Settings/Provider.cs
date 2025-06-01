using System.Text.Json.Serialization;

using AIStudio.Provider;
using AIStudio.Provider.HuggingFace;
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
public readonly record struct Provider(
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
    HFInferenceProvider HFInferenceProvider = HFInferenceProvider.NONE) : ISecretId
{
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
}