using System.Linq.Expressions;

using AIStudio.Tools.PluginSystem;

namespace AIStudio.Settings.DataModel;

/// <summary>
/// The settings of the assistants page: the categories the user collapsed or hid, and what that means for
/// their assistants.
/// </summary>
/// <remarks>
/// Collapsing and hiding are choices of the user alone and never managed: both only take something out of
/// sight. Your organization disables categories elsewhere, see DataApp.DisabledAssistantCategories. It may,
/// however, preset or lock the two options which decide whether AI Studio keeps offering the assistants of
/// collapsed and hidden categories, e.g., in the "Send to ..." menu.<br/><br/>
/// Ask AssistantCategoryRules instead of reading the categories: CORE counts as COMMON there, and custom
/// categories are compared without regard to case.
/// </remarks>
/// <param name="configSelection">The managed-configuration selector.</param>
public sealed class DataAssistantsPage(Expression<Func<Data, DataAssistantsPage>>? configSelection = null)
{
    /// <summary>
    /// The default constructor for the JSON deserializer.
    /// </summary>
    public DataAssistantsPage() : this(null)
    {
    }

    /// <summary>
    /// The categories of AI Studio the user collapsed on the assistants page.
    /// </summary>
    public HashSet<PluginCategory> CollapsedCategories { get; set; } = [];

    /// <summary>
    /// The custom categories the user collapsed on the assistants page, by their names.
    /// </summary>
    public HashSet<string> CollapsedCustomCategories { get; set; } = [];

    /// <summary>
    /// The categories of AI Studio the user hid on the assistants page.
    /// </summary>
    public HashSet<PluginCategory> HiddenCategories { get; set; } = [];

    /// <summary>
    /// The custom categories the user hid on the assistants page, by their names.
    /// </summary>
    public HashSet<string> HiddenCustomCategories { get; set; } = [];

    /// <summary>
    /// What happens to the assistants of collapsed categories outside of the assistants page.
    /// </summary>
    /// <remarks>
    /// Collapsing a category is about a tidy page, so AI Studio keeps offering its assistants by default.
    /// </remarks>
    public AssistantOfferBehavior CollapsedCategoryAssistants { get; set; } = ManagedConfiguration.Register(configSelection, n => n.CollapsedCategoryAssistants, AssistantOfferBehavior.KEEP_OFFERING);

    /// <summary>
    /// What happens to the assistants of hidden categories outside of the assistants page.
    /// </summary>
    /// <remarks>
    /// Hiding a category says that its assistants do not matter to the user, so AI Studio treats them as if
    /// they did not exist by default.
    /// </remarks>
    public AssistantOfferBehavior HiddenCategoryAssistants { get; set; } = ManagedConfiguration.Register(configSelection, n => n.HiddenCategoryAssistants, AssistantOfferBehavior.TREAT_AS_ABSENT);
}