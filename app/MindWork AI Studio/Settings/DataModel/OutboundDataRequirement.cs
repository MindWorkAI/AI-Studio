using System.Text.Json.Serialization;

namespace AIStudio.Settings.DataModel;

/// <summary>
/// Where a chat may still send data, and which source of internal content demands it.
/// </summary>
/// <remarks>
/// The source travels along so a tool which is kept from running can name it, and the user
/// learns what stands in the way. For a mailbox, the reference is its id rather than its name,
/// because a mailbox may be renamed after a chat read from it.
/// </remarks>
/// <param name="Restriction">Where the chat may still send data.</param>
/// <param name="Origin">What kind of source demands the restriction. Chats saved before it existed only knew mailboxes, so a missing value reads as one.</param>
/// <param name="Reference">Which source of its kind demands the restriction: the id of a mailbox, or the host of a private web page. An empty text when the kind says it all, as for the wiki, or when nothing restricts the chat.</param>
public sealed record OutboundDataRequirement(
    OutboundDataRestriction Restriction,
    OutboundDataOrigin Origin,
    [property: JsonPropertyName("data_source_id")] string Reference)
{
    /// <summary>
    /// Demands nothing: what a chat holds before it read any internal content, and what a result
    /// demands which brought none in.
    /// </summary>
    public static readonly OutboundDataRequirement NONE = new(OutboundDataRestriction.UNRESTRICTED, OutboundDataOrigin.MAILBOX, string.Empty);

    /// <summary>
    /// The stricter of this requirement and another one.
    /// </summary>
    /// <remarks>
    /// On a tie, this one is kept, whatever the origin of either, so the source which set a level
    /// first stays the one which is named, and a chat does not name another source with every
    /// search.
    /// </remarks>
    /// <param name="other">The other requirement.</param>
    /// <returns>The requirement whose restriction is the stricter one.</returns>
    public OutboundDataRequirement StricterOf(OutboundDataRequirement other) => other.Restriction < this.Restriction ? other : this;
}