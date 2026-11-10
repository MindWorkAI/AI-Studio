using System.Security.Cryptography;
using System.Text;

namespace AIStudio.Assistants.BeyondFactsLab;

/// <summary>
/// Tells whether a confirmed work state of the BeyondFacts Lab still fits what it builds on.
/// </summary>
/// <remarks>
/// The answer is computed whenever it is needed and never stored, so that it cannot drift from the work states. A
/// confirmed work state remembers the revisions of the earlier steps and a fingerprint of the form of its step;
/// comparing both with the present ones gives the answer. A changed form weighs less than a changed earlier step:
/// the person may have only corrected a typo.
/// </remarks>
public static class BflStaleness
{
    /// <summary>
    /// Separates the parts of the text a fingerprint is computed from; it cannot occur in a value.
    /// </summary>
    private const char SEPARATOR = '\0';

    /// <summary>
    /// Tells where the work state of a step stands.
    /// </summary>
    /// <param name="step">The step.</param>
    /// <param name="workStates">The work states of the preparation, by their steps; a step which is missing has none.</param>
    /// <param name="forms">The forms of the preparation, by their steps; a step which is missing has a form this version cannot open, whose changes then do not count.</param>
    /// <returns>The status of the work state.</returns>
    public static BflWorkStateStatus Of(BflStep step, IReadOnlyDictionary<BflStep, BflWorkState> workStates, IReadOnlyDictionary<BflStep, BflStepFormData> forms)
    {
        if (workStates.GetValueOrDefault(step)?.Confirmed is not { } confirmed)
            return BflWorkStateStatus.NOT_CONFIRMED;

        var upstreamRevisions = UpstreamRevisionsOf(step, workStates);
        if (upstreamRevisions.Count != confirmed.UpstreamRevisions.Count || upstreamRevisions.Any(revision => confirmed.UpstreamRevisions.GetValueOrDefault(revision.Key) != revision.Value))
            return BflWorkStateStatus.OUTDATED;

        if (forms.TryGetValue(step, out var form) && FingerprintOf(form) != confirmed.DetailsFingerprint)
            return BflWorkStateStatus.DETAILS_CHANGED;

        return BflWorkStateStatus.CONFIRMED;
    }

    /// <summary>
    /// Gets the revisions of the confirmed work states of the steps before a step, which a confirmation of that step
    /// remembers.
    /// </summary>
    /// <param name="step">The step.</param>
    /// <param name="workStates">The work states of the preparation, by their steps.</param>
    /// <returns>The revision of each earlier step with a confirmed work state.</returns>
    public static IReadOnlyDictionary<BflStep, int> UpstreamRevisionsOf(BflStep step, IReadOnlyDictionary<BflStep, BflWorkState> workStates)
    {
        var revisions = new Dictionary<BflStep, int>();
        foreach (var earlier in BflStep.All.Where(candidate => candidate < step))
        {
            if (workStates.GetValueOrDefault(earlier)?.Confirmed is { } confirmed)
                revisions[earlier] = confirmed.Revision;
        }

        return revisions;
    }

    /// <summary>
    /// Computes the fingerprint of a form, which tells whether the person changed it.
    /// </summary>
    /// <remarks>
    /// Line endings and spaces around a value do not count, since they change nothing the model reads.
    /// </remarks>
    /// <param name="form">The form.</param>
    /// <returns>The fingerprint, in hexadecimal digits.</returns>
    public static string FingerprintOf(BflStepFormData form)
    {
        var text = new StringBuilder();
        foreach (var (fieldId, value) in form.Values.OrderBy(entry => entry.Key, StringComparer.Ordinal))
        {
            var normalized = value.Trim().ReplaceLineEndings("\n");
            if (normalized.Length is 0)
                continue;

            text.Append(fieldId).Append(SEPARATOR).Append(normalized).Append(SEPARATOR);
        }

        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(text.ToString())));
    }
}