namespace AIStudio.Settings.DataModel;

/// <summary>
/// How AI Studio signs in to an IMAP server.
/// </summary>
public enum MailboxAuthMethod
{
    /// <summary>
    /// A method this version of AI Studio does not know, e.g., one written by a newer version. AI Studio never signs in then.
    /// </summary>
    /// <remarks>
    /// It is the member with the underlying value 0 on purpose: when the settings file holds a value
    /// TolerantEnumConverter cannot read, it falls back to that member. Falling back to a password
    /// instead would sign in with a secret meant for something else, and every such attempt counts
    /// as a failed sign-in, which can lock the account.
    /// </remarks>
    UNKNOWN = 0,

    /// <summary>
    /// A username and a password, or an app password where the provider asks for one.
    /// </summary>
    PASSWORD,
}