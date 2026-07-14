namespace AIStudio.Tools.Services;

/// <summary>Visible phases of the serialized media import lane.</summary>
public enum MediaTranscriptionPhase
{
    /// <summary>No import is active.</summary>
    IDLE,

    /// <summary>The operation is waiting for the serialized runtime lane.</summary>
    QUEUED,
    
    /// <summary>The runtime is inspecting the input.</summary>
    PROBING,
    
    /// <summary>The runtime is preparing normalized audio.</summary>
    TRANSCODING,
    
    /// <summary>The normalized audio is being transcribed by the provider.</summary>
    UPLOADING,

    /// <summary>Cancellation was requested and runtime cleanup is in progress.</summary>
    CANCELING,
}