namespace AIStudio.Assistants.BeyondFactsLab;

/// <summary>
/// Decides which preparation the BeyondFacts Lab shows after its list changed.
/// </summary>
public static class BflProjectSelection
{
    /// <summary>
    /// Chooses the preparation to show: the one asked for, else the one selected last, else the one changed last.
    /// </summary>
    /// <param name="projects">The preparations, the one changed last first.</param>
    /// <param name="preferredId">The preparation asked for, e.g., one just created or the one shown so far.</param>
    /// <param name="lastSelectedId">The preparation the person selected last, from an earlier session.</param>
    /// <returns>The preparation to show, or null when there is none.</returns>
    public static BflProjectEntry? Choose(IReadOnlyList<BflProjectEntry> projects, Guid? preferredId, Guid? lastSelectedId)
    {
        foreach (var candidateId in new[] { preferredId, lastSelectedId })
        {
            if (candidateId is null)
                continue;

            var candidate = projects.FirstOrDefault(project => project.ProjectId == candidateId);
            if (candidate is not null)
                return candidate;
        }

        return projects.Count > 0 ? projects[0] : null;
    }
}