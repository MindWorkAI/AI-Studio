using AIStudio.Provider;

namespace AIStudio.Chat;

/// <summary>
/// What a conversation carries into its next request, as far as a provider has stated it.
/// </summary>
/// <remarks>
/// Two parts, and only one of them is the provider's statement. PromptTokens is what the provider
/// counted for the request behind the last answer: the system prompt, the tools, and every message
/// up to the question. The answer travels in the next request as well, though not in the shape the
/// provider charged for: its completion included the model's reasoning and whatever the model wrote
/// between think tags. So the answer comes along as the text it will be sent as, and is counted the
/// same way every other text is.
///
/// That text is the answer alone. Reasoning never belongs to it, whether it was thrown away or kept
/// to be read next to the answer: it is there for a person, and no request carries it. An answer
/// which consists of reasoning only has no text at all, is not sent, and adds nothing to the prompt.
///
/// What is estimated that way is one answer, next to a prompt which holds the whole conversation
/// before it. The error is the tokenizer's error on that one answer, not on the chat.
/// </remarks>
public sealed record ReportedHistory
{
    /// <summary>
    /// The history of a conversation no report describes.
    /// </summary>
    public static readonly ReportedHistory UNKNOWN = new();

    /// <summary>
    /// Whether a report describes the conversation. When false, nothing else here means anything.
    /// </summary>
    public bool IsKnown { get; private init; }

    /// <summary>
    /// What the provider counted for the request behind the last answer.
    /// </summary>
    public int PromptTokens { get; private init; }

    /// <summary>
    /// The text of the last answer, as the next request will carry it, without any reasoning.
    /// </summary>
    public string LastAnswer { get; private init; } = string.Empty;

    /// <summary>
    /// States what a report says about the conversation.
    /// </summary>
    /// <param name="usage">What the provider reported for the request behind the last answer.</param>
    /// <param name="lastAnswer">The text of that answer.</param>
    /// <returns>The history, or UNKNOWN when the usage states nothing.</returns>
    public static ReportedHistory Of(TokenUsage usage, string lastAnswer) => usage.IsKnown
        ? new()
        {
            IsKnown = true,
            PromptTokens = usage.PromptTokens,
            LastAnswer = lastAnswer,
        }
        : UNKNOWN;
}