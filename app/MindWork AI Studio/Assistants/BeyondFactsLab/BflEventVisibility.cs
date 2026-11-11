namespace AIStudio.Assistants.BeyondFactsLab;

/// <summary>
/// Whether the event of a preparation is public, which decides whether the lab may research it on the web.
/// </summary>
/// <remarks>
/// <para>
/// Only the person decides, in the field BflFieldCatalog.EVENT_VISIBILITY of the event details; the work state of the
/// step may suggest a value, which the person takes over or dismisses. The lab never guesses it: a model cannot know
/// whether, e.g., a defense is open to the public, and a wrong guess would send unpublished details to a search.
/// </para>
/// <para>
/// An event which is not public is never researched, because its details are not public either. While the field is
/// empty, the lab researches as before: the form tells the person which details the research receives.
/// </para>
/// </remarks>
public static class BflEventVisibility
{
    private static string TB(string fallbackEN) => Tools.PluginSystem.I18N.I.T(fallbackEN, typeof(BflEventVisibility).Namespace, nameof(BflEventVisibility));

    /// <summary>
    /// Gets what the lab tells the person when it does not research because the event is not public.
    /// </summary>
    public static string NotPublicMessage => TB("The event is not public. Therefore, the lab does not research it on the web.");

    /// <summary>
    /// Tells whether the person said that the event is not public.
    /// </summary>
    /// <param name="forms">The forms of the preparation, by their steps; a missing form counts as empty.</param>
    /// <returns>True when the research must not search the web for the event.</returns>
    public static bool IsNotPublic(IReadOnlyDictionary<BflStep, BflStepFormData> forms) => forms.TryGetValue(BflStep.FRAMEWORK, out var form) && IsNotPublic(form);

    /// <summary>
    /// Tells whether the person said in the event details that the event is not public.
    /// </summary>
    /// <param name="eventDetails">The form of the step "Event details".</param>
    /// <returns>True when the research must not search the web for the event.</returns>
    public static bool IsNotPublic(BflStepFormData eventDetails) => string.Equals(eventDetails.Get(BflFieldCatalog.EVENT_VISIBILITY), BflFieldCatalog.EVENT_NOT_PUBLIC, StringComparison.Ordinal);

    /// <summary>
    /// Tells whether the work state of the event details suggests whether the event is public, while the person has not
    /// decided yet.
    /// </summary>
    /// <remarks>
    /// The research must not start on its own meanwhile: the person may confirm the work state before they look at the
    /// suggestion, which may say that the event is not public. A suggestion this version does not show, such as an
    /// option a newer version added, keeps nothing waiting.
    /// </remarks>
    /// <param name="workStates">The work states of the preparation, by their steps.</param>
    /// <param name="forms">The forms of the preparation, by their steps; a missing form counts as empty.</param>
    /// <returns>True while the suggestion waits for the person.</returns>
    public static bool IsSuggestionPending(IReadOnlyDictionary<BflStep, BflWorkState> workStates, IReadOnlyDictionary<BflStep, BflStepFormData> forms)
    {
        if (workStates.GetValueOrDefault(BflStep.FRAMEWORK)?.FieldSuggestions.GetValueOrDefault(BflFieldCatalog.EVENT_VISIBILITY) is not { } suggestion)
            return false;

        var form = forms.GetValueOrDefault(BflStep.FRAMEWORK) ?? new BflStepFormData();
        return string.IsNullOrWhiteSpace(form.Get(BflFieldCatalog.EVENT_VISIBILITY)) && suggestion is BflFieldCatalog.EVENT_PUBLIC or BflFieldCatalog.EVENT_NOT_PUBLIC;
    }
}