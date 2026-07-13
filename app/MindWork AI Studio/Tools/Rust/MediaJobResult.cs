namespace AIStudio.Tools.Rust;

public sealed record MediaJobResult(
    string OutputPath,
    string DetectedFormat,
    string DetectedCodec,
    ulong DurationMs,
    bool PassThrough);