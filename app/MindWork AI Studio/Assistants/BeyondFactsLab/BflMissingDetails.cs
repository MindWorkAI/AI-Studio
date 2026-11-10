namespace AIStudio.Assistants.BeyondFactsLab;

/// <summary>
/// Finds the key details a preparation of the BeyondFacts Lab still lacks.
/// </summary>
/// <remarks>
/// Missing details never block anything: the person may continue, and the chat asks for them.
/// </remarks>
public static class BflMissingDetails
{
    /// <summary>
    /// Gets the key details of a step which its form leaves empty.
    /// </summary>
    /// <param name="step">The step.</param>
    /// <param name="form">The form of the step.</param>
    /// <returns>The fields which are missing, in the order the form shows them.</returns>
    public static IReadOnlyList<BflFieldDefinition> Of(BflStep step, BflStepFormData form) =>
        [.. BflFieldCatalog.ForStep(step).Where(field => field.IsKeyDetail && string.IsNullOrWhiteSpace(form.Get(field.Id)))];

    /// <summary>
    /// Gets the steps whose forms leave key details empty.
    /// </summary>
    /// <param name="forms">The forms of a preparation, by their steps; a step without a form counts as empty.</param>
    /// <returns>The steps with missing details, in their order.</returns>
    public static IReadOnlyList<BflStep> StepsOf(IReadOnlyDictionary<BflStep, BflStepFormData> forms) =>
        [.. BflStep.All.Where(step => Of(step, forms.GetValueOrDefault(step) ?? new BflStepFormData()).Count > 0)];
}