using AIStudio.Settings;
using AIStudio.Tools.PluginSystem;
using AIStudio.Tools.PluginSystem.Assistants;

using Microsoft.AspNetCore.Components;

namespace AIStudio.Components;

/// <summary>
/// Renders one category of assistants together with its heading.
/// </summary>
/// <remarks>
/// The heading is derived from the assistant blocks inside this category: it is rendered only when
/// at least one of them is visible. Thus, hiding assistants by configuration can never leave an
/// empty category heading behind. The built-in assistants of the category come as child content,
/// followed by the assistant plugins which name this category.<br/><br/>
/// A category the user hid renders nothing at all. One the user collapsed keeps rendering its blocks
/// and only hides them, so that its heading stays. Each block reports to the registry of the page
/// after rendering, so that the page knows which categories it shows.
/// </remarks>
public partial class AssistantCategoryBlock : ComponentBase, IDisposable
{
    private readonly HashSet<IAssistantCategoryMember> members = [];

    [Inject]
    private SettingsManager SettingsManager { get; init; } = null!;

    [Inject]
    private ISnackbar Snackbar { get; init; } = null!;

    /// <summary>
    /// The registry of the assistants page, which learns which categories the page shows.
    /// </summary>
    [CascadingParameter]
    public AssistantCategoryRegistry? Registry { get; set; }

    /// <summary>
    /// The category of this block. Its name is the heading.
    /// </summary>
    [Parameter, EditorRequired]
    public AssistantCategoryKey Category { get; set; } = null!;

    /// <summary>
    /// The assistant plugins which name this category, in the order to show them.
    /// </summary>
    [Parameter]
    public IReadOnlyList<PluginAssistants> Plugins { get; set; } = [];

    /// <summary>
    /// The CSS classes used for the heading.
    /// </summary>
    [Parameter]
    public string HeaderClass { get; set; } = "mb-2 mr-3 mt-6";

    [Parameter]
    public RenderFragment? ChildContent { get; set; }

    /// <summary>
    /// Adds an assistant block to this category.
    /// </summary>
    /// <remarks>
    /// Assistant blocks call this while they initialize, i.e. after this category was rendered for
    /// the first time. Hence, we have to render again to show the heading.
    /// </remarks>
    /// <param name="member">The assistant block which belongs to this category.</param>
    internal void RegisterAssistant(IAssistantCategoryMember member)
    {
        if (this.members.Add(member))
            this.StateHasChanged();
    }

    /// <summary>
    /// Removes an assistant block from this category.
    /// </summary>
    /// <param name="member">The assistant block which no longer belongs to this category.</param>
    internal void UnregisterAssistant(IAssistantCategoryMember member) => this.members.Remove(member);

    /// <summary>
    /// Gets whether at least one assistant of this category is visible right now.
    /// </summary>
    /// <remarks>
    /// We evaluate this live instead of caching it. That way, changes to the configuration take
    /// effect as soon as the assistants page renders again.
    /// </remarks>
    private bool HasVisibleAssistant => this.members.Any(member => member.IsVisible);

    /// <summary>
    /// Gets whether this category shows at least one assistant right now, collapsed or not.
    /// </summary>
    internal bool IsShowingAssistants => !this.IsHidden && this.HasVisibleAssistant;

    /// <summary>
    /// Gets whether the user collapsed this category.
    /// </summary>
    private bool IsCollapsed => this.SettingsManager.ConfigurationData.AssistantsPage.IsCategoryCollapsed(this.Category);

    /// <summary>
    /// Gets whether the user hid this category.
    /// </summary>
    private bool IsHidden => this.SettingsManager.ConfigurationData.AssistantsPage.IsCategoryHidden(this.Category);

    #region Overrides of ComponentBase

    /// <inheritdoc />
    protected override void OnInitialized()
    {
        this.Registry?.Register(this);
        base.OnInitialized();
    }

    /// <inheritdoc />
    protected override void OnAfterRender(bool firstRender)
    {
        this.Registry?.ReportChange();
        base.OnAfterRender(firstRender);
    }

    #endregion

    /// <inheritdoc />
    public void Dispose() => this.Registry?.Unregister(this);

    /// <summary>
    /// Gets the CSS classes used for the assistant stack.
    /// </summary>
    /// <remarks>
    /// The stack must be rendered even when no assistant is visible, because the assistant blocks
    /// register themselves while rendering. Without any visible assistant, we drop the margin so
    /// that a hidden category leaves no gap behind. A collapsed category keeps rendering its blocks
    /// for the same reason and only hides them, so that its heading stays.
    /// </remarks>
    private string StackClass
    {
        get
        {
            if (this.IsCollapsed)
                return "d-none";

            return this.HasVisibleAssistant ? "mb-3" : string.Empty;
        }
    }

    /// <summary>
    /// Collapses this category or expands it again, and remembers that.
    /// </summary>
    /// <remarks>
    /// The user clicks once, so storing the settings right away costs nothing noticeable.
    /// </remarks>
    private async Task ToggleCollapsedAsync()
    {
        this.SettingsManager.ConfigurationData.AssistantsPage.SetCategoryCollapsed(this.Category, !this.IsCollapsed);
        await this.SettingsManager.StoreSettings();
    }

    /// <summary>
    /// Hides this category, remembers that, and tells the user how to get it back.
    /// </summary>
    /// <remarks>
    /// The whole app learns about it, since a hidden category may also take its assistants out of the
    /// "Send to ..." menu.
    /// </remarks>
    private async Task HideAsync()
    {
        var configuration = this.SettingsManager.ConfigurationData;
        configuration.AssistantsPage.SetCategoryHidden(this.Category, true);
        await this.SettingsManager.StoreSettings();

        var categories = AssistantPageSections.SelectForSettings(this.Registry?.ShownCategories ?? [], configuration.App, configuration.AssistantsPage);
        new DataAssistantCategoryHiddenMessage(categories).Show(this.Snackbar);
        await MessageBus.INSTANCE.SendMessage<bool>(this, Event.CONFIGURATION_CHANGED);
    }
}