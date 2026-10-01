using AIStudio.Tools.Mail;

namespace AIStudio.Tools.Databases.IndexStore;

/// <summary>
/// One address from the header of a mail.
/// </summary>
/// <param name="Role">The header the address comes from.</param>
/// <param name="Address">The address itself.</param>
/// <param name="DisplayName">The name shown next to it, empty when the header gives none.</param>
public sealed record MailAddressRecord(MailAddressRole Role, string Address, string DisplayName);