namespace AIStudio.Tools;

/// <summary>
/// How HTMLParser downloads a body which its caller reads as a document.
/// </summary>
/// <param name="ChooseFile">Gets the media type and the address of the final response, and returns the file to stream the body into, or null to read it as text.</param>
/// <param name="MaxBytes">The largest body which is downloaded.</param>
public sealed record HTMLParserDocumentDownload(Func<string, Uri, string?> ChooseFile, long MaxBytes);