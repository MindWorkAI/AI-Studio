using System.Linq.Expressions;

using AIStudio.Provider;

namespace AIStudio.Settings.DataModel;

/// <summary>
/// Stores the options of the Model Comparison assistant, which an organization may manage except for the preset.
/// </summary>
/// <param name="configSelection">The managed-configuration selector.</param>
public sealed class DataModelComparison(Expression<Func<Data, DataModelComparison>>? configSelection = null)
{
    /// <summary>
    /// The value of a model setting which preselects no model at all.
    /// </summary>
    /// <remarks>
    /// An empty text already means the provider which the whole app preselects, as in every other assistant, so
    /// "no model" needs a value of its own. It is the ID of Provider.NONE, an empty GUID, which a configuration
    /// plugin can set as well, since the plugin reads these settings as GUIDs. ConfigurationProviderSelection
    /// offers the same value for its entry without a provider.
    /// </remarks>
    public static readonly string NO_MODEL = Provider.NONE.Id;

    /// <summary>
    /// Initializes unmanaged options of the Model Comparison assistant.
    /// </summary>
    public DataModelComparison() : this(null)
    {
    }

    /// <summary>
    /// Gets or sets whether the options below apply. Without them, new presets and comparison series start with the
    /// provider of the whole app as their first model and without a judge.
    /// </summary>
    public bool PreselectOptions { get; set; } = ManagedConfiguration.Register(configSelection, value => value.PreselectOptions, false);

    /// <summary>
    /// Gets or sets the minimum confidence which every model of a comparison needs, the judge included.
    /// </summary>
    public ConfidenceLevel MinimumProviderConfidence { get; set; } = ManagedConfiguration.Register(configSelection, value => value.MinimumProviderConfidence, ConfidenceLevel.NONE);

    /// <summary>
    /// Gets or sets the model which every new preset and every new comparison series starts with as its first model,
    /// e.g., the model the user works with every day and wants to test new models against.
    /// </summary>
    /// <remarks>
    /// NO_MODEL preselects none, and an empty text the provider which the whole app preselects.
    /// </remarks>
    public string PreselectedProvider { get; set; } = ManagedConfiguration.Register(configSelection, value => value.PreselectedProvider, NO_MODEL);

    /// <summary>
    /// Gets or sets the judge which every new preset and every new comparison series starts with. A preselected
    /// judge also switches the judge on.
    /// </summary>
    /// <remarks>
    /// NO_MODEL preselects none and leaves the judge off, and an empty text the provider which the whole app preselects.
    /// </remarks>
    public string PreselectedJudgeProvider { get; set; } = ManagedConfiguration.Register(configSelection, value => value.PreselectedJudgeProvider, NO_MODEL);

    /// <summary>
    /// Gets or sets the ID of the preset which the Answer Test opens with, or an empty text for none.
    /// </summary>
    /// <remarks>
    /// Not managed: the presets belong to the user alone, as files of the data directory, so no organization can
    /// know their IDs.
    /// </remarks>
    public string PreselectedPresetId { get; set; } = string.Empty;
}