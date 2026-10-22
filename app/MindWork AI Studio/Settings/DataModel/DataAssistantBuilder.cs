using System.Linq.Expressions;

namespace AIStudio.Settings.DataModel;

/// <summary>
/// Stores the default options for the Assistant Builder.
/// </summary>
/// <param name="configSelection">The managed-configuration selector.</param>
public sealed class DataAssistantBuilder(Expression<Func<Data, DataAssistantBuilder>>? configSelection = null)
{
    /// <summary>
    /// Initializes an unmanaged Assistant Builder settings instance.
    /// </summary>
    public DataAssistantBuilder() : this(null)
    {
    }

    /// <summary>
    /// Gets or sets whether Assistant Builder defaults are preselected.
    /// </summary>
    public bool PreselectOptions { get; set; } = ManagedConfiguration.Register(configSelection, value => value.PreselectOptions, true);

    /// <summary>
    /// Gets or sets the provider used by default. An empty value uses the app default.
    /// </summary>
    public string PreselectedProvider { get; set; } = ManagedConfiguration.Register(configSelection, value => value.PreselectedProvider, string.Empty);

    /// <summary>
    /// Gets or sets the default output language for generated assistants.
    /// </summary>
    public CommonLanguages PreselectedOutputLanguage { get; set; } = ManagedConfiguration.Register(configSelection, value => value.PreselectedOutputLanguage, CommonLanguages.AS_IS);

    /// <summary>
    /// Gets or sets the default custom output language.
    /// </summary>
    public string PreselectedOtherOutputLanguage { get; set; } = ManagedConfiguration.Register(configSelection, value => value.PreselectedOtherOutputLanguage, string.Empty);
}