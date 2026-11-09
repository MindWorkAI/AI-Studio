namespace AIStudio.Assistants.BeyondFactsLab;

/// <summary>
/// The texts which a system prompt of the BeyondFacts Lab is made of, as they come with the app.
/// </summary>
/// <param name="Core">The core prompt with its placeholders.</param>
/// <param name="Knowledge">The guide of the lab.</param>
/// <param name="Step">The prompt of the step.</param>
/// <param name="Address">The instruction for the form of address.</param>
public sealed record BflPromptParts(string Core, string Knowledge, string Step, string Address);