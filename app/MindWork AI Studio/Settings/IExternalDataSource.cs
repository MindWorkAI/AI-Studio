using System.Text.Json.Serialization;

using AIStudio.Settings.DataModel;

namespace AIStudio.Settings;

public interface IExternalDataSource : IDataSource, ISecretId
{
    /// <summary>
    /// Which data security policy is applied to this external data source?
    /// </summary>
    public DataSourceSecurity SecurityPolicy { get; init; }

    #region Implementation of ISecretId

    [JsonIgnore]
    string ISecretId.SecretId => this.IsEnterpriseConfiguration ? $"{ENTERPRISE_KEY_PREFIX}::{this.Id}" : this.Id;

    [JsonIgnore]
    string ISecretId.SecretName => this.Name;

    #endregion
}