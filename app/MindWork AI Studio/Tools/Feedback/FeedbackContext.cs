using AIStudio.Provider;

namespace AIStudio.Tools.Feedback;

/// <summary>
/// Describes the chat the feedback was given in: which AI answered.
/// </summary>
public sealed record FeedbackContext(string Provider, string Model)
{
    public static readonly FeedbackContext EMPTY = new(string.Empty, string.Empty);

    public static FeedbackContext From(AIStudio.Settings.Provider provider)
    {
        if (provider.UsedLLMProvider is LLMProviders.NONE)
            return EMPTY;

        return new(provider.UsedLLMProvider.ToName(translate: false), provider.Model.Id);
    }
}