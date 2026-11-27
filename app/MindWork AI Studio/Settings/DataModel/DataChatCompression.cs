using System.Linq.Expressions;

using AIStudio.Provider;

namespace AIStudio.Settings.DataModel;

/// <summary>
/// The settings of compressing a chat, and of what happens when a chat fills the context window of its model.
/// </summary>
/// <remarks>
/// The summary is written by an AI task. Unlike the other AI tasks, it has no switch for its preselection:
/// without a provider of its own, the model of the chat writes the summary, and that model saw everything
/// the summary reads anyway.
/// </remarks>
public sealed class DataChatCompression(Expression<Func<Data, DataChatCompression>>? configSelection = null)
{
    /// <summary>
    /// The lowest share of the context window, in percent, from which AI Studio acts before sending.
    /// </summary>
    /// <remarks>
    /// A summary takes up to a quarter of the window. Below half of it, a compressed chat could reach the
    /// share again right away, and compressing would never be offered.
    /// </remarks>
    public const int MIN_CONTEXT_WINDOW_THRESHOLD_PERCENT = 50;

    /// <summary>
    /// The highest share of the context window, in percent, from which AI Studio acts before sending.
    /// </summary>
    /// <remarks>
    /// The rest of the window holds the answer of the model.
    /// </remarks>
    public const int MAX_CONTEXT_WINDOW_THRESHOLD_PERCENT = 95;

    /// <summary>
    /// The fewest tokens which a summary may be limited to.
    /// </summary>
    public const int MIN_SUMMARY_TOKENS = 1_024;

    /// <summary>
    /// The most tokens which a summary may be limited to.
    /// </summary>
    public const int MAX_SUMMARY_TOKENS = 32_768;

    /// <summary>
    /// The default constructor for the JSON deserializer.
    /// </summary>
    public DataChatCompression() : this(null)
    {
    }

    /// <summary>
    /// The provider which writes the summary; empty for the model of the chat.
    /// </summary>
    public string PreselectedProvider { get; set; } = ManagedConfiguration.Register(configSelection, n => n.PreselectedProvider, string.Empty);

    /// <summary>
    /// The minimum confidence level of the provider which writes the summary.
    /// </summary>
    /// <remarks>
    /// It applies to the preselected provider only. When the model of the chat writes the summary instead,
    /// the minimum does not apply: that model saw everything the summary reads anyway.
    /// </remarks>
    public ConfidenceLevel MinimumProviderConfidence { get; set; } = ManagedConfiguration.Register(configSelection, n => n.MinimumProviderConfidence, ConfidenceLevel.NONE);

    /// <summary>
    /// What the summary should pay particular attention to, filled into the dialog of compressing; empty for nothing.
    /// </summary>
    public string PreselectedFocus { get; set; } = ManagedConfiguration.Register(configSelection, n => n.PreselectedFocus, string.Empty);

    /// <summary>
    /// The share of the context window, in percent, from which AI Studio acts before sending a message.
    /// </summary>
    /// <remarks>
    /// Between MIN_CONTEXT_WINDOW_THRESHOLD_PERCENT and MAX_CONTEXT_WINDOW_THRESHOLD_PERCENT. What AI Studio
    /// does then decides ContextWindowFullBehavior.
    /// </remarks>
    public int ContextWindowThresholdPercent { get; set; } = ManagedConfiguration.Register(configSelection, n => n.ContextWindowThresholdPercent, MAX_CONTEXT_WINDOW_THRESHOLD_PERCENT);

    /// <summary>
    /// The most tokens a summary should take.
    /// </summary>
    /// <remarks>
    /// Between MIN_SUMMARY_TOKENS and MAX_SUMMARY_TOKENS. A summary never takes more than a quarter of the
    /// context window of the model which writes it, nor of the model of the chat.<br/><br/>
    ///
    /// Context windows of 250,000 tokens and more are common by now. A summary of 8,192 tokens takes a small
    /// share of them, yet keeps room for the details of a long conversation. A summary meant to save tokens has
    /// no business being much longer anyway.
    /// </remarks>
    public int MaxSummaryTokens { get; set; } = ManagedConfiguration.Register(configSelection, n => n.MaxSummaryTokens, 8_192);

    /// <summary>
    /// What AI Studio does when a message would fill the context window of the model.
    /// </summary>
    public ContextWindowFullBehavior ContextWindowFullBehavior { get; set; } = ManagedConfiguration.Register(configSelection, n => n.ContextWindowFullBehavior, ContextWindowFullBehavior.ASK);

    /// <summary>
    /// Whether a share of the context window is one AI Studio may act from.
    /// </summary>
    /// <param name="percent">The share in percent.</param>
    /// <returns>True between MIN_CONTEXT_WINDOW_THRESHOLD_PERCENT and MAX_CONTEXT_WINDOW_THRESHOLD_PERCENT.</returns>
    public static bool IsValidContextWindowThresholdPercent(int percent) => percent is >= MIN_CONTEXT_WINDOW_THRESHOLD_PERCENT and <= MAX_CONTEXT_WINDOW_THRESHOLD_PERCENT;

    /// <summary>
    /// Whether a number of tokens is one a summary may be limited to.
    /// </summary>
    /// <param name="tokens">The tokens.</param>
    /// <returns>True between MIN_SUMMARY_TOKENS and MAX_SUMMARY_TOKENS.</returns>
    public static bool IsValidMaxSummaryTokens(int tokens) => tokens is >= MIN_SUMMARY_TOKENS and <= MAX_SUMMARY_TOKENS;
}