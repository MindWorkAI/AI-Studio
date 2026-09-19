namespace AIStudio.Settings.DataModel;

public enum TranscriptionOpusBitrate
{
    // The recommended bitrate is deliberately the member with the underlying value 0: when the
    // settings file holds a value TolerantEnumConverter cannot read, it falls back to that member.
    // Landing on the lowest bitrate there would silently reintroduce the very defect this setting
    // exists to prevent -- transcripts losing what was said quietly.
    KBPS_128 = 0,

    KBPS_32,
    KBPS_64,
    KBPS_256,
}