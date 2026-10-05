using AIStudio.Tools.Mail;

namespace AIStudio.Tools.Databases.IndexStore;

/// <summary>
/// One part of a mail as AI Studio read it.
/// </summary>
/// <param name="Kind">The header block, the body or an attachment.</param>
/// <param name="Name">The file name of an attachment, empty for the other parts.</param>
/// <param name="ContentType">The content type of the part.</param>
/// <param name="PartSize">The size of the part on the server, in bytes.</param>
/// <param name="Text">The text of the part, or null when there is none.</param>
/// <param name="TextState">Whether the text could be read and, when not, why.</param>
public sealed record MailPartRecord(MailPartKind Kind, string Name, string ContentType, long PartSize, string? Text, MailPartTextState TextState);