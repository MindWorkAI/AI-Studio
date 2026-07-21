using System.Linq.Expressions;

namespace AIStudio.Settings.DataModel;

public sealed class DataApp(Expression<Func<Data, DataApp>>? configSelection = null)
{
    /// <summary>
    /// The default constructor for the JSON deserializer.
    /// </summary>
    public DataApp() : this(null)
    {
    }
    
    /// <summary>
    /// The language behavior.
    /// </summary>
    public LangBehavior LanguageBehavior { get; set; } = LangBehavior.AUTO;
    
    /// <summary>
    /// The language plugin ID to use.
    /// </summary>
    public Guid LanguagePluginId { get; set; } = Guid.Empty;
    
    /// <summary>
    /// The preferred theme to use.
    /// </summary>
    public Themes PreferredTheme { get; set; } = Themes.SYSTEM;
    
    /// <summary>
    /// Should we save energy? When true, we will update content streamed
    /// from the server, i.e., AI, less frequently.
    /// </summary>
    public bool IsSavingEnergy { get; set; } = ManagedConfiguration.Register(configSelection, n => n.IsSavingEnergy, false);
    
    /// <summary>
    /// Should we enable spellchecking for all input fields?
    /// </summary>
    public bool EnableSpellchecking { get; set; }

    /// <summary>
    /// If and when we should look for updates.
    /// </summary>
    public UpdateInterval UpdateInterval { get; set; } = ManagedConfiguration.Register(configSelection, n => n.UpdateInterval, UpdateInterval.HOURLY);

    /// <summary>
    /// How updates should be installed.
    /// </summary>
    public UpdateInstallation UpdateInstallation { get; set; } = ManagedConfiguration.Register(configSelection, n => n.UpdateInstallation, UpdateInstallation.MANUAL);
    
    /// <summary>
    /// The navigation behavior.
    /// </summary>
    public NavBehavior NavigationBehavior { get; set; } = NavBehavior.NEVER_EXPAND_USE_TOOLTIPS;

    /// <summary>
    /// Which page should be opened first when the app starts?
    /// </summary>
    public StartPage StartPage { get; set; } = ManagedConfiguration.Register(configSelection, n => n.StartPage, StartPage.HOME);

    /// <summary>
    /// Should the built-in introduction be visible on the home page?
    /// </summary>
    public bool ShowIntroduction { get; set; } = ManagedConfiguration.Register(configSelection, n => n.ShowIntroduction, true);

    /// <summary>
    /// Should the quick start guide be visible on the home page?
    /// </summary>
    public bool ShowQuickStartGuide { get; set; } = ManagedConfiguration.Register(configSelection, n => n.ShowQuickStartGuide, true);

    /// <summary>
    /// Should the last changelog be visible on the home page?
    /// </summary>
    public bool ShowLastChangelog { get; set; } = ManagedConfiguration.Register(configSelection, n => n.ShowLastChangelog, true);

    /// <summary>
    /// Should the vision panel be visible on the home page?
    /// </summary>
    public bool ShowVision { get; set; } = ManagedConfiguration.Register(configSelection, n => n.ShowVision, true);

    /// <summary>
    /// The visibility setting for previews features.
    /// </summary>
    public PreviewVisibility PreviewVisibility { get; set; } = ManagedConfiguration.Register(configSelection, n => n.PreviewVisibility, PreviewVisibility.NONE);

    /// <summary>
    /// The enabled preview features.
    /// </summary>
    public HashSet<PreviewFeatures> EnabledPreviewFeatures { get; set; } = ManagedConfiguration.Register(configSelection, n => n.EnabledPreviewFeatures, []);
    
    /// <summary>
    /// Should we preselect a provider for the entire app?
    /// </summary>
    public string PreselectedProvider { get; set; } = ManagedConfiguration.Register(configSelection, n => n.PreselectedProvider, string.Empty);
    
    /// <summary>
    /// Should we preselect a profile for the entire app?
    /// </summary>
    public string PreselectedProfile { get; set; } = ManagedConfiguration.Register(configSelection, n => n.PreselectedProfile, string.Empty);
    
    /// <summary>
    /// Should we preselect a chat template for the entire app?
    /// </summary>
    public string PreselectedChatTemplate { get; set; } = string.Empty;

    /// <summary>
    /// Which transcription provider should be used?
    /// </summary>
    public string UseTranscriptionProvider { get; set; } = ManagedConfiguration.Register(configSelection, n => n.UseTranscriptionProvider, string.Empty);

    /// <summary>
    /// The global keyboard shortcut for toggling voice recording.
    /// Uses Tauri's shortcut format, e.g., "CmdOrControl+1" (Cmd+1 on macOS, Ctrl+1 on Windows/Linux).
    /// Set to empty string to disable the global shortcut.
    /// </summary>
    public string ShortcutVoiceRecording { get; set; } = ManagedConfiguration.Register(configSelection, n => n.ShortcutVoiceRecording, string.Empty);

    /// <summary>
    /// The user-facing label for the voice recording shortcut, based on the user's keyboard layout.
    /// </summary>
    public string ShortcutVoiceRecordingDisplayName { get; set; } = string.Empty;

    /// <summary>
    /// The canonical voice recording shortcut value this display label belongs to.
    /// </summary>
    public string ShortcutVoiceRecordingDisplaySource { get; set; } = string.Empty;

    /// <summary>
    /// The HTTP timeout in seconds for external HTTP clients.
    /// </summary>
    public int HttpClientTimeoutSeconds { get; set; } = ManagedConfiguration.Register(configSelection, n => n.HttpClientTimeoutSeconds, ExternalHttpClientTimeout.DEFAULT_HTTP_CLIENT_TIMEOUT_SECONDS);

    /// <summary>
    /// Should external HTTP clients trust additional root certificates from a configured PEM bundle?
    /// </summary>
    public bool ExternalHttpCustomRootCertificatesEnabled { get; set; } = ManagedConfiguration.Register(configSelection, n => n.ExternalHttpCustomRootCertificatesEnabled, false);

    /// <summary>
    /// Path to a PEM bundle containing additional root certificates for external HTTP clients.
    /// </summary>
    public string ExternalHttpCustomRootCertificateBundlePath { get; set; } = ManagedConfiguration.Register(configSelection, n => n.ExternalHttpCustomRootCertificateBundlePath, string.Empty);

    /// <summary>
    /// Hostnames for which external HTTP clients may use the additional root certificates.
    /// </summary>
    public HashSet<string> ExternalHttpCustomRootCertificateAllowedHosts { get; set; } = ManagedConfiguration.Register(configSelection, n => n.ExternalHttpCustomRootCertificateAllowedHosts, []);

    /// <summary>
    /// Should the user be allowed to add providers?
    /// </summary>
    public bool AllowUserToAddProvider { get; set; } = ManagedConfiguration.Register(configSelection, n => n.AllowUserToAddProvider, true);
    
    /// <summary>
    /// Should administration settings be visible in the UI?
    /// </summary>
    public bool ShowAdminSettings { get; set; } = ManagedConfiguration.Register(configSelection, n => n.ShowAdminSettings, false);

    /// <summary>
    /// Should copied and exported AI-generated content include a disclosure?
    /// </summary>
    public bool AddAIGeneratedContentDisclosure { get; set; } = ManagedConfiguration.Register(configSelection, n => n.AddAIGeneratedContentDisclosure, true);

    /// <summary>
    /// List of assistants that should be hidden from the UI.
    /// </summary>
    public HashSet<ConfigurableAssistant> HiddenAssistants { get; set; } = ManagedConfiguration.Register(configSelection, n => n.HiddenAssistants, []);
}
