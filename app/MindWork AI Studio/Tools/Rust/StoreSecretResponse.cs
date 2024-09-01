namespace AIStudio.Tools.Rust;

/// <summary>
/// Data structure for storing a secret response.
/// </summary>
/// <param name="Success">True, when the secret was successfully stored.</param>
/// <param name="Issue">The issue, when the secret could not be stored.</param>
public readonly record struct StoreSecretResponse(bool Success, string Issue);