using AIStudio.Settings;

namespace AIStudio.Chat;

/// <summary>
/// The system prompt of a chat thread as it would be sent, together with what building it decided.
/// </summary>
/// <remarks>
/// The system prompt is not the text a person typed into it. A chat template may replace it, the
/// retrieved data of a data source is appended to it, a profile adds its own paragraph, the tool
/// policy adds another, and the current date goes in front of everything. Whoever wants to know how
/// long the next request is has to ask the same question the request does, and whoever shows the
/// prompt reads its parts from here rather than repeating the decisions behind them.
/// </remarks>
/// <param name="Text">The whole system prompt, as the provider receives it.</param>
/// <param name="BasePrompt">
/// The prompt without any of the parts added around it. The thread keeps this one, so that it can
/// still say which prompt it was configured with rather than the assembled result.
/// </param>
/// <param name="Explanation">What was used, in one sentence, for the log.</param>
/// <param name="Parts">The parts the text is made of, in the order in which they stand in it.</param>
/// <param name="SwitchedOffProfile">The profile the user selected but the chat template does not let take part, or Profile.NO_PROFILE when nothing was switched off.</param>
public sealed record PreparedSystemPrompt(string Text, string BasePrompt, string Explanation, IReadOnlyList<SystemPromptPart> Parts, Profile SwitchedOffProfile);