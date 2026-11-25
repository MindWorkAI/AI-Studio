namespace AIStudio.Tools.Validation;

/// <summary>
/// The first character of an API key which cannot be sent.
/// </summary>
/// <remarks>
/// The key itself never goes in here: this value ends up in messages, and its kind in the log.
/// Only a character the user can see is kept, so that a message can name it.
/// </remarks>
/// <param name="Kind">What is wrong with the character.</param>
/// <param name="Character">The character as the user sees it, or empty unless it is a visible character outside of ASCII.</param>
public readonly record struct APIKeyIssue(APIKeyIssueKind Kind, string Character)
{
    public static readonly APIKeyIssue NONE = new(APIKeyIssueKind.NONE, string.Empty);

    /// <summary>
    /// Whether the key cannot be sent.
    /// </summary>
    public bool IsProblem => this.Kind is not APIKeyIssueKind.NONE;
}