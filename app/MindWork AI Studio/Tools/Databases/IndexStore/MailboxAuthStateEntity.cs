namespace AIStudio.Tools.Databases.IndexStore;

/// <summary>
/// A sign-in the server refused. While the row exists, AI Studio does not try to sign in again.
/// </summary>
/// <remarks>
/// The row has no foreign key on purpose. Rebuilding the index deletes the data source row and
/// everything which cascades from it, and a refused sign-in has to survive that: otherwise every
/// rebuild would try the refused password again, which is how a directory account gets locked.
/// </remarks>
internal sealed class MailboxAuthStateEntity
{
    public string DataSourceId { get; set; } = string.Empty;

    public DateTimeOffset FailedAtUtc { get; set; }

    /// <summary>
    /// What the server answered, empty when it gave no reason.
    /// </summary>
    public string FailureMessage { get; set; } = string.Empty;
}