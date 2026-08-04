using AIStudio.Tools.Rust;

namespace AIStudio.Tools.Media;

/// <summary>One user-visible failure retained until its owner is displayed.</summary>
public sealed record MediaImportFailure(string FileName, string UserMessage, MediaJobErrorCode? ErrorCode = null);