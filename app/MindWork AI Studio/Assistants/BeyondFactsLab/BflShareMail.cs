namespace AIStudio.Assistants.BeyondFactsLab;

/// <summary>
/// Prepares the mail with which a person sends a shared session to the research team of the BeyondFacts Lab.
/// </summary>
/// <remarks>
/// The lab writes no mail itself: a mailto link opens the mail program of the person with the address, the subject,
/// and a short text, and the person attaches the exported file and sends it, from their own address. The link holds
/// nothing of the session, since mail programs accept only short links; the runtime opens it outside the app.
/// </remarks>
public static class BflShareMail
{
    private static string TB(string fallbackEN) => Tools.PluginSystem.I18N.I.T(fallbackEN, typeof(BflShareMail).Namespace, nameof(BflShareMail));

    /// <summary>
    /// The address of the research team.
    /// </summary>
    /// <remarks>
    /// A placeholder until the research team names its address; it is replaced before the release which brings the
    /// lab, as the lab is a prototype until then.
    /// </remarks>
    public const string RECIPIENT = "xxx@xxx.xxx";

    /// <summary>
    /// Who receives the mail, as the lab names her.
    /// </summary>
    public const string RECIPIENT_NAME = "Lara Wüster (TUM)";

    /// <summary>
    /// The longest link the lab hands to a mail program. Some programs on Windows refuse longer ones.
    /// </summary>
    public const int MAX_URI_LENGTH = 1_800;

    /// <summary>
    /// The line break of a mailto link, which RFC 6068 demands as CR LF.
    /// </summary>
    private const string LINE_BREAK = "\r\n";

    /// <summary>
    /// Gets the subject of the mail, in the language of the app.
    /// </summary>
    /// <remarks>
    /// The mail speaks to the research team, not to the person, so neither the subject nor the text addresses anybody:
    /// the form of address the person chose for the lab does not fit the research team.
    /// </remarks>
    public static string Subject => TB("A shared session of the BeyondFacts Lab");

    /// <summary>
    /// Gets the text of the mail, in the language of the app.
    /// </summary>
    public static string Body => TB("""
        Hello,

        attached is a session of the BeyondFacts Lab, which I share for the research on the lab. Before I exported it, AI Studio removed the personal details it recognized.

        Kind regards
        """);

    /// <summary>
    /// Creates the link which opens the mail to the research team.
    /// </summary>
    public static string CreateUri() => CreateUri(RECIPIENT, Subject, Body);

    /// <summary>
    /// Creates a mailto link, with everything encoded as RFC 6068 demands.
    /// </summary>
    /// <remarks>
    /// A link longer than MAX_URI_LENGTH loses the end of its text, at a word, so that no mail program refuses it.
    /// </remarks>
    /// <param name="recipient">The address.</param>
    /// <param name="subject">The subject.</param>
    /// <param name="body">The text, with line breaks of any kind.</param>
    /// <returns>The link.</returns>
    public static string CreateUri(string recipient, string subject, string body)
    {
        var at = recipient.LastIndexOf('@');
        var address = at < 0 ? Uri.EscapeDataString(recipient) : $"{Uri.EscapeDataString(recipient[..at])}@{Uri.EscapeDataString(recipient[(at + 1)..])}";
        var prefix = $"mailto:{address}?subject={Uri.EscapeDataString(subject.ReplaceLineEndings(" "))}";

        var text = body.Trim().ReplaceLineEndings("\n");
        var uri = Compose(prefix, text);
        while (uri.Length > MAX_URI_LENGTH && text.Length > 0)
        {
            var cut = text.LastIndexOfAny([' ', '\n']);
            text = cut > 0 ? text[..cut].TrimEnd() : string.Empty;
            uri = Compose(prefix, text);
        }

        return uri;
    }

    /// <summary>
    /// Puts the link together from its beginning and its text.
    /// </summary>
    /// <param name="prefix">The link up to its subject.</param>
    /// <param name="text">The text, with LF line breaks.</param>
    private static string Compose(string prefix, string text) => text.Length is 0 ? prefix : $"{prefix}&body={Uri.EscapeDataString(text.Replace("\n", LINE_BREAK))}";
}