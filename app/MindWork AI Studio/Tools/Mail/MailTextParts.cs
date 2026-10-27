namespace AIStudio.Tools.Mail;

/// <summary>
/// The text parts of a mail as the server sent them, decoded from their transfer encoding and charset.
/// </summary>
/// <param name="HtmlBody">The HTML part a reader sees, or null when there is none or it is too large to fetch.</param>
/// <param name="TextBody">The plain text part, or null likewise.</param>
public sealed record MailTextParts(string? HtmlBody, string? TextBody);