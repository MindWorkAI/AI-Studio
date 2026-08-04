namespace AIStudio.Tools.Rust;

/// <summary>
/// Stable failure categories returned by the Rust media pipeline.
/// </summary>
public enum MediaJobErrorCode
{
    /// <summary>The runtime returned an unrecognized code.</summary>
    UNKNOWN,
    
    /// <summary>The input file does not exist.</summary>
    FILE_NOT_FOUND,
    
    /// <summary>The file type could not be identified.</summary>
    UNKNOWN_FORMAT,
    
    /// <summary>Executable input was rejected.</summary>
    UNSAFE_FILE,
    
    /// <summary>The input is not media.</summary>
    NOT_MEDIA,
    
    /// <summary>The input file could not be opened.</summary>
    FILE_OPEN_FAILED,
    
    /// <summary>The container is unsupported.</summary>
    UNSUPPORTED_CONTAINER,
    
    /// <summary>The media has no audio track.</summary>
    NO_AUDIO_TRACK,
    
    /// <summary>No audio track has a supported decoder.</summary>
    UNSUPPORTED_CODEC,
    
    /// <summary>Decoded audio parameters are absent or inconsistent.</summary>
    INVALID_AUDIO_PARAMETERS,
    
    /// <summary>The Opus identification header is invalid.</summary>
    INVALID_OPUS_HEADER,
    
    /// <summary>The Opus mapping requires unsupported multistream decoding.</summary>
    UNSUPPORTED_OPUS_MAPPING,
    
    /// <summary>The decoder could not be initialized.</summary>
    DECODER_INIT_FAILED,
    
    /// <summary>The encoder could not be initialized.</summary>
    ENCODER_INIT_FAILED,
    
    /// <summary>The stream changed unexpectedly.</summary>
    STREAM_RESET,
    
    /// <summary>The container is damaged.</summary>
    DAMAGED_CONTAINER,
    
    /// <summary>Audio decoding failed.</summary>
    DECODE_FAILED,
    
    /// <summary>Audio resampling failed.</summary>
    RESAMPLE_FAILED,
    
    /// <summary>Opus encoding failed.</summary>
    ENCODE_FAILED,
    
    /// <summary>The output directory or file could not be created.</summary>
    OUTPUT_CREATE_FAILED,
    
    /// <summary>The output could not be written.</summary>
    OUTPUT_WRITE_FAILED,
    
    /// <summary>The partial output could not be committed.</summary>
    OUTPUT_COMMIT_FAILED,
    
    /// <summary>A WebM relative timestamp overflowed.</summary>
    WEBM_TIMESTAMP_OVERFLOW,
    
    /// <summary>WebM serialization failed.</summary>
    WEBM_WRITE_FAILED,
    
    /// <summary>The job was cancelled.</summary>
    CANCELLED,
    
    /// <summary>The runtime worker failed unexpectedly.</summary>
    INTERNAL_ERROR,
}