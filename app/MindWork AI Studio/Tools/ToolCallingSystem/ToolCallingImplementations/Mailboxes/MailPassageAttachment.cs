namespace AIStudio.Tools.ToolCallingSystem.ToolCallingImplementations.Mailboxes;

/// <summary>
/// The attachment of a mail which a passage is from, and where in it, cf. SearchMailsTool.
/// </summary>
/// <param name="Name">The filtered name of the attachment, or an empty string when the passage is from no attachment.</param>
/// <param name="Where">Where in the attachment the passage is, or null when that is not known.</param>
internal readonly record struct MailPassageAttachment(string Name, string? Where)
{
    /// <summary>
    /// The passage is from no attachment.
    /// </summary>
    public static readonly MailPassageAttachment NONE = new(string.Empty, null);

    /// <summary>
    /// The name of the attachment, followed by where in it the passage is, when that is known.
    /// </summary>
    public string Label => this.Where is null ? this.Name : $"{this.Name}, {this.Where}";
}