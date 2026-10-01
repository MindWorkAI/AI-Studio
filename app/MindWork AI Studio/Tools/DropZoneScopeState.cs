namespace AIStudio.Tools;

/// <summary>
/// The shared state of one drop zone scope, meaning one page, one assistant, or one dialog.
/// </summary>
/// <remarks>
/// A scope is the area whose drops end up at its default target whenever the cursor is not over a
/// more specific zone. That is what users are used to: a file dropped anywhere in the chat hangs
/// itself on the composer. This object connects the two sides -- the scope cascades it inwards, and
/// the zone which wants to be the default target claims it here.
/// </remarks>
/// <param name="scopeId">The ID of the element the scope renders.</param>
public sealed class DropZoneScopeState(string scopeId)
{
    /// <summary>
    /// The ID of the element the scope renders. The hit test reports it for every point inside the
    /// area which no more specific zone covers.
    /// </summary>
    public string ScopeId { get; } = scopeId;

    private object? defaultZone;

    /// <summary>
    /// Makes the given zone the default target of this scope, unless another zone was there first.
    /// </summary>
    /// <remarks>
    /// Zones initialize in render order, so the first one in the markup wins. Two zones asking for
    /// the same area is a mistake in the markup rather than a state worth resolving, and taking the
    /// first one is at least a rule which can be stated and logged. Asking twice is no mistake,
    /// though: a zone whose parameters are set anew has to keep the role it already holds.
    /// </remarks>
    /// <param name="zone">The zone that wants to be the default target.</param>
    /// <returns>True if the zone is the default target of this scope from now on.</returns>
    public bool TryBecomeDefaultZone(object zone)
    {
        if (this.defaultZone is not null && !ReferenceEquals(this.defaultZone, zone))
            return false;

        this.defaultZone = zone;
        return true;
    }

    /// <summary>
    /// Gives the role of the default target up again so that another zone can take it.
    /// </summary>
    /// <remarks>
    /// Every zone that took the role has to do this when it is disposed. Without it, an area would
    /// lose its default target for good as soon as the zone holding it is created anew -- which is
    /// what happens on every navigation and every time a dialog is opened again.
    /// </remarks>
    /// <param name="zone">The zone that gives the role up.</param>
    public void ReleaseDefaultZone(object zone)
    {
        if (ReferenceEquals(this.defaultZone, zone))
            this.defaultZone = null;
    }
}