using AIStudio.Settings;

using ComponentKind = AIStudio.Tools.Components;

namespace AIStudio.Assistants.ModelComparison;

/// <summary>
/// Decides whether the pages of the Model Comparison assistant may be opened.
/// </summary>
/// <remarks>
/// An organization can hide the assistant like any other. The tile asks the same question; the pages which do not
/// inherit AssistantBase ask here, because somebody might reach them without the tile, e.g., from a link or the
/// browser history.
/// </remarks>
public static class ModelComparisonAccess
{
    /// <summary>
    /// Checks whether the Model Comparison assistant may be opened with the given settings.
    /// </summary>
    /// <param name="settingsManager">The settings to check.</param>
    /// <param name="assistantName">The name of the assistant for the log; without one, nothing gets logged.</param>
    /// <returns>True, when no configuration hides the assistant.</returns>
    public static bool CanOpen(SettingsManager settingsManager, string assistantName = "") => settingsManager.IsAssistantVisible(ComponentKind.MODEL_COMPARISON_ASSISTANT, assistantName: assistantName);
}