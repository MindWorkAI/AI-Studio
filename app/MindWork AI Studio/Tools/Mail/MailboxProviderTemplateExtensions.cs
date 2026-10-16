using AIStudio.Tools.PluginSystem;

namespace AIStudio.Tools.Mail;

public static class MailboxProviderTemplateExtensions
{
    private static string TB(string fallbackEN) => I18N.I.T(fallbackEN, typeof(MailboxProviderTemplateExtensions).Namespace, nameof(MailboxProviderTemplateExtensions));

    /// <summary>
    /// What the user enters as the username at this provider.
    /// </summary>
    public static string GetDescription(this MailboxUsernameFormat format) => format switch
    {
        MailboxUsernameFormat.EMAIL_ADDRESS => TB("Your full e-mail address."),
        MailboxUsernameFormat.ADDRESS_NAME_PART => TB("The part of your e-mail address before the @ sign. When that does not work, try your full e-mail address."),
        MailboxUsernameFormat.DOMAIN_ACCOUNT => string.Format(TB("Your account in the directory of your organization, either as {0} or as {1}. Your IT department knows which form your server expects."), @"DOMAIN\username", "username@domain"),

        _ => TB("The username your provider gave you."),
    };

    /// <summary>
    /// What the user has to do before AI Studio can sign in, one text per requirement.
    /// </summary>
    public static IEnumerable<string> GetDescriptions(this MailboxProviderRequirements requirements)
    {
        if (requirements.HasFlag(MailboxProviderRequirements.IMAP_ACTIVATION))
            yield return TB("Enable the IMAP access in the settings of your webmail first. The provider may switch it off again after a longer time without use.");

        if (requirements.HasFlag(MailboxProviderRequirements.ADMIN_ACTIVATION))
            yield return TB("Your IT department has to enable IMAP for the server and for your mailbox first.");

        if (requirements.HasFlag(MailboxProviderRequirements.APP_PASSWORD))
            yield return TB("This provider requires an app password, which you create in the security settings of your account. Your usual password does not work here.");

        if (requirements.HasFlag(MailboxProviderRequirements.APP_PASSWORD_WITH_TWO_FACTOR))
            yield return TB("When two-factor authentication is enabled for your account, this provider requires an app password, which you create in the security settings of your account.");
    }
}