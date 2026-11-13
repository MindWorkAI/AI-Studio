using AIStudio.Provider;

namespace AIStudio.Tools.ToolCallingSystem;

/// <summary>
/// What a tool collection is: tools which the user selects and trusts as one, and which an
/// organization switches off as one.
/// </summary>
/// <remarks>
/// The model still sees each tool of a collection on its own and calls it by its name. Only the
/// people see the collection, because its tools make sense only together: reading the mails which
/// a search found, with less trust than the search itself asks for, would protect nothing.<br/><br/>
/// A tool which belongs to no collection forms one of its own, under its own ID. That is why the
/// settings which were keyed by tool ID before are keyed by collection ID now, without anything
/// stored having to change. The ID of a tool in a collection stands for its collection wherever
/// settings name it, see ToolRegistry.GetCollectionId.
/// </remarks>
public sealed record ToolCollectionDefinition
{
    public string Id { get; init; } = string.Empty;

    /// <summary>
    /// The IDs of the tools in this collection, in the order in which they are listed.
    /// </summary>
    public IReadOnlyList<string> ToolIds { get; init; } = [];

    /// <summary>
    /// The lowest provider confidence the tools of this collection may be used with, unless an
    /// administrator or the user says otherwise.
    /// </summary>
    /// <remarks>
    /// The minimums of the tools themselves do not count: they would let the tools of one
    /// collection ask for different levels again.
    /// </remarks>
    public ConfidenceLevel MinimumProviderConfidence { get; init; } = ConfidenceLevel.NONE;

    /// <summary>
    /// What the tools of this collection do, written for a model which picks the tools of an assistant.
    /// </summary>
    public string DescriptionForLLM { get; init; } = string.Empty;
}