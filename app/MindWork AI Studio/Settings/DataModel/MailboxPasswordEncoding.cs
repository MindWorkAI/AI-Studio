namespace AIStudio.Settings.DataModel;

/// <summary>
/// How AI Studio encodes the username and the password when it signs in to an IMAP server.
/// </summary>
/// <remarks>
/// The characters of ASCII come out the same in every member, but those beyond it do not, e.g., the
/// German umlauts, ß, §, or €. Most servers expect UTF-8. Others, Microsoft Exchange among them,
/// compare the bytes with the password of the Windows account and expect Windows-1252.
/// </remarks>
public enum MailboxPasswordEncoding
{
    /// <summary>
    /// An encoding this version of AI Studio does not know, e.g., one written by a newer version. AI Studio never signs in then.
    /// </summary>
    /// <remarks>
    /// It is the member with the underlying value 0 on purpose: when the settings file holds a value
    /// TolerantEnumConverter cannot read, it falls back to that member. Falling back to an encoding
    /// instead would sign in with bytes which may not be the password, and every such attempt counts
    /// as a failed sign-in, which can lock the account.
    /// </remarks>
    UNKNOWN = 0,

    /// <summary>
    /// UTF-8, as the standard for signing in with a password demands (RFC 4616).
    /// </summary>
    UTF8,

    /// <summary>
    /// Windows-1252, the Western European code page of Windows, which Latin-1 is a part of.
    /// </summary>
    WINDOWS_1252,
}