using AIStudio.Tools.ERIClient.DataModel;
using AIStudio.Tools.PluginSystem;

namespace AIStudio.Tools.Validation;

/// <summary>
/// Checks whether the username, the password, or the access token of an ERI data source can be sent to its server at all.
/// </summary>
/// <remarks>
/// <para>
/// ERI v1 sends all three in HTTP headers, which carry nothing but ASCII. The check is the one for API
/// keys in APIKeyValidation: the whitespace around a value goes, since HTTP drops it anyway, and
/// everything else is reported instead of removed. An access token is checked exactly like an API key.
/// A password and a username may contain spaces, since people choose them, but no other whitespace.
/// </para>
/// <para>
/// A message about a password never names the character: the field hides the password, and the
/// message would show a part of it. A message for a stored value names no character at all, since
/// the callers of the ERI client write such messages to the log.
/// </para>
/// </remarks>
public static class ERICredentialValidation
{
    private static string TA(string fallbackEN) => I18N.I.TA(fallbackEN, typeof(ERICredentialValidation).Namespace, nameof(ERICredentialValidation));

    /// <summary>
    /// Removes the whitespace around a username, a password, or an access token.
    /// </summary>
    /// <param name="value">The value as the user entered it.</param>
    /// <returns>The value without the whitespace around it, or an empty string for no value.</returns>
    public static string Normalize(string? value) => APIKeyValidation.Normalize(value);

    /// <summary>
    /// Finds the first character of a password or an access token which cannot be sent.
    /// </summary>
    /// <param name="authMethod">How the data source signs in.</param>
    /// <param name="secret">The password or the access token.</param>
    /// <returns>The first issue, or NONE when the secret can be sent or the auth method needs none.</returns>
    public static APIKeyIssue CheckSecret(AuthMethod authMethod, string? secret) => authMethod switch
    {
        AuthMethod.TOKEN => APIKeyValidation.Check(secret),
        AuthMethod.USERNAME_PASSWORD => APIKeyValidation.Check(secret, allowsSpaces: true),

        _ => APIKeyIssue.NONE,
    };

    /// <summary>
    /// Finds the first character of a username which cannot be sent.
    /// </summary>
    /// <param name="username">The username.</param>
    /// <returns>The first issue, or NONE when the username can be sent.</returns>
    public static APIKeyIssue CheckUsername(string? username) => APIKeyValidation.Check(username, allowsSpaces: true);

    /// <summary>
    /// Checks a password or an access token the user is entering.
    /// </summary>
    /// <param name="authMethod">How the data source signs in.</param>
    /// <param name="secret">The password or the access token as the user entered it.</param>
    /// <returns>What is wrong with the secret, or null when it can be sent.</returns>
    public static string? ValidateSecret(AuthMethod authMethod, string? secret)
    {
        var issue = CheckSecret(authMethod, secret);
        if (!issue.IsProblem)
            return null;

        return authMethod is AuthMethod.TOKEN ? GetTokenMessage(issue) : GetPasswordMessage(issue);
    }

    /// <summary>
    /// Checks a username the user is entering.
    /// </summary>
    /// <param name="username">The username as the user entered it.</param>
    /// <returns>What is wrong with the username, or null when it can be sent.</returns>
    public static string? ValidateUsername(string? username)
    {
        var issue = CheckUsername(username);
        return issue.IsProblem ? GetUsernameMessage(issue) : null;
    }

    /// <summary>
    /// Tells the user that the stored password or access token of a data source cannot be sent.
    /// </summary>
    /// <param name="authMethod">How the data source signs in.</param>
    /// <param name="dataSourceName">The name of the data source.</param>
    /// <returns>The message, which names no character.</returns>
    public static string GetUnusableSecretMessage(AuthMethod authMethod, string dataSourceName) => authMethod is AuthMethod.TOKEN
        ? string.Format(TA("The access token of the data source '{0}' contains a character which an access token cannot contain, such as a line break or an invisible character. Please enter the token again in the settings of the data source. If your organization provides the data source, please contact your IT department."), dataSourceName)
        : string.Format(TA("The password of the data source '{0}' contains a character which the ERI server cannot receive, such as an umlaut, a line break, or an invisible character. Please enter the password again in the settings of the data source. If your organization provides the data source, please contact your IT department."), dataSourceName);

    /// <summary>
    /// Tells the user that the username for a data source cannot be sent.
    /// </summary>
    /// <param name="dataSourceName">The name of the data source.</param>
    /// <returns>The message, which names no character.</returns>
    public static string GetUnusableUsernameMessage(string dataSourceName) => string.Format(TA("The username for the data source '{0}' contains a character which the ERI server cannot receive, such as an umlaut. Please check the username in the settings of the data source. If your organization provides the data source, please contact your IT department."), dataSourceName);

    private static string GetTokenMessage(APIKeyIssue issue) => issue.Kind switch
    {
        APIKeyIssueKind.WHITESPACE => TA("The access token contains a space or a line break, which an access token cannot contain. Please check whether you copied more than the token."),
        APIKeyIssueKind.INVISIBLE_CHARACTER => TA("The access token contains an invisible character, which often comes along when you copy text from a web page or a document. Please delete the token and enter it again."),
        APIKeyIssueKind.NON_ASCII_CHARACTER => string.Format(TA("The access token contains the character '{0}', which an access token cannot contain. Word processors, for example, turn quotation marks and dashes into such characters. Please check the token."), issue.Character),

        _ => string.Empty,
    };

    private static string GetPasswordMessage(APIKeyIssue issue) => issue.Kind is APIKeyIssueKind.NON_ASCII_CHARACTER
        ? TA("The password contains a character which the ERI server cannot receive, such as an umlaut, 'ß', or a typographic quotation mark: only letters without accents, digits, spaces, and common punctuation get through. Please check the password. If it really contains such a character, please contact your IT department.")
        : TA("The password contains a line break, a tab, a special space such as a non-breaking space, or an invisible character, which the ERI server cannot receive. Ordinary spaces are fine. Please enter the password again.");

    private static string GetUsernameMessage(APIKeyIssue issue) => issue.Kind is APIKeyIssueKind.NON_ASCII_CHARACTER
        ? string.Format(TA("The username contains the character '{0}', which the ERI server cannot receive: only letters without accents, digits, spaces, and common punctuation get through. Please check the username. If it really contains such a character, please contact your IT department."), issue.Character)
        : TA("The username contains a line break, a tab, a special space such as a non-breaking space, or an invisible character, which the ERI server cannot receive. Ordinary spaces are fine. Please enter the username again.");
}