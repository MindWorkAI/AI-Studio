namespace AIStudio.Tools.Services;

/// <summary>
/// What deleting a local configuration plugin takes with it, besides the plugin directory itself.
/// </summary>
/// <remarks>
/// A configuration plugin owns everything it configured. Removing it therefore removes its providers,
/// data sources, chat templates, and profiles, and it resets the settings it had locked. Users cannot
/// see any of that on the plugins page, so we show it before they confirm the deletion.
/// </remarks>
public sealed record ConfigurationPluginDeleteSummary(
    int LlmProviders,
    int TranscriptionProviders,
    int EmbeddingProviders,
    int DataSources,
    int ChatTemplates,
    int Profiles,
    int DocumentAnalysisPolicies,
    int LockedSettings,
    int MandatoryInfos,
    int Introductions)
{
    /// <summary>
    /// An empty summary, used when the configuration plugin is not running and we cannot tell what it configured.
    /// </summary>
    public static readonly ConfigurationPluginDeleteSummary EMPTY = new(0, 0, 0, 0, 0, 0, 0, 0, 0, 0);

    /// <summary>
    /// True when the deletion affects anything beyond the plugin directory.
    /// </summary>
    public bool HasAnyConsequence =>
        this.LlmProviders > 0 ||
        this.TranscriptionProviders > 0 ||
        this.EmbeddingProviders > 0 ||
        this.DataSources > 0 ||
        this.ChatTemplates > 0 ||
        this.Profiles > 0 ||
        this.DocumentAnalysisPolicies > 0 ||
        this.LockedSettings > 0 ||
        this.MandatoryInfos > 0 ||
        this.Introductions > 0;
}