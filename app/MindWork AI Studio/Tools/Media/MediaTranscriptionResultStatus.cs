namespace AIStudio.Tools.Media;

/// <summary>
/// Terminal outcome of a media transcription operation.
/// </summary>
public enum MediaTranscriptionResultStatus
{
    /// <summary>The provider returned a usable transcript.</summary>
    SUCCEEDED,
    
    /// <summary>The operation failed.</summary>
    FAILED,

    /// <summary>The media contains no signal above the practical-silence threshold.</summary>
    NO_AUDIBLE_SIGNAL,
    
    /// <summary>The caller or user cancelled the operation.</summary>
    CANCELLED,
}