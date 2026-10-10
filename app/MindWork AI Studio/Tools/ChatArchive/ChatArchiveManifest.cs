using System.Text.Json.Serialization;

namespace AIStudio.Tools.ChatArchive;

/// <summary>
/// The manifest at the root of a chat archive. It describes the archive contents, so that
/// the import can show a preview without extracting anything.
/// </summary>
public sealed record ChatArchiveManifest
{
    /// <summary>
    /// The format version of this archive.
    /// </summary>
    public int FormatVersion { get; init; } = ChatArchiveFormat.FORMAT_VERSION;

    /// <summary>
    /// The time when this archive was created.
    /// </summary>
    public DateTimeOffset ExportedAt { get; init; }

    /// <summary>
    /// The AI Studio version which created this archive. Informational only.
    /// </summary>
    public string AppVersion { get; init; } = string.Empty;

    /// <summary>
    /// All workspaces contained in this archive.
    /// </summary>
    public List<ChatArchiveManifestWorkspace> Workspaces { get; init; } = [];

    /// <summary>
    /// All temporary chats contained in this archive.
    /// </summary>
    public List<ChatArchiveManifestChat> TemporaryChats { get; init; } = [];

    /// <summary>
    /// Gets the total number of chats in this archive.
    /// </summary>
    [JsonIgnore]
    public int TotalChatCount => this.Workspaces.Sum(workspace => workspace.Chats.Count) + this.TemporaryChats.Count;
}