namespace AIStudio.Assistants.BeyondFactsLab;

/// <summary>
/// A section of the work state of a step in the BeyondFacts Lab.
/// </summary>
/// <param name="Key">The key of the section, which a work state stores and the model writes as [KEY]. It never changes.</param>
/// <param name="Step">The step whose work state has the section.</param>
/// <param name="Description">What the section holds, in German for the model, like the prompts.</param>
public sealed record BflWorkStateSection(string Key, BflStep Step, string Description)
{
    /// <summary>
    /// Gets whether the section holds something personal, which reaches only the chat of its own step, like a
    /// personal field of a form.
    /// </summary>
    public bool IsPersonalDisclosure { get; init; }
}