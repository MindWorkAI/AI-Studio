namespace AIStudio.Tools.Services;

/// <summary>Visible phases of the serialized media import lane.</summary>
public enum MediaTranscriptionPhase
{
    /// <summary>No import is active.</summary>
    IDLE,
    
    /// <summary>The runtime is inspecting the input.</summary>
    PROBING,
    
    /// <summary>The runtime is preparing normalized audio.</summary>
    TRANSCODING,
    
    /// <summary>The normalized audio is being transcribed by the provider.</summary>
    UPLOADING,
}