using System.Text.Json.Serialization;

namespace AIStudio.Settings;

public interface IExternalDataSource : IDataSource, ISecretId
{
    #region Implementation of ISecretId

    [JsonIgnore]
    string ISecretId.SecretId => this.Id;

    [JsonIgnore]
    string ISecretId.SecretName => this.Name;

    #endregion
}