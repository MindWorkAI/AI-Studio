namespace AIStudio.Tools.Media;

/// <summary>Capabilities of a media-import owner kind.</summary>
public static class MediaImportOwnerKindExtensions
{
    /// <summary>
    /// Gets whether the owner stores its own source list and transcripts.
    /// </summary>
    /// <remarks>
    /// Owners that persist their own sources take the attached media over immediately and keep it
    /// next to the stored document, see <see cref="MediaImportOwnerKind.VISUAL_BRIEFING"/>. The
    /// attachment control must therefore neither wait for the transcription to finish before showing
    /// the file, nor deliver the completed transcripts back into its own list afterwards, because
    /// the owner already holds them. All other owners rely on that delivery instead.
    /// </remarks>
    /// <param name="kind">The owner kind to look up.</param>
    /// <returns><c>true</c> when the owner persists its own sources.</returns>
    public static bool PersistsOwnSources(this MediaImportOwnerKind kind) => kind is MediaImportOwnerKind.VISUAL_BRIEFING;
}