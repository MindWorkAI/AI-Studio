namespace AIStudio.Assistants.BeyondFactsLab;

/// <summary>
/// What the person entered into the form "Your details" of one step, stored as form.json in the folder of the step.
/// </summary>
/// <remarks>
/// The values are stored under the IDs of the fields of BflFieldCatalog. A value of a field this version does not
/// know, e.g., one a newer version added, stays as it is and is written back unchanged.
/// </remarks>
public sealed class BflStepFormData
{
    /// <summary>
    /// The version of the stored form this version of AI Studio writes. Raise it with every new property.
    /// </summary>
    public const int CURRENT_SCHEMA_VERSION = 1;

    /// <summary>
    /// The character which separates the options of a field with several choices.
    /// </summary>
    private const char OPTION_SEPARATOR = ',';

    /// <summary>
    /// Gets or sets the version of the stored form.
    /// </summary>
    public int SchemaVersion { get; set; } = CURRENT_SCHEMA_VERSION;

    /// <summary>
    /// Gets or sets the values, by the IDs of their fields.
    /// </summary>
    public Dictionary<string, string> Values { get; set; } = new(StringComparer.Ordinal);

    /// <summary>
    /// Gets the value of a field, or an empty string when the field has none.
    /// </summary>
    /// <param name="fieldId">The ID of the field.</param>
    public string Get(string fieldId) => this.Values.GetValueOrDefault(fieldId, string.Empty);

    /// <summary>
    /// Sets the value of a field; an empty value removes it.
    /// </summary>
    /// <param name="fieldId">The ID of the field.</param>
    /// <param name="value">The value.</param>
    public void Set(string fieldId, string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            this.Values.Remove(fieldId);
        else
            this.Values[fieldId] = value;
    }

    /// <summary>
    /// Gets the options chosen in a field with several choices.
    /// </summary>
    /// <param name="fieldId">The ID of the field.</param>
    public IReadOnlySet<string> GetChoices(string fieldId) => this.Get(fieldId).Split(OPTION_SEPARATOR, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToHashSet(StringComparer.Ordinal);

    /// <summary>
    /// Sets the options chosen in a field with several choices.
    /// </summary>
    /// <param name="field">The field.</param>
    /// <param name="optionIds">The IDs of the chosen options.</param>
    public void SetChoices(BflFieldDefinition field, IEnumerable<string> optionIds)
    {
        var chosen = optionIds.ToHashSet(StringComparer.Ordinal);

        // Stored in the order of the options, so that the same choice always gives the same value:
        this.Set(field.Id, string.Join(OPTION_SEPARATOR, field.Options.Select(option => option.Id).Where(chosen.Contains)));
    }
}