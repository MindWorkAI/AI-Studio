namespace AIStudio.Tools.Databases.IndexStore;

/// <summary>
/// A sign-in the server of a mailbox refused.
/// </summary>
/// <param name="FailedAtUtc">When the server refused the sign-in.</param>
/// <param name="FailureMessage">What the server answered, empty when it gave no reason.</param>
public sealed record MailboxAuthFailure(DateTimeOffset FailedAtUtc, string FailureMessage)
{
    /// <summary>
    /// How much of the answer of the server is kept. The reason is in the first sentence, if at all.
    /// </summary>
    public const int MAX_FAILURE_MESSAGE_LENGTH = 500;

    /// <summary>
    /// Records a sign-in the server refused just now, together with what it answered.
    /// </summary>
    /// <remarks>
    /// The answer often says why, e.g., that the provider requires an app password. It may name
    /// the user as well, which is why it is kept for the settings of the mailbox and never logged.
    /// </remarks>
    /// <param name="serverAnswer">What the server answered.</param>
    /// <returns>The refused sign-in.</returns>
    public static MailboxAuthFailure FromServerAnswer(string serverAnswer)
    {
        var trimmedAnswer = serverAnswer.Trim();
        return new(DateTimeOffset.UtcNow, trimmedAnswer.Length > MAX_FAILURE_MESSAGE_LENGTH ? trimmedAnswer[..MAX_FAILURE_MESSAGE_LENGTH] : trimmedAnswer);
    }
}