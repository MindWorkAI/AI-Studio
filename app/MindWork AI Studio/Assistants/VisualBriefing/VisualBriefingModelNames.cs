using AIStudio.Provider;

namespace AIStudio.Assistants.VisualBriefing;

/// <summary>
/// Produces export-safe model labels without exposing provider model identifiers.
/// </summary>
internal static class VisualBriefingModelNames
{
    /// <summary>
    /// Returns the configured display name or a neutral fallback.
    /// </summary>
    /// <param name="model">The selected provider model.</param>
    /// <returns>An export-safe model label.</returns>
    internal static string ExportLabel(Model model) =>
        string.IsNullOrWhiteSpace(model.DisplayName) ? "Model" : model.DisplayName.Trim();
}
