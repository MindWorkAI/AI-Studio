using AIStudio.Settings;

using ComponentKind = AIStudio.Tools.Components;

namespace AIStudio.Assistants.BeyondFactsLab;

/// <summary>
/// Decides whether the BeyondFacts Lab may be opened.
/// </summary>
/// <remarks>
/// The lab is a prototype behind its own preview feature, and an organization can hide it like any other
/// assistant. The tile asks the same question through its required preview feature; the page asks here,
/// because somebody might reach its address without the tile, e.g., from a link or the browser history.
/// </remarks>
public static class BflAccess
{
    /// <summary>
    /// Checks whether the BeyondFacts Lab may be opened with the given settings.
    /// </summary>
    /// <param name="settingsManager">The settings to check.</param>
    /// <param name="assistantName">The name of the lab for the log; without one, nothing gets logged.</param>
    /// <returns>True, when the preview feature is switched on and no configuration hides the lab.</returns>
    public static bool CanOpen(SettingsManager settingsManager, string assistantName = "") => settingsManager.IsAssistantVisible(
        ComponentKind.BEYOND_FACTS_LAB_ASSISTANT,
        assistantName: assistantName,
        requiredPreviewFeature: ComponentKind.BEYOND_FACTS_LAB_ASSISTANT.RequiredPreviewFeature);
}