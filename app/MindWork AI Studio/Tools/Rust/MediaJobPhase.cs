namespace AIStudio.Tools.Rust;

/// <summary>Lifecycle phases exposed by the Rust media API.</summary>
public enum MediaJobPhase
{
    /// <summary>An unknown future value received from Rust.</summary>
    UNKNOWN,
    
    /// <summary>The runtime is identifying the input and selecting audio.</summary>
    PROBING,
    
    /// <summary>The runtime is normalizing audio.</summary>
    TRANSCODING,
    
    /// <summary>The output was committed successfully.</summary>
    COMPLETED,
    
    /// <summary>The job failed.</summary>
    FAILED,
    
    /// <summary>Cancellation and temporary-output cleanup completed.</summary>
    CANCELLED,
}