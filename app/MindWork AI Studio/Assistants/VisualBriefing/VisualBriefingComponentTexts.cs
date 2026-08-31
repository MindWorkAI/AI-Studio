namespace AIStudio.Assistants.VisualBriefing;

/// <summary>
/// Derives assistive component text requirements from the planned component kinds.
/// </summary>
internal static class VisualBriefingComponentTexts
{
    /// <summary>
    /// Determines whether a component requires an assistive description from the content model.
    /// </summary>
    /// <param name="kind">The planned component kind.</param>
    /// <returns>Whether an accessibility text is required.</returns>
    private static bool RequiresAccessibilityText(VisualBriefingComponentKind kind) =>
        kind is VisualBriefingComponentKind.CHART or
            VisualBriefingComponentKind.SIMULATION or
            VisualBriefingComponentKind.FILTERABLE_TABLE;

    /// <summary>
    /// Determines whether a component inherits its assistive description from evidence.
    /// </summary>
    /// <param name="kind">The planned component kind.</param>
    /// <returns>Whether AI Studio supplies the accessibility text.</returns>
    internal static bool InheritsAccessibilityText(VisualBriefingComponentKind kind) => kind is VisualBriefingComponentKind.ASSET;

    /// <summary>
    /// Lists component identifiers requiring model-supplied accessibility texts.
    /// </summary>
    /// <param name="components">The planned components.</param>
    /// <returns>The component identifiers in plan order.</returns>
    internal static string[] AccessibilityTextKeys(IEnumerable<VisualBriefingPlanComponent> components) =>
    [
        .. components.Where(component => RequiresAccessibilityText(component.Kind)).Select(component => component.ComponentId)
    ];
}