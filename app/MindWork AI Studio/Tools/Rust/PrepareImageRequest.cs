namespace AIStudio.Tools.Rust;

/// <summary>Request body used to check and optionally optimize a local image in the Rust runtime.</summary>
/// <param name="Path">Absolute path of the image.</param>
/// <param name="Optimize">Whether the maximum-edge policy and re-encoding are applied.</param>
public sealed record PrepareImageRequest(string Path, bool Optimize);