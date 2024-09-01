namespace AIStudio.Tools.Rust;

/// <summary>
/// Data structure for deleting a secret response.
/// </summary>
/// <param name="Success">True, when the secret was successfully deleted or not found.</param>
/// <param name="Issue">The issue, when the secret could not be deleted.</param>
/// <param name="WasEntryFound">True, when the entry was found and deleted.</param>
public readonly record struct DeleteSecretResponse(bool Success, string Issue, bool WasEntryFound);