namespace AIStudio.Tools;

/// <summary>
/// Represents an interface defining a secret identifier.
/// </summary>
public interface ISecretId
{
    /// <summary>
    /// Prefix used for secrets imported from enterprise configuration plugins.
    /// This helps distinguish enterprise-managed keys from user-added keys
    /// in the OS keyring.
    /// </summary>
    public const string ENTERPRISE_KEY_PREFIX = "config-plugin";

    /// <summary>
    /// The unique ID of the secret.
    /// </summary>
    public string SecretId { get; }

    /// <summary>
    /// The instance name of the secret.
    /// </summary>
    public string SecretName { get; }
}