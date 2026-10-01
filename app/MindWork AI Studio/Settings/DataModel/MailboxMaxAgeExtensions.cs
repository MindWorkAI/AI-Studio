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
}