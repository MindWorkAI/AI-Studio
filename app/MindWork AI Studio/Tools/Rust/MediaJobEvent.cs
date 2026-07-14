namespace AIStudio.Tools.Rust;

/// <summary>Snapshot emitted by the Rust media job event stream.</summary>
/// <param name="Phase">Current job phase.</param>
/// <param name="Progress">Optional progress fraction.</param>
/// <param name="Result">Completed result.</param>
/// <param name="Error">Failure diagnostic.</param>
public sealed record MediaJobEvent(
    MediaJobPhase Phase,
    double? Progress,
    MediaJobResult? Result,
    MediaJobError? Error);