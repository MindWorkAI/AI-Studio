using Microsoft.AspNetCore.Components;

namespace AIStudio.Components;

/// <summary>
/// A drop zone which reports the paths of whatever was dropped on it, and nothing else.
/// </summary>
/// <remarks>
/// Dropping is a native matter in AI Studio: the Tauri runtime reports real paths, which is why
/// this zone can hand out folders just as well as files. What those paths mean is the consumer's
/// business — this component reads no content and does not care whether a path leads to a file or
/// to a folder.
/// </remarks>
public partial class PathDropZone : MSGComponentBase
{
    /// <summary>
    /// The content shown inside the zone.
    /// </summary>
    [Parameter]
    public RenderFragment? ChildContent { get; set; }

    /// <summary>
    /// Reports the dropped paths, in the order the runtime delivered them.
    /// </summary>
    [Parameter]
    public EventCallback<List<string>> OnPathsDropped { get; set; }

    /// <summary>
    /// Makes this zone the default target of its area, meaning of its page, assistant, or dialog.
    /// </summary>
    /// <remarks>
    /// A drop aimed at this zone arrives here in any case. What this flag decides is the fate of the
    /// drops aimed anywhere else in the surrounding area which hit no zone of their own: with the
    /// flag, they arrive here as well. Only one zone per area can hold that role, and if several ask
    /// for it, the first one in the markup gets it.
    /// </remarks>
    [Parameter]
    public bool CatchAllDocuments { get; set; }

    /// <summary>
    /// When true, the zone ignores drops and is not highlighted.
    /// </summary>
    /// <remarks>
    /// It keeps its ID in the DOM nevertheless and therefore swallows the drops aimed at it. That is
    /// what the pointer says: it rests on a switched-off field, so nothing happens. Letting the drop
    /// fall through to the area behind it would deliver the files somewhere else entirely.
    /// </remarks>
    [Parameter]
    public bool Disabled { get; set; }

    /// <summary>
    /// The area this zone lives in, if it lives in one at all.
    /// </summary>
    [CascadingParameter]
    private DropZoneScopeState? Scope { get; set; }

    [Inject]
    private ILogger<PathDropZone> Logger { get; init; } = null!;

    private const string DEFAULT_DRAG_CLASS = "relative rounded-lg border-2 border-dashed pa-3 mb-3 mud-width-full";

    private readonly string dropZoneId = $"path-drop-zone-{Guid.NewGuid():N}";

    private string dragClass = DEFAULT_DRAG_CLASS;
    private bool isDefaultZone;
    private bool isHighlighted;

    #region Overrides of MSGComponentBase

    protected override async Task OnInitializedAsync()
    {
        this.ApplyFilters([], [ Event.HIGHLIGHT_DROP_ZONE, Event.PATHS_DROPPED ]);
        this.ClaimDefaultZoneRole();

        await base.OnInitializedAsync();
    }

    /// <summary>
    /// Hands the role of the default target back to the area.
    /// </summary>
    protected override void DisposeResources()
    {
        if (this.isDefaultZone)
            this.Scope?.ReleaseDefaultZone(this);

        base.DisposeResources();
    }

    protected override async Task ProcessIncomingMessage<T>(ComponentBase? sendingComponent, Event triggeredEvent, T? data) where T : default
    {
        switch (triggeredEvent)
        {
            case Event.HIGHLIGHT_DROP_ZONE when data is DropZoneHighlight highlight:
                this.ApplyHighlight(this.IsThisZone(highlight.ZoneId));
                break;

            case Event.PATHS_DROPPED when data is DroppedPaths dropped:
                // Whoever the drop was meant for, the drag is over and no zone stays highlighted:
                this.ApplyHighlight(false);

                if (!this.IsThisZone(dropped.ZoneId))
                    return;

                if (this.Disabled)
                {
                    this.Logger.LogDebug("The path drop zone '{ZoneId}' is disabled and swallowed {Count} dropped path(s).", this.dropZoneId, dropped.Paths.Count);
                    return;
                }

                this.Logger.LogDebug("The path drop zone '{ZoneId}' caught {Count} path(s).", this.dropZoneId, dropped.Paths.Count);
                await this.OnPathsDropped.InvokeAsync(dropped.Paths);
                break;
        }
    }

    #endregion

    /// <summary>
    /// Asks the area for the role of its default target, if this zone wants it.
    /// </summary>
    private void ClaimDefaultZoneRole()
    {
        if (!this.CatchAllDocuments || this.Scope is null)
            return;

        this.isDefaultZone = this.Scope.TryBecomeDefaultZone(this);
        if (!this.isDefaultZone)
            this.Logger.LogDebug("The path drop zone '{ZoneId}' asked to be the default target of its area, which another zone already is. It now takes only the drops aimed at itself.", this.dropZoneId);
    }

    /// <summary>
    /// Decides whether the named zone is this one.
    /// </summary>
    /// <remarks>
    /// The area counts as this zone as long as this zone is its default target. That is the whole
    /// mechanism behind dropping anywhere in a page and still landing here.
    /// </remarks>
    /// <param name="zoneId">The ID the hit test reported, or null when it hit nothing.</param>
    private bool IsThisZone(string? zoneId) => zoneId is not null && (zoneId == this.dropZoneId || (this.isDefaultZone && zoneId == this.Scope?.ScopeId));

    /// <summary>
    /// Highlights the zone, or takes the highlight away.
    /// </summary>
    /// <remarks>
    /// The comparison is not for tidiness: a throttled drag-over event arrives about ten times per
    /// second, and without it every one of them would render every zone on the page anew.
    /// </remarks>
    private void ApplyHighlight(bool shouldBeHighlighted)
    {
        var highlighted = shouldBeHighlighted && !this.Disabled;
        if (highlighted == this.isHighlighted)
            return;

        this.isHighlighted = highlighted;
        this.dragClass = highlighted ? $"{DEFAULT_DRAG_CLASS} mud-border-primary border-2" : DEFAULT_DRAG_CLASS;
        this.StateHasChanged();
    }
}