using AIStudio.Tools.PluginSystem;

namespace AIStudio.Tools.Mail;

public static class MailboxProviderTemplateExtensions
{
    private static string TA(string fallbackEN) => I18N.I.TA(fallbackEN, typeof(MailboxProviderTemplateExtensions).Namespace, nameof(MailboxProviderTemplateExtensions));

    extension(MailboxUsernameFormat format)
    {
        /// <summary>
        /// What the user enters as the username at this provider.
        /// </summary>
        public string Description => format switch
        {
            MailboxUsernameFormat.EMAIL_ADDRESS => TA("Your full e-mail address."),
            MailboxUsernameFormat.ADDRESS_NAME_PART => TA("The part of your e-mail address before the @ sign. When that does not work, try your full e-mail address."),
            MailboxUsernameFormat.DOMAIN_ACCOUNT => string.Format(TA("Your account in the directory of your organization, either as {0} or as {1}. Your IT department knows which form your server expects."), @"DOMAIN\username", "username@domain"),

            _ => TA("The username your provider gave you."),
        };
    }

    extension(MailboxProviderRequirements requirements)
    {
        /// <summary>
        /// What the user has to do before AI Studio can sign in, one text per requirement.
        /// </summary>
        public IEnumerable<string> GetDescriptions()
        {
            if (requirements.HasFlag(MailboxProviderRequirements.IMAP_ACTIVATION))
                yield return TA("Enable the IMAP access in the settings of your webmail first. The provider may switch it off again after a longer time without use.");

            if (requirements.HasFlag(MailboxProviderRequirements.ADMIN_ACTIVATION))
                yield return TA("Your IT department has to enable IMAP for the server and for your mailbox first.");

            if (requirements.HasFlag(MailboxProviderRequirements.APP_PASSWORD))
                yield return TA("This provider requires an app password, which you create in the security settings of your account. Your usual password does not work here.");

            if (requirements.HasFlag(MailboxProviderRequirements.APP_PASSWORD_WITH_TWO_FACTOR))
                yield return TA("When two-factor authentication is enabled for your account, this provider requires an app password, which you create in the security settings of your account.");
        }
    }
}