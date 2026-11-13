namespace AIStudio.Settings.DataModel;

/// <summary>
/// How far back the index of a mailbox reaches, by the date the server received a mail. Flagged mails and drafts are indexed regardless of their age.
/// </summary>
public enum MailboxMaxAge
{
    // The shortest period is deliberately the member with the underlying value 0: when the settings
    // file holds a value TolerantEnumConverter cannot read, it falls back to that member. A shorter
    // period than the user chose removes mails from the index, which the protection against mass
    // removal asks about first. A longer one would embed mails nobody asked for, which takes hours
    // for a large mailbox and costs money with a cloud embedding.
    LAST_3_MONTHS = 0,

    LAST_6_MONTHS,
    LAST_12_MONTHS,
    LAST_24_MONTHS,
    ALL,
}