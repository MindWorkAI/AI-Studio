namespace AIStudio.Tools.Validation;

/// <summary>
/// What a data source sends to a provider which is not self-hosted.
/// </summary>
[Flags]
public enum CloudProcessing
{
    /// <summary>
    /// Nothing leaves the computer or the organization.
    /// </summary>
    NONE = 0,

    /// <summary>
    /// The content goes to an embedding provider in the cloud.
    /// </summary>
    EMBEDDING = 1,

    /// <summary>
    /// Audio and video files go to a transcription provider in the cloud.
    /// </summary>
    TRANSCRIPTION = 2,
}