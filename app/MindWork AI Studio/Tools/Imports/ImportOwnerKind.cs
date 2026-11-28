namespace AIStudio.Tools.Imports;

/// <summary>The kinds of owners of an import, see ImportOwner.</summary>
public enum ImportOwnerKind
{
    CHAT,
    ASSISTANT,

    /// <summary>
    /// Identifies persistent media transcripts owned by a visual briefing.
    /// </summary>
    /// <remarks>
    /// A visual briefing cannot use <see cref="ASSISTANT"/>: that kind is keyed by an assistant
    /// session, which ends when the user navigates away or closes the app. A briefing is a stored
    /// document that outlives both, and its transcripts are stored next to it. The owner is
    /// therefore keyed by the briefing ID, see <see cref="ImportOwner.ForVisualBriefing"/>.
    /// This is what lets AI Studio re-associate transcripts with the right briefing after a
    /// restart, and what lets the UI show a running import on the briefing it belongs to.
    /// </remarks>
    VISUAL_BRIEFING,
}