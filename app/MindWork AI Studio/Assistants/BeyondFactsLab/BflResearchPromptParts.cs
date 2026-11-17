namespace AIStudio.Assistants.BeyondFactsLab;

/// <summary>
/// The texts which the system prompt of a research agent of the BeyondFacts Lab is made of, as they come with the app.
/// </summary>
/// <param name="Research">The prompt every research agent starts with, with its placeholders.</param>
/// <param name="Topic">The task of the agent, see BflResearchTopic.</param>
public sealed record BflResearchPromptParts(string Research, string Topic);