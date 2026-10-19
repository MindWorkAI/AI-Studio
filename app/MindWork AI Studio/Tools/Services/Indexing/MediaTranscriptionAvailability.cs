using AIStudio.Settings;

namespace AIStudio.Tools.Services.Indexing;

/// <summary>
/// Which transcription provider may hear the audio and video files of a data source.
/// </summary>
/// <param name="Provider">The provider, or null when none may.</param>
/// <param name="IgnoreReason">Why audio and video files without a transcript are left out when no provider may hear them; null when there is a provider.</param>
internal sealed record MediaTranscriptionAvailability(TranscriptionProvider? Provider, DataSourceEmbeddingIgnoreReason? IgnoreReason)
{
    /// <summary>
    /// No usable transcription provider is configured.
    /// </summary>
    public static readonly MediaTranscriptionAvailability MISSING = new(null, DataSourceEmbeddingIgnoreReason.TRANSCRIPTION_PROVIDER_MISSING);

    /// <summary>
    /// The configured transcription provider falls short of the confidence level of the data source.
    /// </summary>
    public static readonly MediaTranscriptionAvailability NOT_ALLOWED = new(null, DataSourceEmbeddingIgnoreReason.TRANSCRIPTION_PROVIDER_NOT_ALLOWED);

    /// <summary>
    /// The given provider may hear the media files.
    /// </summary>
    /// <param name="provider">The provider.</param>
    /// <returns>The availability.</returns>
    public static MediaTranscriptionAvailability Allowed(TranscriptionProvider provider) => new(provider, null);
}