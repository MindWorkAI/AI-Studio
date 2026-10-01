namespace AIStudio.Tools.Databases.IndexStore;

/// <summary>
/// A sign-in the server of a mailbox refused.
/// </summary>
/// <param name="FailedAtUtc">When the server refused the sign-in.</param>
/// <param name="FailureMessage">What the server answered, empty when it gave no reason.</param>
public sealed record MailboxAuthFailure(DateTimeOffset FailedAtUtc, string FailureMessage);