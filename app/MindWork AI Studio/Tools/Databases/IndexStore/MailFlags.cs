namespace AIStudio.Tools.Databases.IndexStore;

/// <summary>
/// The flags of a mail at one of its locations.
/// </summary>
/// <param name="IsSeen">Whether the mail was read (\Seen).</param>
/// <param name="IsFlagged">Whether the mail is flagged (\Flagged).</param>
/// <param name="IsAnswered">Whether the mail was answered (\Answered).</param>
public readonly record struct MailFlags(bool IsSeen, bool IsFlagged, bool IsAnswered);