namespace AIStudio.Tools.Validation;

public static class CloudProcessingExtensions
{
    /// <summary>
    /// Whether what the user acknowledged covers what the data source sends to the cloud now.
    /// </summary>
    /// <remarks>
    /// An acknowledgment holds only for the providers it was given for. When the data source starts
    /// to send its recordings to a transcription provider in the cloud, say since a new pattern
    /// selects them, the user has to acknowledge that once more.
    /// </remarks>
    /// <param name="acknowledged">What the user acknowledged.</param>
    /// <param name="current">What the data source sends to the cloud with its current settings.</param>
    /// <returns>True when nothing is sent that the user did not acknowledge.</returns>
    public static bool Covers(this CloudProcessing acknowledged, CloudProcessing current) => (current & ~acknowledged) is CloudProcessing.NONE;
}