namespace AIStudio.Tools.Rust;

/// <summary>Request body used to start a Rust media normalization job.</summary>
/// <param name="InputPath">Absolute source media path.</param>
/// <param name="OutputPath">Absolute operation-owned output path.</param>
/// <param name="MaxPassThroughBytes">Optional pass-through size ceiling.</param>
public sealed record CreateMediaJobRequest(string InputPath, string OutputPath, ulong? MaxPassThroughBytes = null);