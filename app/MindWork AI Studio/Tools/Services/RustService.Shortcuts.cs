// ReSharper disable NotAccessedPositionalProperty.Local
namespace AIStudio.Tools.Services;

public sealed partial class RustService
{
    /// <summary>
    /// Registers or updates a global keyboard shortcut.
    /// </summary>
    /// <param name="name">The name/identifier for the shortcut (e.g., "voice_recording_toggle").</param>
    /// <param name="shortcut">The shortcut string in Tauri format (e.g., "CmdOrControl+1"). Use empty string to disable.</param>
    /// <returns>True if the shortcut was registered successfully, false otherwise.</returns>
    public async Task<bool> UpdateGlobalShortcut(string name, string shortcut)
    {
        try
        {
            var request = new RegisterShortcutRequest(name, shortcut);
            var response = await this.http.PostAsJsonAsync("/shortcuts/register", request, this.jsonRustSerializerOptions);

            if (!response.IsSuccessStatusCode)
            {
                this.logger?.LogError("Failed to register global shortcut '{Name}' due to network error: {StatusCode}", name, response.StatusCode);
                return false;
            }

            var result = await response.Content.ReadFromJsonAsync<ShortcutResponse>(this.jsonRustSerializerOptions);
            if (result is null || !result.Success)
            {
                this.logger?.LogError("Failed to register global shortcut '{Name}': {Error}", name, result?.ErrorMessage ?? "Unknown error");
                return false;
            }

            this.logger?.LogInformation("Global shortcut '{Name}' registered successfully with key '{Shortcut}'.", name, shortcut);
            return true;
        }
        catch (Exception ex)
        {
            this.logger?.LogError(ex, "Exception while registering global shortcut '{Name}'.", name);
            return false;
        }
    }

    /// <summary>
    /// Validates a shortcut string without registering it.
    /// </summary>
    /// <param name="shortcut">The shortcut string to validate.</param>
    /// <returns>A validation result indicating if the shortcut is valid and any conflicts.</returns>
    public async Task<ShortcutValidationResult> ValidateShortcut(string shortcut)
    {
        try
        {
            var request = new ValidateShortcutRequest(shortcut);
            var response = await this.http.PostAsJsonAsync("/shortcuts/validate", request, this.jsonRustSerializerOptions);

            if (!response.IsSuccessStatusCode)
            {
                this.logger?.LogError("Failed to validate shortcut due to network error: {StatusCode}", response.StatusCode);
                return new ShortcutValidationResult(false, "Network error during validation", false, string.Empty);
            }

            var result = await response.Content.ReadFromJsonAsync<ShortcutValidationResponse>(this.jsonRustSerializerOptions);
            if (result is null)
                return new ShortcutValidationResult(false, "Invalid response from server", false, string.Empty);

            return new ShortcutValidationResult(result.IsValid, result.ErrorMessage, result.HasConflict, result.ConflictDescription);
        }
        catch (Exception ex)
        {
            this.logger?.LogError(ex, "Exception while validating shortcut.");
            return new ShortcutValidationResult(false, ex.Message, false, string.Empty);
        }
    }

    private sealed record RegisterShortcutRequest(string Name, string Shortcut);

    private sealed record ShortcutResponse(bool Success, string ErrorMessage);

    private sealed record ValidateShortcutRequest(string Shortcut);

    private sealed record ShortcutValidationResponse(bool IsValid, string ErrorMessage, bool HasConflict, string ConflictDescription);
}

/// <summary>
/// Result of validating a keyboard shortcut.
/// </summary>
/// <param name="IsValid">Whether the shortcut syntax is valid.</param>
/// <param name="ErrorMessage">Error message if not valid.</param>
/// <param name="HasConflict">Whether the shortcut conflicts with another registered shortcut.</param>
/// <param name="ConflictDescription">Description of the conflict if any.</param>
public sealed record ShortcutValidationResult(bool IsValid, string ErrorMessage, bool HasConflict, string ConflictDescription);
