using System.Text.Json.Serialization;

namespace AIStudio.Assistants.BeyondFactsLab;

/// <summary>
/// How the person sorted the values of the deck in the step "Values and perspectives" of the BeyondFacts Lab: a
/// category for each value, the values the deck misses, and the two or three values to look at more closely.
/// </summary>
/// <remarks>
/// The card sort belongs to the details of its step and is stored with them, see BflStepFormData.CardSort. Every part
/// is voluntary and stays changeable: a value without a category stays open, and the step can be finished without
/// any sorting. The methods keep the limits. A category or a choice whose value the deck does not know, e.g., one of
/// another version of the deck, stays stored but shows nowhere, see FocusIn.
/// </remarks>
public sealed class BflCardSort
{
    /// <summary>
    /// How many values of their own a person may add.
    /// </summary>
    public const int MAX_OWN_VALUES = 10;

    /// <summary>
    /// How many characters the name of an own value may have.
    /// </summary>
    public const int MAX_NAME_LENGTH = 60;

    /// <summary>
    /// How many characters the definition of an own value may have.
    /// </summary>
    public const int MAX_DEFINITION_LENGTH = 300;

    /// <summary>
    /// How many values a person may choose to look at more closely: two or three, after Lara Wüster's dialogue logic.
    /// </summary>
    public const int MAX_FOCUS = 3;

    /// <summary>
    /// Gets or sets the category of each value the person sorted, by the ID of the value in the deck.
    /// </summary>
    public Dictionary<string, BflValueCategory> Categories { get; set; } = new(StringComparer.Ordinal);

    /// <summary>
    /// Gets or sets the values the person added, in the order they added them.
    /// </summary>
    public List<BflOwnValue> OwnValues { get; set; } = [];

    /// <summary>
    /// Gets or sets the IDs of the values the person wants to look at more closely, of the deck or their own, in the
    /// order they chose them. The order is no ranking.
    /// </summary>
    public List<string> Focus { get; set; } = [];

    /// <summary>
    /// Gets whether the person has neither sorted a value, nor added one, nor chosen one to look at more closely.
    /// </summary>
    [JsonIgnore]
    public bool IsEmpty => this.Categories.Values.All(category => category is BflValueCategory.NONE) && this.OwnValues.Count is 0 && this.Focus.Count is 0;

    /// <summary>
    /// Gets whether the person may add another value of their own.
    /// </summary>
    [JsonIgnore]
    public bool CanAddOwnValue => this.OwnValues.Count < MAX_OWN_VALUES;

    /// <summary>
    /// Gets the category of a value of the deck.
    /// </summary>
    /// <param name="valueId">The ID of the value.</param>
    /// <returns>The category, or NONE when the person has not sorted the value.</returns>
    public BflValueCategory CategoryOf(string valueId) => this.Categories.GetValueOrDefault(valueId, BflValueCategory.NONE);

    /// <summary>
    /// Sorts a value of the deck into a category; NONE leaves it open again.
    /// </summary>
    /// <param name="valueId">The ID of the value.</param>
    /// <param name="category">The category.</param>
    public void Sort(string valueId, BflValueCategory category)
    {
        if (category is BflValueCategory.NONE)
            this.Categories.Remove(valueId);
        else
            this.Categories[valueId] = category;
    }

    /// <summary>
    /// Adds a value which the deck misses.
    /// </summary>
    /// <param name="name">The name of the value; spaces around it do not count, and a longer name is cut.</param>
    /// <param name="definition">What the value means to the person; it may be empty, and a longer one is cut.</param>
    /// <returns>The value, or null when the name is empty or the person has added as many values as they may.</returns>
    public BflOwnValue? AddOwnValue(string name, string definition)
    {
        var limitedName = Limit(name, MAX_NAME_LENGTH);
        if (limitedName.Length is 0 || !this.CanAddOwnValue)
            return null;

        var value = new BflOwnValue { Id = Guid.NewGuid().ToString("N"), Name = limitedName, Definition = Limit(definition, MAX_DEFINITION_LENGTH) };
        this.OwnValues.Add(value);
        return value;
    }

    /// <summary>
    /// Changes the name and the definition of a value of the person.
    /// </summary>
    /// <param name="valueId">The ID of the value.</param>
    /// <param name="name">The new name; spaces around it do not count, and a longer name is cut.</param>
    /// <param name="definition">The new definition; it may be empty, and a longer one is cut.</param>
    /// <returns>True when the value changed; false when the person has no such value or the name is empty.</returns>
    public bool ChangeOwnValue(string valueId, string name, string definition)
    {
        var limitedName = Limit(name, MAX_NAME_LENGTH);
        if (limitedName.Length is 0 || this.OwnValues.FirstOrDefault(candidate => string.Equals(candidate.Id, valueId, StringComparison.Ordinal)) is not { } value)
            return false;

        value.Name = limitedName;
        value.Definition = Limit(definition, MAX_DEFINITION_LENGTH);
        return true;
    }

    /// <summary>
    /// Removes a value of the person, also from the values to look at more closely.
    /// </summary>
    /// <param name="valueId">The ID of the value.</param>
    public void RemoveOwnValue(string valueId)
    {
        this.OwnValues.RemoveAll(candidate => string.Equals(candidate.Id, valueId, StringComparison.Ordinal));
        this.Focus.RemoveAll(candidate => string.Equals(candidate, valueId, StringComparison.Ordinal));
    }

    /// <summary>
    /// Chooses the values to look at more closely. The values chosen before keep their place; of the new ones, only
    /// as many are taken as fit within MAX_FOCUS.
    /// </summary>
    /// <param name="valueIds">The IDs of all values the person wants to look at more closely.</param>
    public void ChooseFocus(IReadOnlyCollection<string> valueIds)
    {
        var focus = this.Focus.Where(valueIds.Contains).Distinct(StringComparer.Ordinal).ToList();
        foreach (var valueId in valueIds)
        {
            if (focus.Count >= MAX_FOCUS)
                break;

            if (!focus.Contains(valueId, StringComparer.Ordinal))
                focus.Add(valueId);
        }

        this.Focus = focus;
    }

    /// <summary>
    /// Gets the values to look at more closely which exist, in the deck or among the values of the person, at most
    /// MAX_FOCUS of them in the order the person chose them.
    /// </summary>
    /// <param name="deck">The deck the person sorted.</param>
    /// <returns>The IDs of the values.</returns>
    public IReadOnlyList<string> FocusIn(BflValueDeck deck) => [.. this.Focus.Distinct(StringComparer.Ordinal).Where(valueId => this.NameOf(valueId, deck) is not null).Take(MAX_FOCUS)];

    /// <summary>
    /// Gets the name of a value, of the deck or of the person.
    /// </summary>
    /// <param name="valueId">The ID of the value.</param>
    /// <param name="deck">The deck the person sorted.</param>
    /// <returns>The name, or null when neither the deck nor the person has such a value.</returns>
    public string? NameOf(string valueId, BflValueDeck deck) => deck.Find(valueId)?.Name ?? this.OwnValues.FirstOrDefault(candidate => string.Equals(candidate.Id, valueId, StringComparison.Ordinal))?.Name;

    /// <summary>
    /// Trims a text and cuts it to a number of characters.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <param name="maxLength">The number of characters.</param>
    private static string Limit(string? text, int maxLength)
    {
        var trimmed = (text ?? string.Empty).Trim();
        return trimmed.Length > maxLength ? trimmed[..maxLength].TrimEnd() : trimmed;
    }
}