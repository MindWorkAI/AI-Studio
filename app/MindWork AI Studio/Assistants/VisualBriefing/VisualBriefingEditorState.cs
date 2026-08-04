using System.Diagnostics.CodeAnalysis;

using AIStudio.Assistants.SlideBuilder;
using AIStudio.Chat;
using AIStudio.Settings;

using ProviderSettings = AIStudio.Settings.Provider;

namespace AIStudio.Assistants.VisualBriefing;

/// <summary>
/// Holds the editable state of one visual briefing while the user works on it.
/// </summary>
/// <remarks>
/// This is the single source of truth for the briefing editor. It exists because the editor cannot
/// bind to <see cref="VisualBriefingLocalSettings"/> directly: that type stores the provider, model,
/// and profile as identifiers, while the UI binds whole <see cref="ProviderSettings"/> and
/// <see cref="Profile"/> objects. Keeping one draft object means saving, restoring, and change
/// detection all read the same fields instead of three hand-maintained lists.
/// </remarks>
public sealed class VisualBriefingEditorState
{
    /// <summary>Gets or sets the briefing name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Gets or sets the optional author.</summary>
    public string Author { get; set; } = string.Empty;

    /// <summary>Gets or sets the selected provider and model.</summary>
    public ProviderSettings Provider { get; set; } = ProviderSettings.NONE;

    /// <summary>Gets or sets the selected profile.</summary>
    public Profile Profile { get; set; } = Profile.NO_PROFILE;

    /// <summary>Gets or sets the current scope or change instruction.</summary>
    public string Instruction { get; set; } = string.Empty;

    /// <summary>Gets or sets the selected target language.</summary>
    public CommonLanguages TargetLanguage { get; set; } = CommonLanguages.EN_US;

    /// <summary>Gets or sets a free-form target language.</summary>
    public string CustomTargetLanguage { get; set; } = string.Empty;

    /// <summary>Gets or sets the audience profile.</summary>
    public AudienceProfile AudienceProfile { get; set; }

    /// <summary>Gets or sets the audience age group.</summary>
    public AudienceAgeGroup AudienceAgeGroup { get; set; }

    /// <summary>Gets or sets the audience organizational level.</summary>
    public AudienceOrganizationalLevel AudienceOrganizationalLevel { get; set; }

    /// <summary>Gets or sets the audience expertise.</summary>
    public AudienceExpertise AudienceExpertise { get; set; }

    /// <summary>Gets or sets whether visible source references are requested.</summary>
    public bool ShowSourceReferences { get; set; } = true;

    /// <summary>Gets or sets whether large visual assets are optimized.</summary>
    public bool OptimizeImages { get; set; } = true;

    /// <summary>Gets or sets the selected protection level.</summary>
    public VisualBriefingProtectionLevel ProtectionLevel { get; set; } = VisualBriefingProtectionLevel.INTERNAL;

    /// <summary>Gets or sets the free-form protection level.</summary>
    public string CustomProtectionLevel { get; set; } = string.Empty;

    /// <summary>Gets or sets the source-material attachments.</summary>
    public HashSet<FileAttachment> SourceMaterial { get; set; } = [];

    /// <summary>Gets or sets the visual-asset attachments.</summary>
    public HashSet<FileAttachment> VisualAssets { get; set; } = [];

