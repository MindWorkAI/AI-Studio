using AIStudio.Tools.Rust;

namespace AIStudio.Tools.Services;

/// <summary>
/// Hands files to the share sheet of the operating system, and remembers when that share sheet
/// failed.
/// </summary>
/// <remarks>
/// A share sheet which hung once tends to stay broken: on Windows, it then accepts every further
/// request without ever appearing, so the share button seems to do nothing at all. Once the share
/// sheet failed or did not respond, AI Studio therefore exports plugin archives instead, until it
/// restarts. This service is a singleton, so that the decision holds for every window.
/// </remarks>
public sealed class NativeShareService(RustService rustService, ILogger<NativeShareService> logger)
{
    private volatile bool isUnavailable;

    /// <summary>
    /// Whether the share sheet of the operating system can be used. Linux has none, and on Windows
    /// and macOS it counts as unavailable once it failed or did not respond.
    /// </summary>
    public bool IsAvailable => !OperatingSystem.IsLinux() && !this.isUnavailable;

    /// <summary>
    /// Opens the share sheet of the operating system for a file.
    /// </summary>
    /// <param name="filePath">The plugin archive to share.</param>
    /// <returns>What became of the request.</returns>
    public async Task<ShareFileResponse> Share(string filePath)
    {
        var response = await rustService.ShareFile(filePath);
        if (response.Outcome is ShareSheetOutcome.OPENED || this.isUnavailable)
            return response;

        this.isUnavailable = true;
        logger.LogWarning("The native share sheet did not open ({Outcome}). Until AI Studio restarts, plugin archives are exported instead.", response.Outcome);
        return response;
    }
}