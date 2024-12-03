namespace AIStudio.Tools;

/// <summary>
/// Represents an interface defining a secret identifier.
/// </summary>
public interface ISecretId
{
    /// <summary>
    /// The unique ID of the secret.
    /// </summary>
    public string SecretId { get; }

    /// <summary>
    /// The instance name of the secret.
    /// </summary>
    public string SecretName { get; }
}