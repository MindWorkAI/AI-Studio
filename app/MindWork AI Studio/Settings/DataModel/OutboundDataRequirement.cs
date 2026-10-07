namespace AIStudio.Settings.DataModel;

/// <summary>
/// Where a chat may still send data, and which data source demands it.
/// </summary>
/// <remarks>
/// The data source travels along so a tool which is kept from running can name it, and the user
/// learns which mailbox stands in the way. It is its id rather than its name, because a mailbox
/// may be renamed after a chat read from it.
/// </remarks>
/// <param name="Restriction">Where the chat may still send data.</param>
/// <param name="DataSourceId">The id of the data source which demands the restriction, or an empty text when nothing restricts the chat.</param>
public sealed record OutboundDataRequirement(OutboundDataRestriction Restriction, string DataSourceId)
{
    /// <summary>
    /// Demands nothing: what a chat holds before it read from any mailbox, and what a result
    /// demands which brought no content of one in.
    /// </summary>
    public static readonly OutboundDataRequirement NONE = new(OutboundDataRestriction.UNRESTRICTED, string.Empty);

    /// <summary>
    /// The stricter of this requirement and another one.
    /// </summary>
    /// <remarks>
    /// On a tie, this one is kept, so the data source which set a level first stays the one which
    /// is named, and a chat does not name another mailbox with every search.
    /// </remarks>
    /// <param name="other">The other requirement.</param>
    /// <returns>The requirement whose restriction is the stricter one.</returns>
    public OutboundDataRequirement StricterOf(OutboundDataRequirement other) => other.Restriction < this.Restriction ? other : this;
}