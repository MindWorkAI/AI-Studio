namespace AIStudio.Tools;

/// <summary>
/// The formats in which AI Studio encrypts the secrets of enterprise configuration plugins.
/// </summary>
public enum EnterpriseEncryptionFormat
{
    /// <summary>
    /// "ENC:v1:", which every version of AI Studio reads. It is not authenticated, so a wrong
    /// encryption secret is not always detected.
    /// </summary>
    V1,

    /// <summary>
    /// "ENC:v2:", which is authenticated: AI Studio refuses every value encrypted with another
    /// secret or changed afterward. Versions before v26.10.1 cannot read it.
    /// </summary>
    V2,
}