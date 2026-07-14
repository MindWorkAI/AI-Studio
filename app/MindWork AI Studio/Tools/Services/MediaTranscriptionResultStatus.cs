namespace AIStudio.Tools.Services;

/// <summary>
/// Terminal outcome of a media transcription operation.
/// </summary>
public enum MediaTranscriptionResultStatus
{
    /// <summary>The provider returned a usable transcript.</summary>
    SUCCEEDED,
    
    /// <summary>The operation failed.</summary>
    FAILED,
    
    /// <summary>The caller or user cancelled the operation.</summary>
    CANCELLED,
}