namespace AIStudio.Tools.Rust;

/// <summary>Response returned after a Rust media job is registered.</summary>
/// <param name="JobId">Opaque runtime job identifier.</param>
public sealed record CreateMediaJobResponse(string JobId);