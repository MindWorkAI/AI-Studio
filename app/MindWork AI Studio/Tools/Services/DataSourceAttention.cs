namespace AIStudio.Tools.Services;

/// <summary>
/// What a data source waits for the user to decide, beyond the failures its list explains.
/// </summary>
public enum DataSourceAttention
{
    /// <summary>
    /// Nothing to decide.
    /// </summary>
    NONE,

    /// <summary>
    /// The server refused the sign-in, and AI Studio does not sign in again on its own.
    /// </summary>
    AUTH_FAILED,

    /// <summary>
    /// A run would remove many documents from the index at once, and waits for the user to agree.
    /// </summary>
    MASS_REMOVAL_PENDING,

    /// <summary>
    /// The organization allows only its own mail servers, and the mailbox is on another one. AI Studio does not connect to it until the organization allows the server.
    /// </summary>
    SERVER_NOT_ALLOWED,
}