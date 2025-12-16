namespace AIStudio.Tools;

/// <summary>
/// Helper methods for image handling, particularly for Base64 images.
/// </summary>
public static class ImageHelpers
{
    /// <summary>
    /// Detects the MIME type of an image from its Base64-encoded header.
    /// </summary>
    /// <param name="base64ImageString">The Base64-encoded image string.</param>
    /// <returns>The detected MIME type (e.g., "image/png", "image/jpeg").</returns>
    public static string DetectMimeType(ReadOnlySpan<char> base64ImageString)
    {
        if (base64ImageString.IsWhiteSpace() || base64ImageString.Length < 10)
            return "image"; // Fallback

        var header = base64ImageString[..Math.Min(20, base64ImageString.Length)];

        //
        // See https://en.wikipedia.org/wiki/List_of_file_signatures
        //
        
        // PNG: iVBORw0KGgo
        if (header.StartsWith("iVBORw0KGgo", StringComparison.Ordinal))
            return "image/png";

        // JPEG: /9j/
        if (header.StartsWith("/9j/", StringComparison.Ordinal))
            return "image/jpeg";

        // GIF: R0lGOD
        if (header.StartsWith("R0lGOD", StringComparison.Ordinal))
            return "image/gif";

        // WebP: UklGR
        if (header.StartsWith("UklGR", StringComparison.Ordinal))
            return "image/webp";

        // BMP: Qk
        if (header.StartsWith("Qk", StringComparison.Ordinal))
            return "image/bmp";

        // Default fallback:
        return "image";
    }

    /// <summary>
    /// Converts a Base64 string to a data URL suitable for use in HTML img src attributes.
    /// </summary>
    /// <param name="base64String">The Base64-encoded image string.</param>
    /// <param name="mimeType">Optional MIME type. If not provided, it will be auto-detected.</param>
    /// <returns>A data URL in the format "data:image/type;base64,..."</returns>
    public static string ToDataUrl(string base64String, string? mimeType = null)
    {
        if (string.IsNullOrEmpty(base64String))
            return string.Empty;

        var detectedMimeType = mimeType ?? DetectMimeType(base64String);
        return $"data:{detectedMimeType};base64,{base64String}";
    }
}
