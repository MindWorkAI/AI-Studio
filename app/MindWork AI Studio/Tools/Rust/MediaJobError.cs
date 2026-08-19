namespace AIStudio.Tools.Rust;

/// <summary>
/// Runtime media error containing a stable code and an English log diagnostic.
/// </summary>
/// <param name="Code">Stable machine-readable error category.</param>
/// <param name="Message">US-English diagnostic intended for logs.</param>
public sealed record MediaJobError(MediaJobErrorCode Code, string Message);