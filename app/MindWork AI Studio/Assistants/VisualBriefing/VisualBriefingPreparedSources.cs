using AIStudio.Chat;

namespace AIStudio.Assistants.VisualBriefing;

/// <summary>
/// Holds prepared source inputs and owns their temporary optimized attachment files.
/// </summary>
internal sealed class VisualBriefingPreparedSources : IAsyncDisposable
{
    /// <summary>
    /// Gets or initializes the temporary directory.
    /// </summary>
    internal string TemporaryDirectory { get; init; } = string.Empty;

    /// <summary>
    /// Gets or initializes model attachments.
    /// </summary>
    internal IReadOnlyList<FileAttachment> Attachments { get; init; } = [];

    /// <summary>
    /// Gets or initializes transcript sections keyed by stable source ID.
    /// </summary>
    internal IReadOnlyDictionary<Guid, string> Transcripts { get; init; } = new Dictionary<Guid, string>();

    /// <summary>
    /// Gets or initializes prepared visual assets.
    /// </summary>
    internal IReadOnlyDictionary<string, PreparedVisualBriefingAsset> Assets { get; init; } = new Dictionary<string, PreparedVisualBriefingAsset>(StringComparer.Ordinal);

    /// <summary>
    /// Gets or initializes the current source fingerprint.
    /// </summary>
    internal string SourceFingerprint { get; init; } = string.Empty;

    /// <summary>
    /// Deletes temporary optimized attachment files on a best-effort basis.
    /// </summary>
    /// <returns>A completed value task.</returns>
    public ValueTask DisposeAsync()
    {
        try
        {
            if (!string.IsNullOrWhiteSpace(this.TemporaryDirectory) &&
                Directory.Exists(this.TemporaryDirectory))
                Directory.Delete(this.TemporaryDirectory, recursive: true);
        }
        catch
        {
            // Temporary optimized visual assets are cleaned up best effort.
        }
        
        return ValueTask.CompletedTask;
    }
}