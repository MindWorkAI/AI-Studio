namespace AIStudio.Tools.Rust;

/// <summary>
/// Why the Rust runtime could not prepare an image for an AI model.
/// </summary>
/// <remarks>
/// UNKNOWN comes first on purpose. The converter for the enums of the runtime falls back to the
/// default value for a code it does not know, e.g., from a newer runtime, and such a code must
/// never read as a prepared image.
/// </remarks>
public enum ModelImageErrorCode
{
    /// <summary>
    /// The runtime answered with a code this version does not know, or it could not be asked at all.
    /// </summary>
    UNKNOWN,

    /// <summary>
    /// No failure happened: the image was prepared.
    /// </summary>
    NONE,

    //
    // Codes reported by the Rust runtime:
    //

    FILE_NOT_FOUND,

    /// <summary>
    /// The file exists, but could not be opened, e.g., for lack of permission.
    /// </summary>
    FILE_NOT_READABLE,

    /// <summary>
    /// AI Studio cannot decode this image format, e.g., HEIC, AVIF, or SVG.
    /// </summary>
    UNSUPPORTED_FORMAT,

    /// <summary>
    /// The content of the file does not match its file extension.
    /// </summary>
    CONTENT_MISMATCH,

    /// <summary>
    /// The file is damaged or uses a variant of its format which cannot be decoded.
    /// </summary>
    NOT_DECODABLE,

    /// <summary>
    /// Decoding the image would need more memory than the runtime allows.
    /// </summary>
    TOO_MANY_PIXELS,

    /// <summary>
    /// Not even the smallest size and lowest quality the runtime accepts fit into the byte budget.
    /// </summary>
    BUDGET_NOT_REACHABLE,

    /// <summary>
    /// Any failure in the runtime which does not carry a code of its own.
    /// </summary>
    INTERNAL,
}