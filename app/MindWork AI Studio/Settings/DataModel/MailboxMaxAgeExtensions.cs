using AIStudio.Tools.PluginSystem;

namespace AIStudio.Settings.DataModel;

public static class MailboxMaxAgeExtensions
{
    private static string TB(string fallbackEN) => I18N.I.T(fallbackEN, typeof(MailboxMaxAgeExtensions).Namespace, nameof(MailboxMaxAgeExtensions));

    public static string GetName(this MailboxMaxAge maxAge) => maxAge switch
    {
        MailboxMaxAge.LAST_3_MONTHS => TB("The last 3 months"),
        MailboxMaxAge.LAST_6_MONTHS => TB("The last 6 months"),
        MailboxMaxAge.LAST_12_MONTHS => TB("The last 12 months"),
        MailboxMaxAge.LAST_24_MONTHS => TB("The last 24 months"),
        MailboxMaxAge.ALL => TB("All mails"),

        _ => TB("Unknown period"),
    };

    /// <summary>
    /// The first day of the period, as the sync asks the server for it.
    /// </summary>
    /// <param name="maxAge">The period.</param>
    /// <param name="now">The current time.</param>
    /// <returns>The first day, or null when the period has no end.</returns>
    public static DateTimeOffset? GetReceivedSince(this MailboxMaxAge maxAge, DateTimeOffset now) => maxAge switch
    {
        MailboxMaxAge.LAST_6_MONTHS => now.AddMonths(-6),
        MailboxMaxAge.LAST_12_MONTHS => now.AddMonths(-12),
        MailboxMaxAge.LAST_24_MONTHS => now.AddMonths(-24),
        MailboxMaxAge.ALL => null,

        // The shortest period also stands in for a value this version does not know, as it does in the settings:
        _ => now.AddMonths(-3),
    };
}