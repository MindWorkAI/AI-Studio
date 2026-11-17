using System.Globalization;

namespace AIStudio.Assistants.BeyondFactsLab;

/// <summary>
/// What the reflection folder of a preparation is put together from.
/// </summary>
/// <param name="ProjectName">The name of the preparation.</param>
/// <param name="WorkStates">The work states of the preparation, by their steps; a step which is missing has none.</param>
/// <param name="Forms">The forms of the preparation, by their steps, to tell where a work state stands.</param>
/// <param name="Materials">The material of the preparation which can be opened.</param>
/// <param name="Conversations">The conversations of the steps, by their steps, for the appendix; empty for none.</param>
/// <param name="IncludePersonalDisclosures">Whether personal disclosures go into the folder, e.g., how the topic touches the person.</param>
/// <param name="Culture">The culture for dates, the one of the language of the app.</param>
public sealed record BflReflectionFolderInput(string ProjectName, IReadOnlyDictionary<BflStep, BflWorkState> WorkStates, IReadOnlyDictionary<BflStep, BflStepFormData> Forms, IReadOnlyList<BflMaterial> Materials, IReadOnlyDictionary<BflStep, IReadOnlyList<BflConversationMessage>> Conversations, bool IncludePersonalDisclosures, CultureInfo Culture);