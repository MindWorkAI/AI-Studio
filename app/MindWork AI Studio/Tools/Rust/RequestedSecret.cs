namespace AIStudio.Tools.Rust;

/// <summary>
/// Data structure for any requested secret.
/// </summary>
/// <param name="Success">True, when the secret was successfully retrieved.</param>
/// <param name="Secret">The secret, e.g., API key.</param>
/// <param name="Issue">The issue, when the secret could not be retrieved.</param>
/// <param name="IssueCode">The structured issue reported by the native credential store.</param>
public readonly record struct RequestedSecret(bool Success, EncryptedText Secret, string Issue, SecretStoreIssueCode IssueCode = SecretStoreIssueCode.NONE);