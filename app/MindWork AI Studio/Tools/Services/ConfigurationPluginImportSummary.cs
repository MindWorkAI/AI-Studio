namespace AIStudio.Tools.Services;

/// <summary>
/// What a configuration plugin would set up, read from the archive before anything is installed.
/// </summary>
/// <remarks>
/// A configuration takes effect the moment it is installed, and it has no on/off switch. The import
/// dialog is therefore the only place where users can see what they are about to accept, which is
/// why this carries the destinations of providers and data sources and not just their number.
/// </remarks>
/// <param name="Destinations">The providers and data sources, together with where they send data to.</param>
/// <param name="ChatTemplates">How many chat templates the configuration adds.</param>
/// <param name="Profiles">How many profiles the configuration adds.</param>
/// <param name="DocumentAnalysisPolicies">How many document analysis policies the configuration adds.</param>
/// <param name="DeclaredSettings">How many settings the configuration takes over.</param>
/// <param name="MandatoryInfos">How many mandatory information texts users must accept.</param>
/// <param name="Introductions">How many introductions the configuration adds to the welcome page.</param>
public sealed record ConfigurationPluginImportSummary(
    IReadOnlyList<ConfigurationPluginDestination> Destinations,
    int ChatTemplates,
    int Profiles,
    int DocumentAnalysisPolicies,
    int DeclaredSettings,
    int MandatoryInfos,
    int Introductions)
{
    /// <summary>
    /// True when the configuration sets up anything at all.
    /// </summary>
    public bool HasAnyContent =>
        this.Destinations.Count > 0 ||
        this.ChatTemplates > 0 ||
        this.Profiles > 0 ||
        this.DocumentAnalysisPolicies > 0 ||
        this.DeclaredSettings > 0 ||
        this.MandatoryInfos > 0 ||
        this.Introductions > 0;
}