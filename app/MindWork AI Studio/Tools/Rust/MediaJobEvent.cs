namespace AIStudio.Tools.Rust;

public sealed record MediaJobEvent(
    MediaJobPhase Phase,
    double? Progress,
    MediaJobResult? Result,
    MediaJobError? Error);