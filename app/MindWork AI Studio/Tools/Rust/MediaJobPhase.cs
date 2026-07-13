namespace AIStudio.Tools.Rust;

public enum MediaJobPhase
{
    UNKNOWN,
    PROBING,
    TRANSCODING,
    COMPLETED,
    FAILED,
    CANCELLED,
}