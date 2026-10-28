using AIStudio.Provider;
using AIStudio.Settings.DataModel;

namespace AIStudio.Chat;

/// <summary>
/// What a chat demands of everybody who continues with its content, because of what it read.
/// </summary>
/// <remarks>
/// The three things a thread ratchets, see ChatThread.RequiredProviderConfidence, ChatThread.DataSecurity, and
/// ChatThread.RequiredOutboundDataRestriction, as one value which can leave the thread. Content which goes from one
/// thread to another carries what its source demands: a chat which read mails demands the same of an assistant its
/// answer is sent to. Each part only ever tightens, and NONE demands nothing.
/// </remarks>
/// <param name="ProviderConfidence">The minimum confidence of a provider which may see the content.</param>
/// <param name="DataSecurity">Whether only self-hosted providers may see the content; NOT_SPECIFIED demands nothing.</param>
/// <param name="OutboundData">Where the content may still be sent, and which source demands it.</param>
public sealed record ChatRestrictions(ConfidenceLevel ProviderConfidence, DataSourceSecurity DataSecurity, OutboundDataRequirement OutboundData)
{
    /// <summary>
    /// Demands nothing: what a chat holds before it read anything which restricts it.
    /// </summary>
    public static readonly ChatRestrictions NONE = new(ConfidenceLevel.NONE, DataSourceSecurity.NOT_SPECIFIED, OutboundDataRequirement.NONE);

    /// <summary>
    /// The restrictions which demand everything this one and another one demand.
    /// </summary>
    /// <param name="other">The other restrictions.</param>
    /// <returns>The stricter one of each part.</returns>
    public ChatRestrictions StricterOf(ChatRestrictions other) => new(
        other.ProviderConfidence > this.ProviderConfidence ? other.ProviderConfidence : this.ProviderConfidence,
        this.DataSecurity.TightenedBy(other.DataSecurity),
        this.OutboundData.StricterOf(other.OutboundData));
}
