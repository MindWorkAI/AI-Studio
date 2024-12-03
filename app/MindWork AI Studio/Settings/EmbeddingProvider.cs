using System.Text.Json.Serialization;

using AIStudio.Provider;

using Host = AIStudio.Provider.SelfHosted.Host;

namespace AIStudio.Settings;

public readonly record struct EmbeddingProvider(
    uint Num,
    string Id,
    string Name,
    LLMProviders UsedLLMProvider,
    Model Model,
    bool IsSelfHosted = false,
    string Hostname = "http://localhost:1234",
    Host Host = Host.NONE) : ISecretId
{
    public override string ToString() => this.Name;
    
    #region Implementation of ISecretId
    
    /// <inheritdoc />
    [JsonIgnore]
    public string SecretId => this.Id;
    
    /// <inheritdoc />
    [JsonIgnore]
    public string SecretName => this.Name;
    
    #endregion
}