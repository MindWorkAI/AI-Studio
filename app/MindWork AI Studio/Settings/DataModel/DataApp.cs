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
    public UpdateBehavior UpdateBehavior { get; set; } = ManagedConfiguration.Register(configSelection, n => n.UpdateBehavior, UpdateBehavior.HOURLY);
    
    /// <summary>
    /// The navigation behavior.
    /// </summary>
    public NavBehavior NavigationBehavior { get; set; } = NavBehavior.NEVER_EXPAND_USE_TOOLTIPS;

    /// <summary>
    /// The visibility setting for previews features.
    /// </summary>
    public PreviewVisibility PreviewVisibility { get; set; } = ManagedConfiguration.Register(configSelection, n => n.PreviewVisibility, PreviewVisibility.NONE);

    /// <summary>
    /// The enabled preview features.
    /// </summary>
    public HashSet<PreviewFeatures> EnabledPreviewFeatures { get; set; } = new();
    
    /// <summary>
    /// Should we preselect a provider for the entire app?
    /// </summary>
    public string PreselectedProvider { get; set; } = string.Empty;
    
    /// <summary>
    /// Should we preselect a profile for the entire app?
    /// </summary>
    public string PreselectedProfile { get; set; } = string.Empty;
    
    /// <summary>
    /// Should we preselect a chat template for the entire app?
    /// </summary>
    public string PreselectedChatTemplate { get; set; } = string.Empty;

    /// <summary>
    /// Should the user be allowed to add providers?
    /// </summary>
    public bool AllowUserToAddProvider { get; set; } = ManagedConfiguration.Register(configSelection, n => n.AllowUserToAddProvider, true);
}