using System.Text.Json.Serialization;

namespace AIStudio.Settings;

public interface IExternalDataSource : IDataSource, ISecretId
{
    #region Implementation of ISecretId

    [JsonIgnore]
    string ISecretId.SecretId => this.IsEnterpriseConfiguration ? $"{ISecretId.ENTERPRISE_KEY_PREFIX}::{this.Id}" : this.Id;

    [JsonIgnore]
    string ISecretId.SecretName => this.Name;

    #endregion
}