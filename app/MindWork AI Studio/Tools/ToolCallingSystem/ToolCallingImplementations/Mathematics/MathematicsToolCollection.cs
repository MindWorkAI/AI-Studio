using AIStudio.Provider;
using AIStudio.Tools.PluginSystem;

namespace AIStudio.Tools.ToolCallingSystem.ToolCallingImplementations.Mathematics;

/// <summary>
/// The tools which compute locally: Calculate and Compute Symbolically.
/// </summary>
/// <remarks>
/// Both read the same syntax and share one computing slot, see MathComputationService, and a model
/// which works with formulas needs both: symbolic answers are checked with numbers, and numbers come
/// from formulas. They see nothing but what the model writes into their arguments, and nothing leaves
/// AI Studio, so the collection asks for no provider confidence. More tools of this kind, such as
/// approximations or fits, belong here; a tool which reads local data does not, since it needs a
/// confidence of its own.
/// </remarks>
public sealed class MathematicsToolCollection : IToolCollection
{
    private static string TB(string fallbackEN) => I18N.I.T(fallbackEN, typeof(MathematicsToolCollection).Namespace, nameof(MathematicsToolCollection));

    private static string TA(string fallbackEN) => I18N.I.TA(fallbackEN, typeof(MathematicsToolCollection).Namespace, nameof(MathematicsToolCollection));

    public ToolCollectionDefinition GetDefinition() => new()
    {
        Id = ToolSelectionRules.MATHEMATICS_COLLECTION_ID,
        ToolIds = [ToolSelectionRules.CALCULATE_TOOL_ID, ToolSelectionRules.COMPUTE_SYMBOLICALLY_TOOL_ID],
        MinimumProviderConfidence = ConfidenceLevel.NONE,
        DescriptionForLLM = "Calculate exactly or to a stated number of digits, and work with formulas: simplify, factor, differentiate, integrate, take limits, and solve equations. Everything is computed locally, and each result is checked numerically where possible.",
    };

    public string Icon => AppIcons.MATHEMATICS;

    public string GetDisplayName() => TB("Mathematics");

    public string GetDescription() => TA("Lets the AI calculate exactly and work with formulas, such as solving equations or checking an integral, right on your computer.");
}