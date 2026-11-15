using System.Globalization;

using AIStudio.Components;

using Microsoft.AspNetCore.Components;

namespace AIStudio.Assistants.BeyondFactsLab;

/// <summary>
/// The card sort of the step "Values and perspectives" of the BeyondFacts Lab: a tab for each round of the deck, in
/// which the person sorts each value into a category, a tab for values of their own, and a tab for the two or three
/// values to look at more closely.
/// </summary>
/// <remarks>
/// The card sort belongs to the details of its step: the component changes the form it gets and tells its parent,
/// which stores it. Every part is voluntary. The deck comes in the language of the app and is read again when that
/// language changes.
/// </remarks>
public partial class BflValueCardSort : MSGComponentBase
{
    /// <summary>
    /// What the component reads while the person has not sorted anything yet. It is never changed: the first change
    /// gives the form a card sort of its own.
    /// </summary>
    private static readonly BflCardSort UNSORTED = new();

    [Inject]
    private ILogger<BflValueCardSort> Logger { get; init; } = null!;

    /// <summary>
    /// Gets or sets the form of the step, whose card sort the component changes.
    /// </summary>
    [Parameter, EditorRequired]
    public BflStepFormData Form { get; set; } = new();

    /// <summary>
    /// Gets or sets whether the card sort cannot be changed.
    /// </summary>
    [Parameter]
    public bool Disabled { get; set; }

    /// <summary>
    /// Gets or sets the callback raised after the card sort changed.
    /// </summary>
    [Parameter]
    public EventCallback FormChanged { get; set; }

    /// <summary>
    /// Gets or sets the CSS classes of the component.
    /// </summary>
    [Parameter]
    public string Class { get; set; } = string.Empty;

    /// <summary>
    /// The deck in the language of the app, or null until it is read or when the app comes without it.
    /// </summary>
    private BflValueDeck? deck;

    /// <summary>
    /// The culture of the language whose deck the component read last.
    /// </summary>
    private CultureInfo? deckCulture;

    /// <summary>
    /// Whether the app comes without a readable deck.
    /// </summary>
    private bool isDeckMissing;

    /// <summary>
    /// The name of the value the person is about to add.
    /// </summary>
    private string newName = string.Empty;

    /// <summary>
    /// What the value the person is about to add means to them.
    /// </summary>
    private string newDefinition = string.Empty;

    private static CultureInfo Culture => Tools.PluginSystem.I18N.I.Culture;

    /// <summary>
    /// Gets the card sort to show.
    /// </summary>
    private BflCardSort CardSort => this.Form.CardSort ?? UNSORTED;

    /// <summary>
    /// Gets the card sort to change, which the form gets with the first change.
    /// </summary>
    private BflCardSort ChangeableCardSort => this.Form.CardSort ??= new();

    /// <summary>
    /// Gets whether the person chose as many values to look at more closely as they may.
    /// </summary>
    private bool IsFocusFull => this.CardSort.Focus.Count >= BflCardSort.MAX_FOCUS;

    protected override async Task OnParametersSetAsync()
    {
        await base.OnParametersSetAsync();
        if (Equals(this.deckCulture, Culture))
            return;

        this.deckCulture = Culture;
        try
        {
            this.deck = await BflValueDeck.LoadAsync(Culture);
            this.isDeckMissing = false;
        }
        catch (Exception exception) when (exception is FileNotFoundException or InvalidDataException)
        {
            this.Logger.LogError(exception, "Could not read the value deck of the BeyondFacts Lab.");
            this.deck = null;
            this.isDeckMissing = true;
        }
    }

    /// <summary>
    /// Gets the icon of the tab of a round: a check once the person sorted every value of it.
    /// </summary>
    /// <param name="round">The values of the round.</param>
    private string? RoundIcon(IReadOnlyList<BflValueCard> round) => round.All(card => this.CardSort.CategoryOf(card.Id) is not BflValueCategory.NONE) ? AppIcons.CARD_SORT_ROUND_DONE : null;

    private async Task SortAsync(string valueId, BflValueCategory category)
    {
        if (this.CardSort.CategoryOf(valueId) == category)
            return;

        this.ChangeableCardSort.Sort(valueId, category);
        await this.FormChanged.InvokeAsync();
    }

    private async Task AddOwnValueAsync()
    {
        if (this.ChangeableCardSort.AddOwnValue(this.newName, this.newDefinition) is null)
            return;

        this.newName = string.Empty;
        this.newDefinition = string.Empty;
        await this.FormChanged.InvokeAsync();
    }

    private async Task ChangeOwnValueAsync(BflOwnValue value, string? name, string? definition)
    {
        if (this.ChangeableCardSort.ChangeOwnValue(value.Id, name ?? string.Empty, definition ?? string.Empty))
            await this.FormChanged.InvokeAsync();
    }

    private async Task RemoveOwnValueAsync(BflOwnValue value)
    {
        this.ChangeableCardSort.RemoveOwnValue(value.Id);
        await this.FormChanged.InvokeAsync();
    }

    private async Task ChooseFocusAsync(IReadOnlyCollection<string>? valueIds)
    {
        this.ChangeableCardSort.ChooseFocus(valueIds ?? []);
        await this.FormChanged.InvokeAsync();
    }

    /// <summary>
    /// Tells whether a value cannot be chosen to look at more closely, because the person chose as many as they may.
    /// </summary>
    /// <param name="valueId">The ID of the value.</param>
    private bool IsFocusBlocked(string valueId) => this.IsFocusFull && !this.CardSort.Focus.Contains(valueId, StringComparer.Ordinal);

    /// <summary>
    /// Gets the check of the name of an own value, which refuses an empty name: the value keeps its former one then.
    /// </summary>
    private Func<string, string?> NameValidation => this.ValidateName;

    private string? ValidateName(string name) => string.IsNullOrWhiteSpace(name) ? TA("Please enter a name, or remove the value.") : null;
}