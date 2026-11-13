namespace AIStudio.Assistants.BeyondFactsLab;

/// <summary>
/// The texts which the system prompt of the conversation about whether the lab fits a task is made of, as they come
/// with the app.
/// </summary>
/// <param name="Onboarding">The prompt of the conversation with its placeholders.</param>
/// <param name="Address">The instruction for the form of address.</param>
public sealed record BflOnboardingPromptParts(string Onboarding, string Address);