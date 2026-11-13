namespace AIStudio.Tools.Databases.IndexStore;

/// <summary>
/// One address from the header of a mail.
/// </summary>
internal sealed class MailAddressEntity
{
    public int Id { get; set; }

    public string ParentFileId { get; set; } = string.Empty;

    /// <summary>
    /// The header the address comes from: FROM, SENDER, REPLY_TO, TO, CC or BCC, stored by name.
    /// </summary>
    public string Role { get; set; } = string.Empty;

    /// <summary>
    /// The place of the address within its header, starting at zero.
    /// </summary>
    public int Position { get; set; }

    public string Address { get; set; } = string.Empty;

    /// <summary>
    /// The name shown next to the address, empty when the header gives none.
    /// </summary>
    public string DisplayName { get; set; } = string.Empty;

    public MailMessageEntity? Message { get; set; }
}