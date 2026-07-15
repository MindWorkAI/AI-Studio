namespace AIStudio.Tools.MCPClient;

/// <summary>
/// An image returned by an MCP image-generation tool.
/// </summary>
/// <param name="Base64Data">The base64-encoded image data.</param>
/// <param name="MimeType">The MIME type of the image, e.g., "image/png".</param>
public sealed record MCPGeneratedImage(string Base64Data, string MimeType);
