namespace AIStudio.Assistants.BeyondFactsLab;

/// <summary>
/// An option of a field of the BeyondFacts Lab.
/// </summary>
/// <param name="Id">The ID of the option, which a form stores. It never changes and is unique across all fields.</param>
public sealed record BflFieldOption(string Id);