using System.Globalization;

namespace AIStudio.Assistants.BeyondFactsLab;

/// <summary>
/// What a session shared with the research team of the BeyondFacts Lab is put together from.
/// </summary>
/// <param name="Forms">The forms of the preparation, by their steps.</param>
/// <param name="WorkStates">The work states of the preparation, by their steps; a step which is missing has none.</param>
/// <param name="Results">The results of the preparation, by their kinds; a kind which is missing has none.</param>
/// <param name="Conversations">The conversations of the steps as the person saw them, by their steps.</param>
/// <param name="Materials">The material of the preparation; only how much of each kind there is goes into the session.</param>
/// <param name="ResearchTopics">The topics on which the lab researched the event on the web.</param>
/// <param name="IncludePersonalDisclosures">Whether personal disclosures go into the session, e.g., how the topic touches the person.</param>
/// <param name="AppVersion">The version of AI Studio, since the prompts of the lab change with it.</param>
/// <param name="SharedAtUtc">When the person shares the session.</param>
/// <param name="Culture">The culture for dates and numbers, the one of the language of the app.</param>
public sealed record BflShareInput(IReadOnlyDictionary<BflStep, BflStepFormData> Forms, IReadOnlyDictionary<BflStep, BflWorkState> WorkStates, IReadOnlyDictionary<BflResultKind, BflResult> Results, IReadOnlyDictionary<BflStep, IReadOnlyList<BflConversationMessage>> Conversations, IReadOnlyList<BflMaterial> Materials, IReadOnlyCollection<BflResearchTopic> ResearchTopics, bool IncludePersonalDisclosures, string AppVersion, DateTimeOffset SharedAtUtc, CultureInfo Culture);