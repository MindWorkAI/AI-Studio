namespace AIStudio.Tools.Rust;

/// <summary>Successful terminal result returned by Rust media normalization.</summary>
/// <param name="OutputPath">Committed normalized output path.</param>
/// <param name="DetectedFormat">Detected container diagnostic.</param>
/// <param name="DetectedCodec">Selected codec diagnostic.</param>
/// <param name="DurationMs">Normalized duration in milliseconds.</param>
/// <param name="PassThrough">Whether the source was copied unchanged.</param>
/// <param name="HasAudibleSignal">Whether the normalized audio exceeds the practical-silence threshold.</param>
public sealed record MediaJobResult(
    string OutputPath,
    string DetectedFormat,
    string DetectedCodec,
    ulong DurationMs,
    bool PassThrough,
    bool HasAudibleSignal);