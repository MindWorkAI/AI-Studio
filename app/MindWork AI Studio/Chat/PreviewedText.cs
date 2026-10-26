using System.Text.Json.Serialization;

namespace AIStudio.Chat;

/// <summary>
/// The text a preview read of a file attached to the draft of a chat, which sending takes along instead of reading the file again.
/// </summary>
/// <param name="FilePath">The path of the file, as the attachment names it.</param>
/// <param name="ProvenanceId">The entry of the provenance store which holds the text.</param>
/// <param name="Version">The state of the file before the preview read it. A file which changed since is read again.</param>
public sealed record PreviewedText([property: JsonConverter(typeof(DataDirectoryRelativePathConverter))] string FilePath, Guid ProvenanceId, FileVersion Version);