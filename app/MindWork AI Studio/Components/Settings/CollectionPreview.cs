namespace AIStudio.Components.Settings;

/// <summary>
/// What a <see cref="CollectionSection"/> shows of its collection: the entries in the order to show them,
/// and how many more the page of the collection holds.
/// </summary>
/// <remarks>
/// The section shows exactly this and decides nothing itself, so each collection keeps its own order and
/// grouping. Whoever builds a preview gives a visible reason for the entries it picks: a label, or the
/// entries are the start of the list on the page of the collection, in the same order.
/// </remarks>
/// <param name="Items">The entries to show.</param>
/// <param name="Overflow">How many entries the page holds beyond these, shown as "+N more".</param>
/// <param name="Label">A small label in front of the entries, which says why these show, such as "By company".</param>
public sealed record CollectionPreview(IReadOnlyList<CollectionPreviewItem> Items, int Overflow, string? Label = null);

/// <summary>
/// One entry of a <see cref="CollectionPreview"/>.
/// </summary>
/// <param name="Id">What tells the entry apart from the others.</param>
/// <param name="Name">The name the entry shows.</param>
/// <param name="Open">What a click on the entry does, such as opening it. Without, the entry is plain text, since a chip which leads nowhere would only mislead.</param>
/// <param name="Icon">An icon in front of the name, if any.</param>
public sealed record CollectionPreviewItem(string Id, string Name, Func<Task>? Open = null, string? Icon = null);