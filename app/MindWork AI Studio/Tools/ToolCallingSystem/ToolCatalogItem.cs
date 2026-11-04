using AIStudio.Provider;

namespace AIStudio.Tools.ToolCallingSystem;

/// <summary>
/// A tool collection as people see it: one entry of a tool selection, one row of the tool settings.
/// </summary>
/// <remarks>
/// A tool which belongs to no declared collection forms one of its own, so it appears here like any
/// other entry, with its own name and icon. Whatever somebody decides about an entry applies to all
/// of its tools, see ToolCollectionDefinition.
/// </remarks>
public sealed class ToolCatalogItem
{
    /// <summary>
    /// The ID of the collection, which selections and settings store.
    /// </summary>
    public required string Id { get; init; }

    public required string Icon { get; init; }

    public required string DisplayName { get; init; }

    public required string Description { get; init; }

    /// <summary>
    /// What the tools of this entry do, written for a model which picks the tools of an assistant.
    /// </summary>
    public required string DescriptionForLLM { get; init; }

    /// <summary>
    /// The tools of the collection which exist here, in the order in which the collection lists them.
    /// </summary>
    public required IReadOnlyList<ToolCatalogTool> Tools { get; init; }

    /// <summary>
    /// Whether the settings of every tool are complete; otherwise the state of the first tool whose settings are not.
    /// </summary>
    public required ToolConfigurationState ConfigurationState { get; init; }

    /// <summary>
    /// Who switched the collection off: tools altogether, the organization, or the user; NONE while it may be used.
    /// </summary>
    /// <remarks>
    /// A reason rather than a yes or no, because the settings have to tell the user apart from the
    /// organization: the user can switch on again what they switched off themselves, but nothing
    /// their organization switched off.
    /// </remarks>
    public ToolOfferBlockReason SwitchOffReason { get; init; } = ToolOfferBlockReason.NONE;

    /// <summary>
    /// Whether the collection may be used at all, see ToolRegistry.IsToolActive.
    /// </summary>
    public bool IsActive => this.SwitchOffReason is ToolOfferBlockReason.NONE;

    public ConfidenceLevel MinimumProviderConfidence { get; init; } = ConfidenceLevel.NONE;
}