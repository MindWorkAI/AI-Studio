namespace AIStudio.Tools.Rust;

/// <summary>
/// The answer of the runtime to a request to share a file through the share sheet of the
/// operating system.
/// </summary>
/// <param name="Outcome">What became of the request.</param>
/// <param name="Issue">Why the share sheet did not open; empty when it opened.</param>
public sealed record ShareFileResponse(ShareSheetOutcome Outcome, string Issue);