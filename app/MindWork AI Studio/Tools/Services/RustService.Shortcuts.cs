// ReSharper disable NotAccessedPositionalProperty.Local
using AIStudio.Tools.Rust;

namespace AIStudio.Tools.Services;

public sealed partial class RustService
{
    /// <summary>
    /// Registers or updates a global keyboard shortcut.
    /// </summary>
    /// <param name="shortcutId">The identifier for the shortcut.</param>
    /// <param name="shortcut">The shortcut string in Tauri format (e.g., "CmdOrControl+1"). Use empty string to disable.</param>
    /// <returns>True if the shortcut was registered successfully, false otherwise.</returns>
    public async Task<bool> UpdateGlobalShortcut(Shortcut shortcutId, string shortcut)
    {
        try
        {
            var request = new RegisterShortcutRequest(shortcutId, shortcut);
            var response = await this.http.PostAsJsonAsync("/shortcuts/register", request, this.jsonRustSerializerOptions);

            if (!response.IsSuccessStatusCode)
            {
                this.logger?.LogError("Failed to register global shortcut '{ShortcutId}' due to network error: {StatusCode}", shortcutId, response.StatusCode);
                return false;
            }

            var result = await response.Content.ReadFromJsonAsync<ShortcutResponse>(this.jsonRustSerializerOptions);
            if (result is null || !result.Success)
            {
                this.logger?.LogError("Failed to register global shortcut '{ShortcutId}': {Error}", shortcutId, result?.ErrorMessage ?? "Unknown error");
                return false;
            }

            this.logger?.LogInformation("Global shortcut '{ShortcutId}' registered successfully with key '{Shortcut}'.", shortcutId, shortcut);
            return true;
        }
        catch (Exception ex)
        {
            this.logger?.LogError(ex, "Exception while registering global shortcut '{ShortcutId}'.", shortcutId);
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

    /// <summary>
    /// Suspends shortcut processing. The shortcuts remain registered, but events are not sent.
    /// This is useful when opening a dialog to configure shortcuts, so the user can
    /// press the current shortcut to re-enter it without triggering the action.
    /// </summary>
    /// <returns>True if successful, false otherwise.</returns>
    public async Task<bool> SuspendShortcutProcessing()
    {
        try
        {
            var response = await this.http.PostAsync("/shortcuts/suspend", null);
            if (!response.IsSuccessStatusCode)
            {
                this.logger?.LogError("Failed to suspend the shortcut processing due to network error: {StatusCode}.", response.StatusCode);
                return false;
            }

            var result = await response.Content.ReadFromJsonAsync<ShortcutResponse>(this.jsonRustSerializerOptions);
            if (result is null || !result.Success)
            {
                this.logger?.LogError("Failed to suspend shortcut processing: {Error}", result?.ErrorMessage ?? "Unknown error");
                return false;
            }

            this.logger?.LogDebug("Shortcut processing suspended.");
            return true;
        }
        catch (Exception ex)
        {
            this.logger?.LogError(ex, "Exception while suspending shortcut processing.");
            return false;
        }
    }

    /// <summary>
    /// Resumes the shortcut processing after it was suspended.
    /// </summary>
    /// <returns>True if successful, false otherwise.</returns>
    public async Task<bool> ResumeShortcutProcessing()
    {
        try
        {
            var response = await this.http.PostAsync("/shortcuts/resume", null);
            if (!response.IsSuccessStatusCode)
            {
                this.logger?.LogError("Failed to resume shortcut processing due to network error: {StatusCode}.", response.StatusCode);
                return false;
            }

            var result = await response.Content.ReadFromJsonAsync<ShortcutResponse>(this.jsonRustSerializerOptions);
            if (result is null || !result.Success)
            {
                this.logger?.LogError("Failed to resume shortcut processing: {Error}", result?.ErrorMessage ?? "Unknown error");
                return false;
            }

            this.logger?.LogDebug("Shortcut processing resumed.");
            return true;
        }
        catch (Exception ex)
        {
            this.logger?.LogError(ex, "Exception while resuming shortcut processing.");
            return false;
        }
    }
}

/// <summary>
/// Result of validating a keyboard shortcut.
/// </summary>
/// <param name="IsValid">Whether the shortcut syntax is valid.</param>
/// <param name="ErrorMessage">Error message if not valid.</param>
/// <param name="HasConflict">Whether the shortcut conflicts with another registered shortcut.</param>
/// <param name="ConflictDescription">Description of the conflict, if any.</param>
public sealed record ShortcutValidationResult(bool IsValid, string ErrorMessage, bool HasConflict, string ConflictDescription);
