namespace AIStudio.Tools.Rust;

public sealed record CreateMediaJobRequest(string InputPath, string OutputPath, ulong? MaxPassThroughBytes = null);