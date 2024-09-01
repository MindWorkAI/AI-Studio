using AIStudio.Assistants.Coding;
using AIStudio.Assistants.IconFinder;
using AIStudio.Assistants.TextSummarizer;

namespace AIStudio.Settings.DataModel.PreviousModels;

/// <summary>
/// The data model for the settings file.
/// </summary>
public sealed class DataV1V3
{
    /// <summary>
    /// The version of the settings file. Allows us to upgrade the settings
    /// when a new version is available.
    /// </summary>
    public Version Version { get; init; } = Version.V3;

    /// <summary>
    /// List of configured providers.
    /// </summary>
    public List<Provider> Providers { get; init; } = [];

    /// <summary>
    /// The next provider number to use.
    /// </summary>
    public uint NextProviderNum { get; set; } = 1;

    #region App Settings
    
    /// <summary>
    /// Should we save energy? When true, we will update content streamed
    /// from the server, i.e., AI, less frequently.
    /// </summary>
    public bool IsSavingEnergy { get; set; }
    
    /// <summary>
    /// Should we enable spellchecking for all input fields?
    /// </summary>
    public bool EnableSpellchecking { get; set; }

    /// <summary>
    /// If and when we should look for updates.
    /// </summary>
    public UpdateBehavior UpdateBehavior { get; set; } = UpdateBehavior.ONCE_STARTUP;
    
    /// <summary>
    /// The navigation behavior.
    /// </summary>
    public NavBehavior NavigationBehavior { get; set; } = NavBehavior.EXPAND_ON_HOVER;
    
    #endregion

    #region Chat Settings

    /// <summary>
    /// Shortcuts to send the input to the AI.
    /// </summary>
    public SendBehavior ShortcutSendBehavior { get; set; } = SendBehavior.MODIFER_ENTER_IS_SENDING;

    /// <summary>
    /// Preselect any chat options?
    /// </summary>
    public bool PreselectChatOptions { get; set; }

    /// <summary>
    /// Should we preselect a provider for the chat?
    /// </summary>
    public string PreselectedChatProvider { get; set; } = string.Empty;

    #endregion

    #region Workspace Settings
    
    /// <summary>
    /// The chat storage behavior.
    /// </summary>
    public WorkspaceStorageBehavior WorkspaceStorageBehavior { get; set; } = WorkspaceStorageBehavior.STORE_CHATS_AUTOMATICALLY;
    
    /// <summary>
    /// The chat storage maintenance behavior.
    /// </summary>
    public WorkspaceStorageTemporaryMaintenancePolicy WorkspaceStorageTemporaryMaintenancePolicy { get; set; } = WorkspaceStorageTemporaryMaintenancePolicy.DELETE_OLDER_THAN_90_DAYS;
    
    #endregion

    #region Assiatant: Icon Finder Settings
    
    /// <summary>
    /// Do we want to preselect any icon options?
    /// </summary>
    public bool PreselectIconOptions { get; set; }
    
    /// <summary>
    /// The preselected icon source.
    /// </summary>
    public IconSources PreselectedIconSource { get; set; }

    /// <summary>
    /// The preselected icon provider.
    /// </summary>
    public string PreselectedIconProvider { get; set; } = string.Empty;
    
    #endregion

    #region Assistant: Translation Settings

    /// <summary>
    /// The live translation interval for debouncing in milliseconds.
    /// </summary>
    public int LiveTranslationDebounceIntervalMilliseconds { get; set; } = 1_500;
    
    /// <summary>
    /// Do we want to preselect any translator options?
    /// </summary>
    public bool PreselectTranslationOptions { get; set; }

    /// <summary>
    /// Preselect the live translation?
    /// </summary>
    public bool PreselectLiveTranslation { get; set; }

    /// <summary>
    /// Hide the web content reader?
    /// </summary>
    public bool HideWebContentReaderForTranslation { get; set; }

    /// <summary>
    /// Preselect the web content reader?
    /// </summary>
    public bool PreselectWebContentReaderForTranslation { get; set; }

    /// <summary>
    /// Preselect the content cleaner agent?
    /// </summary>
    public bool PreselectContentCleanerAgentForTranslation { get; set; }

    /// <summary>
    /// Preselect the target language?
    /// </summary>
    public CommonLanguages PreselectedTranslationTargetLanguage { get; set; } = CommonLanguages.EN_US;

    /// <summary>
    /// Preselect any other language?
    /// </summary>
    public string PreselectTranslationOtherLanguage { get; set; } = string.Empty;
    
    /// <summary>
    /// The preselected translator provider.
    /// </summary>
    public string PreselectedTranslationProvider { get; set; } = string.Empty;

    #endregion

    #region Assistant: Coding Settings

    /// <summary>
    /// Preselect any coding options?
    /// </summary>
    public bool PreselectCodingOptions { get; set; }

    /// <summary>
    /// Preselect the compiler messages?
    /// </summary>
    public bool PreselectCodingCompilerMessages { get; set; }

    /// <summary>
    /// Preselect the coding language for new contexts?
    /// </summary>
    public CommonCodingLanguages PreselectedCodingLanguage { get; set; }

    /// <summary>
    /// Do you want to preselect any other language?
    /// </summary>
    public string PreselectedCodingOtherLanguage { get; set; } = string.Empty;

    /// <summary>
    /// Which coding provider should be preselected?
    /// </summary>
    public string PreselectedCodingProvider { get; set; } = string.Empty;

    #endregion

    #region Assistant: Text Summarizer Settings

    /// <summary>
    /// Preselect any text summarizer options?
    /// </summary>
    public bool PreselectTextSummarizerOptions { get; set; }

    
    /// <summary>
    /// Hide the web content reader?
    /// </summary>
    public bool HideWebContentReaderForTextSummarizer { get; set; }

    /// <summary>
    /// Preselect the web content reader?
    /// </summary>
    public bool PreselectWebContentReaderForTextSummarizer { get; set; }

    /// <summary>
    /// Preselect the content cleaner agent?
    /// </summary>
    public bool PreselectContentCleanerAgentForTextSummarizer { get; set; }
    
    /// <summary>
    /// Preselect the target language?
    /// </summary>
    public CommonLanguages PreselectedTextSummarizerTargetLanguage { get; set; }

    /// <summary>
    /// Preselect any other language?
    /// </summary>
    public string PreselectedTextSummarizerOtherLanguage { get; set; } = string.Empty;

    /// <summary>
    /// Preselect the complexity?
    /// </summary>
    public Complexity PreselectedTextSummarizerComplexity { get; set; }
    
    /// <summary>
    /// Preselect any expertise in a field?
    /// </summary>
    public string PreselectedTextSummarizerExpertInField { get; set; } = string.Empty;
    
    /// <summary>
    /// Preselect a text summarizer provider?
    /// </summary>
    public string PreselectedTextSummarizerProvider { get; set; } = string.Empty;

    #endregion

    #region Agent: Text Content Cleaner Settings

    /// <summary>
    /// Preselect any text content cleaner options?
    /// </summary>
    public bool PreselectAgentTextContentCleanerOptions { get; set; }
    
    /// <summary>
    /// Preselect a text content cleaner provider?
    /// </summary>
    public string PreselectedAgentTextContentCleanerProvider { get; set; } = string.Empty;

    #endregion
}