namespace AIStudio.Assistants.BeyondFactsLab;

/// <summary>
/// A field of the form "Your details" of a step in the BeyondFacts Lab.
/// </summary>
/// <param name="Id">The ID of the field, which a form stores. It never changes.</param>
/// <param name="Step">The step whose form shows the field.</param>
/// <param name="Kind">How the field takes its value.</param>
public sealed record BflFieldDefinition(string Id, BflStep Step, BflFieldKind Kind)
{
    /// <summary>
    /// Gets whether the step works poorly without the field, so the lab marks it as missing while it is empty.
    /// The person may continue anyway; the chat asks for it.
    /// </summary>
    public bool IsKeyDetail { get; init; }

    /// <summary>
    /// Gets whether the field holds something personal, which exports and sharing leave out unless the person
    /// chooses otherwise.
    /// </summary>
    public bool IsPersonalDisclosure { get; init; }

    /// <summary>
    /// Gets whether the field holds public details about the event, which the research on the web may use.
    /// </summary>
    /// <remarks>
    /// Nothing else of a preparation reaches the research: neither the other fields, nor materials, nor work states.
    /// </remarks>
    public bool IsSharedWithResearch { get; init; }

    /// <summary>
    /// Gets whether the field holds names of people.
    /// </summary>
    public BflNameRole NameRole { get; init; } = BflNameRole.NONE;

    /// <summary>
    /// Gets the options of a field which offers a choice, in the order to show them.
    /// </summary>
    public IReadOnlyList<BflFieldOption> Options { get; init; } = [];
}