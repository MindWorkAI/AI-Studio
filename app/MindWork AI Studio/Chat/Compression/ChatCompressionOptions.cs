using AIStudio.Settings.DataModel;

namespace AIStudio.Chat.Compression;

/// <summary>
/// The settings of compressing a chat, read once for one decision or one run.
/// </summary>
/// <remarks>
/// Several chats, embedded chats, and assistants may ask and compress at the same time, while the user changes the
/// settings. Each of them reads them once into this snapshot, so that one run never mixes old and new values, and the
/// pure functions which decide get them as a parameter instead of reaching for the settings.
/// </remarks>
/// <param name="ShareToAsk">The share of the context window from which AI Studio acts before sending, e.g., 0.95.</param>
/// <param name="MaxSummaryTokens">The most tokens a summary should take.</param>
/// <param name="PreselectedFocus">What the summary should pay particular attention to, filled into the dialog; empty for nothing.</param>
/// <param name="WhenContextWindowFills">What AI Studio does when a message would fill the context window.</param>
public sealed record ChatCompressionOptions(double ShareToAsk, int MaxSummaryTokens, string PreselectedFocus, ContextWindowFullBehavior WhenContextWindowFills)
{
    /// <summary>
    /// The settings as AI Studio ships them.
    /// </summary>
    public static readonly ChatCompressionOptions DEFAULT = Of(new DataChatCompression());

    /// <summary>
    /// Reads the settings of compressing a chat.
    /// </summary>
    /// <remarks>
    /// A value outside its range, e.g., one written into the settings file by hand, is brought into it, since a
    /// configuration plugin cannot set one, see DataChatCompression.IsValidContextWindowThresholdPercent.
    /// </remarks>
    /// <param name="settings">The settings to read.</param>
    /// <returns>The snapshot of the settings.</returns>
    public static ChatCompressionOptions Of(DataChatCompression settings) => new(
        Math.Clamp(settings.ContextWindowThresholdPercent, DataChatCompression.MIN_CONTEXT_WINDOW_THRESHOLD_PERCENT, DataChatCompression.MAX_CONTEXT_WINDOW_THRESHOLD_PERCENT) / 100d,
        Math.Clamp(settings.MaxSummaryTokens, DataChatCompression.MIN_SUMMARY_TOKENS, DataChatCompression.MAX_SUMMARY_TOKENS),
        settings.PreselectedFocus,
        settings.ContextWindowFullBehavior);
}