namespace AIStudio.Settings.DataModel;

/// <summary>
/// How the connection to an IMAP server is encrypted. There is deliberately no way without encryption.
/// </summary>
public enum MailboxTransportSecurity
{
    /// <summary>
    /// A way this version of AI Studio does not know, e.g., one written by a newer version. AI Studio does not connect then.
    /// </summary>
    /// <remarks>
    /// It is the member with the underlying value 0 on purpose: when the settings file holds a value
    /// TolerantEnumConverter cannot read, it falls back to that member. Falling back to a way of
    /// connecting instead would mean guessing how the password travels.
    /// </remarks>
    UNKNOWN = 0,

    /// <summary>
    /// TLS from the first byte on, usually on port 993.
    /// </summary>
    SSL_ON_CONNECT,

    /// <summary>
    /// A plain connection which switches to TLS before signing in, usually on port 143. When the server does not offer the switch, AI Studio does not connect.
    /// </summary>
    STARTTLS,
}