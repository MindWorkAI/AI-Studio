using System.Collections.Immutable;

using AIStudio.Provider;
using AIStudio.Settings.DataModel;
using AIStudio.Tools.Web;

namespace AIStudio.Chat;

/// <summary>
/// What a chat demands of everybody who continues with its content, because of what it read.
/// </summary>
/// <remarks>
/// The things a thread ratchets, see ChatThread.RequiredProviderConfidence, ChatThread.DataSecurity,
/// ChatThread.RequiredOutboundDataRestriction, and ChatThread.WebAddressesWrittenByModels, as one value which can
/// leave the thread. Content which goes from one thread to another carries what its source demands: a chat which
/// read mails demands the same of an assistant its answer is sent to. Each part only ever tightens, and NONE
/// demands nothing.
/// </remarks>
/// <param name="ProviderConfidence">The minimum confidence of a provider which may see the content.</param>
/// <param name="DataSecurity">Whether only self-hosted providers may see the content; NOT_SPECIFIED demands nothing.</param>
/// <param name="OutboundData">Where the content may still be sent, and which source demands it.</param>
/// <param name="WebAddressesWrittenByModels">The web addresses a model wrote into the content, as request keys, see WebAddresses.CreateRequestKey. They never count as given by the user, wherever the content goes.</param>
public sealed record ChatRestrictions(ConfidenceLevel ProviderConfidence, DataSourceSecurity DataSecurity, OutboundDataRequirement OutboundData, ImmutableHashSet<string> WebAddressesWrittenByModels)
{
    /// <summary>
    /// Demands nothing: what a chat holds before it read anything which restricts it.
    /// </summary>
    public static readonly ChatRestrictions NONE = new(ConfidenceLevel.NONE, DataSourceSecurity.NOT_SPECIFIED, OutboundDataRequirement.NONE, []);

    /// <summary>
    /// The restrictions which demand everything this one and another one demand.
    /// </summary>
    /// <param name="other">The other restrictions.</param>
    /// <returns>The stricter one of each part, and the addresses written by models of both.</returns>
    public ChatRestrictions StricterOf(ChatRestrictions other) => new(
        other.ProviderConfidence > this.ProviderConfidence ? other.ProviderConfidence : this.ProviderConfidence,
        this.DataSecurity.TightenedBy(other.DataSecurity),
        this.OutboundData.StricterOf(other.OutboundData),
        this.WebAddressesWrittenByModels.Union(other.WebAddressesWrittenByModels));

    /// <summary>
    /// These restrictions, with every web address in a text a model wrote.
    /// </summary>
    /// <remarks>
    /// For a text which leaves the place where a model wrote it, such as an answer sent to an assistant. There, the
    /// text becomes a message of the user, and its addresses would pass for ones the user gave, see
    /// ChatThread.IsWebAddressGivenToTheModel. A model could have put anything of the chat it came from into them.
    /// </remarks>
    /// <param name="text">The text a model wrote.</param>
    /// <returns>The restrictions, with the request keys of the addresses in the text added.</returns>
    public ChatRestrictions WithWebAddressesWrittenByModelIn(string text)
    {
        var requestKeys = this.WebAddressesWrittenByModels.ToBuilder();
        foreach (var address in WebAddresses.Find(text))
            if (WebAddresses.TryCreateRequestKey(address, out var requestKey))
                requestKeys.Add(requestKey);

        return this with { WebAddressesWrittenByModels = requestKeys.ToImmutable() };
    }
}