    /// <summary>
    /// Creates the editor state for a stored briefing.
    /// </summary>
    /// <param name="briefing">The manifest to read.</param>
    /// <param name="settingsManager">The settings used to resolve the stored provider and profile.</param>
    /// <returns>The editor state for the briefing.</returns>
    [SuppressMessage("Usage", "MWAIS0001:Direct access to `Providers` is not allowed", Justification = "A stored briefing references one specific provider and model by id, so it must be looked up directly instead of using the preselection APIs.")]
    public static VisualBriefingEditorState FromManifest(VisualBriefingManifest briefing, SettingsManager settingsManager) => new()
    {
        Name = briefing.Name,
        Author = briefing.Author,
        Instruction = briefing.Settings.Instruction,
        TargetLanguage = briefing.Settings.TargetLanguage,
        CustomTargetLanguage = briefing.Settings.CustomTargetLanguage,
        AudienceProfile = briefing.Settings.AudienceProfile,
        AudienceAgeGroup = briefing.Settings.AudienceAgeGroup,
        AudienceOrganizationalLevel = briefing.Settings.AudienceOrganizationalLevel,
        AudienceExpertise = briefing.Settings.AudienceExpertise,
        ShowSourceReferences = briefing.Settings.ShowSourceReferences,
        OptimizeImages = briefing.Settings.OptimizeImages,
        ProtectionLevel = briefing.Settings.ProtectionLevel,
        CustomProtectionLevel = briefing.Settings.CustomProtectionLevel,

        Provider = settingsManager.ConfigurationData.Providers.FirstOrDefault(candidate => candidate.Id == briefing.Settings.ProviderId && candidate.Model.Id == briefing.Settings.ModelId) ?? ProviderSettings.NONE,
        Profile = settingsManager.ConfigurationData.Profiles.FirstOrDefault(candidate => candidate.Id == briefing.Settings.ProfileId) ?? Profile.NO_PROFILE,

        SourceMaterial =
        [
            .. briefing.Sources
                .Where(source => source.Kind is VisualBriefingSourceKind.SOURCE_MATERIAL)
                .Select(source => FileAttachment.FromPath(source.Path))
        ],

        VisualAssets =
        [
            .. briefing.Sources
                .Where(source => source.Kind is VisualBriefingSourceKind.VISUAL_ASSET)
                .Select(source => FileAttachment.FromPath(source.Path))
        ],
    };

    /// <summary>
    /// Creates the persisted settings for this editor state.
    /// </summary>
    /// <returns>The settings to store.</returns>
    public VisualBriefingLocalSettings ToSettings() => new()
    {
        ProviderId = this.Provider.Id,
        ModelId = this.Provider.Model.Id,
        ProfileId = this.Profile.Id,
        TargetLanguage = this.TargetLanguage,
        CustomTargetLanguage = this.CustomTargetLanguage,
        AudienceProfile = this.AudienceProfile,
        AudienceAgeGroup = this.AudienceAgeGroup,
        AudienceOrganizationalLevel = this.AudienceOrganizationalLevel,
        AudienceExpertise = this.AudienceExpertise,
        ShowSourceReferences = this.ShowSourceReferences,
        OptimizeImages = this.OptimizeImages,
        Instruction = this.Instruction,
        ProtectionLevel = this.ProtectionLevel,
        CustomProtectionLevel = this.CustomProtectionLevel,
    };

    /// <summary>
    /// Creates the persisted source list for this editor state.
    /// </summary>
    /// <remarks>
    /// Source material is listed before visual assets on purpose: the store discards duplicates by
    /// path and keeps the first occurrence, so this order decides which kind wins when the same file
    /// appears in both lists. Within each kind the paths are ordered so that the same editor state
    /// always produces the same sequence, which is what makes change detection reliable.
    /// </remarks>
    /// <returns>The sources to store, in a stable order.</returns>
    public IEnumerable<(string Path, VisualBriefingSourceKind Kind)> ToSources() =>
        OrderedSources(this.SourceMaterial, VisualBriefingSourceKind.SOURCE_MATERIAL)
            .Concat(OrderedSources(this.VisualAssets, VisualBriefingSourceKind.VISUAL_ASSET));

    /// <summary>
    /// Orders one attachment set into stable source entries of a single kind.
    /// </summary>
    /// <param name="attachments">The attachments to convert.</param>
    /// <param name="kind">The kind to assign.</param>
    /// <returns>The ordered source entries.</returns>
    private static IEnumerable<(string Path, VisualBriefingSourceKind Kind)> OrderedSources(IEnumerable<FileAttachment> attachments, VisualBriefingSourceKind kind) => attachments
        .Select(attachment => attachment.FilePath)
        .Order(StringComparer.Ordinal)
        .Select(path => (path, kind));
}