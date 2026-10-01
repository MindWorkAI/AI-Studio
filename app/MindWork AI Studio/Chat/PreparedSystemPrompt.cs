namespace AIStudio.Chat;

/// <summary>
/// The system prompt of a chat thread as it would be sent, together with what building it decided.
/// </summary>
/// <remarks>
/// The system prompt is not the text a person typed into it. A chat template may replace it, the
/// retrieved data of a data source is appended to it, a profile adds its own paragraph, the tool
/// policy adds another, and the current date goes in front of everything. Whoever wants to know how
/// long the next request is has to ask the same question the request does.
/// </remarks>
/// <param name="Text">The whole system prompt, as the provider receives it.</param>
/// <param name="BasePrompt">
/// The prompt without any of the parts added around it. The thread keeps this one, so that it can
/// still say which prompt it was configured with rather than the assembled result.
/// </param>
/// <param name="ProfileIsAllowed">Whether the chat template let a profile take part.</param>
/// <param name="Explanation">What was used, in one sentence, for the log.</param>
public sealed record PreparedSystemPrompt(string Text, string BasePrompt, bool ProfileIsAllowed, string Explanation);