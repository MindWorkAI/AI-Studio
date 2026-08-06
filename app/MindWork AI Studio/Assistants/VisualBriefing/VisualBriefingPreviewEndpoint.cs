namespace AIStudio.Assistants.VisualBriefing;

/// <summary>
/// Serves committed briefing revisions to the live preview inside the Visual Briefing Assistant.
/// </summary>
/// <remarks>
/// The assistant shows a briefing in an iframe, and an iframe can only load a URL. The exported
/// artifact is a single self-contained HTML file, so this endpoint streams exactly that file and
/// nothing else. Two properties make it safe to expose on the local app port: the caller must
/// present a short-lived token bound to this briefing and revision, and the response repeats the
/// artifact's own Content Security Policy so the preview runs under the same restrictions as the
/// exported file.
/// </remarks>
internal static class VisualBriefingPreviewEndpoint
{
    private const string ROUTE = "/visual-briefing/preview/{briefingId:guid}/{revisionId:guid}";

    /// <summary>
    /// Maps the visual briefing preview endpoint.
    /// </summary>
    /// <param name="app">The web application.</param>
    public static void MapVisualBriefingPreview(this WebApplication app) => app.MapGet(
        ROUTE,
        async (
            Guid briefingId,
            Guid revisionId,
            string? token,
            HttpContext context,
            VisualBriefingPreviewTokenService tokenService,
            VisualBriefingStore store,
            ILoggerFactory loggerFactory,
            CancellationToken cancellationToken) =>
        {
            var logger = loggerFactory.CreateLogger(nameof(VisualBriefingPreviewEndpoint));
            if (!tokenService.Validate(token, briefingId, revisionId))
            {
                logger.LogWarning(
                    Event(VisualBriefingLogEventId.PREVIEW_REJECTED),
                    "Visual briefing preview token rejected. BriefingId={BriefingId} RevisionId={RevisionId}",
                    briefingId,
                    revisionId);

                return Results.NotFound();
            }

            // The store re-validates the stored artifact before handing out a stream, so a manually
            // modified file on disk never reaches the preview:
            var preview = await store.OpenIntegrityCheckedVersionAsync(briefingId, revisionId, cancellationToken);
            if (preview is null)
            {
                logger.LogWarning(
                    Event(VisualBriefingLogEventId.SECURITY_REJECTED),
                    "Visual briefing preview artifact rejected. BriefingId={BriefingId} RevisionId={RevisionId}",
                    briefingId,
                    revisionId);

                return Results.NotFound();
            }

            context.Response.Headers.CacheControl = "no-store";
            context.Response.Headers.XContentTypeOptions = "nosniff";
            context.Response.Headers["Referrer-Policy"] = "no-referrer";
            context.Response.Headers.ContentSecurityPolicy = VisualBriefingArtifactService.GetContentSecurityPolicy(preview.Value.Parts);

            return Results.File(preview.Value.Stream, "text/html; charset=utf-8", enableRangeProcessing: false);
        });

    /// <summary>
    /// Creates the log event ID for one visual briefing log event.
    /// </summary>
    /// <param name="eventId">The visual briefing log event.</param>
    /// <returns>The log event ID.</returns>
    private static EventId Event(VisualBriefingLogEventId eventId) => new((int)eventId, eventId.ToString());
}