using AIStudio.Chat;
using AIStudio.Chat.Compression;

namespace AIStudio.AIOperations.ConversationSummary;

/// <summary>
/// A conversation to summarize.
/// </summary>
/// <param name="SourceChat">The chat to summarize. It stays unchanged, and every request for the summary demands of the provider what it demands.</param>
/// <param name="Plan">Which messages to summarize, and which last exchange the summary leads up to, see ChatCompressionPlan.Of.</param>
/// <param name="Focus">What the user wants the summary to pay particular attention to, or an empty string.</param>
/// <param name="MaxSummaryTokens">The most tokens the summary should take, see DataChatCompression.MaxSummaryTokens.</param>
/// <param name="Progress">Learns which part is being summarized, or null.</param>
public sealed record ConversationSummaryRequest(ChatThread SourceChat, ChatCompressionPlan Plan, string Focus, int MaxSummaryTokens, IProgress<ConversationSummaryProgress>? Progress);