namespace AIStudio.Tools.Rust;

/// <summary>Request body used to prepare a local image for a model in the Rust runtime.</summary>
/// <param name="Path">Absolute path of the image.</param>
/// <param name="MaxBytes">The size the prepared image may have at most.</param>
public sealed record PrepareImageForModelRequest(string Path, long MaxBytes);