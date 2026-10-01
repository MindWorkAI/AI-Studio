using Microsoft.AspNetCore.Components;

namespace AIStudio.Components;

/// <summary>
/// A drop zone which reports the paths of whatever was dropped on it, and nothing else.
/// </summary>
/// <remarks>
/// <para>
/// Dropping is a native matter in AI Studio: the Tauri runtime reports real paths, which is why
/// this zone can hand out folders just as well as files. What those paths mean is the consumer's
/// business — this component reads no content and does not care whether a path leads to a file or
/// to a folder.
/// </para>
/// <para>
/// One component serves both kinds of drop target, because both are the same thing to the hit test:
/// an element with an ID. A zone is a place one aims at, and it draws a frame around whatever it is
/// given. An area is a page, an assistant, or a dialog: it draws nothing, it marks the space in
/// which a drop counts at all, and the drops which hit none of its zones go to its default target.
/// Set IsArea for the second kind.
/// </para>
/// </remarks>
public partial class PathDropZone : MSGComponentBase
{
    /// <summary>
    /// The content shown inside the zone.
    /// </summary>
    /// <remarks>
    /// Its argument tells the content whether this zone is the one under the cursor right now, so it
    /// can show where the drop would land. Everybody who does not care about that ignores it.
    /// </remarks>
    [Parameter]
    public RenderFragment<bool>? ChildContent { get; set; }

    /// <summary>
    /// Reports the dropped paths, in the order the runtime delivered them.
    /// </summary>
    /// <remarks>
    /// An area without this callback is a marker and nothing more: it takes no drops itself, it only
    /// gives the drops aimed between its zones a place to be counted, from where the default target
    /// of the area picks them up.
    /// </remarks>
    [Parameter]
    public EventCallback<List<string>> OnPathsDropped { get; set; }

    /// <summary>
    /// Makes this zone the default target of its area, meaning of its page, assistant, or dialog.
    /// </summary>
    /// <remarks>
    /// A drop aimed at this zone arrives here in any case. What this flag decides is the fate of the
    /// drops aimed anywhere else in the surrounding area which hit no zone of their own: with the
    /// flag, they arrive here as well. Only one zone per area can hold that role, and if several ask
    /// for it, the first one in the markup gets it. An area ignores the flag: an area which takes
    /// drops at all is its own default target, see IsArea.
    /// </remarks>
    [Parameter]
    public bool CatchAllDocuments { get; set; }

    /// <summary>
    /// Decides, at the moment a drop arrives, whether this zone may take it.
    /// </summary>
    /// <remarks>
    /// A disabled zone keeps its ID in the DOM and therefore swallows the drops aimed at it. That is
    /// what the pointer says: it rests on a switched-off field, so nothing happens. Letting the drop
    /// fall through to the area behind it would deliver the files somewhere else entirely. This is
    /// asked rather than passed as a value because the answer often depends on work in flight, and a
    /// value would be as old as the last render of the consumer.
    /// </remarks>
    [Parameter]
    public Func<bool> Disabled { get; set; } = () => false;

    /// <summary>
    /// Makes this element an area instead of a zone: it marks a page, an assistant, or a dialog, and
    /// it draws no frame of its own.
    /// </summary>
    /// <remarks>
    /// This is how the habitual behaviour survives the move to hit testing: a file dropped anywhere
    /// in the chat, in an assistant, or in a dialog still arrives where it used to, while a file
    /// dropped on a specific zone now arrives exactly there. No code decides between the two — the
    /// browser does, because a zone lies deeper in the DOM than the area around it, and the hit test
    /// resolves from the inside out. An area replaces the element it stands in for rather than
    /// adding one, so it takes over its class and its style.
    /// </remarks>
    [Parameter]
    public bool IsArea { get; set; }

    /// <summary>
    /// Leaves out the frame this zone would otherwise draw around its content.
    /// </summary>
    /// <remarks>
    /// For zones whose content is the marker itself, such as a toolbar which shows a drop field
    /// while a file hovers over it. An area never has a frame, so it does not need this flag.
    /// </remarks>
    [Parameter]
    public bool Frameless { get; set; }

    /// <summary>
    /// The first part of the ID this element reports to the hit test, followed by a unique suffix.
    /// </summary>
    /// <remarks>
    /// It names the kind of zone in the log, next to the IDs the arbiter lists when a drop reached
    /// nobody. Which is the whole reason it is a parameter: an ID of its own tells one from another,
    /// but only a name tells what one is looking at.
    /// </remarks>
    [Parameter]
    public string IdPrefix { get; set; } = "path-drop-zone";

    /// <summary>
    /// The CSS classes of the element this component renders: of the frame for a zone which has one,
    /// and of the element itself for an area and for a frameless zone.
    /// </summary>
    /// <remarks>
    /// A frame keeps its border and its width in any case; the classes given here replace the
    /// padding and the margin it would use otherwise.
    /// </remarks>
    [Parameter]
    public string Class { get; set; } = string.Empty;

    /// <summary>
    /// The classes a frame takes on in addition while it is the zone under the cursor.
    /// </summary>
    /// <remarks>
    /// Only a frame is highlighted this way. Without one there is nothing to draw on, and the
    /// content says for itself what it looks like when it is the target, see ChildContent.
    /// </remarks>
    [Parameter]
    public string HighlightClass { get; set; } = "mud-border-primary border-2";

    /// <summary>
    /// The inline style of the element this component renders.
    /// </summary>
    [Parameter]
    public string Style { get; set; } = string.Empty;

    /// <summary>
    /// The area this zone lives in, if it lives in one at all.
    /// </summary>
    [CascadingParameter]
    private DropZoneScopeState? Scope { get; set; }

    [Inject]
    private ILogger<PathDropZone> Logger { get; init; } = null!;

    private const string FRAME_CLASSES = "relative rounded-lg border-2 border-dashed mud-width-full";
    private const string DEFAULT_SPACING_CLASSES = "pa-3 mb-3";

    private DropZoneScopeState? areaState;
    private string zoneId = string.Empty;
    private bool isDefaultZone;
    private bool isHighlighted;
    private bool hasReportedDefaultZoneProblem;

    private bool IsFramed => !this.IsArea && !this.Frameless;

    private string FrameClass => this.isHighlighted
        ? $"{FRAME_CLASSES} {this.SpacingClasses} {this.HighlightClass}"
        : $"{FRAME_CLASSES} {this.SpacingClasses}";

    private string SpacingClasses => string.IsNullOrWhiteSpace(this.Class) ? DEFAULT_SPACING_CLASSES : this.Class;

    /// <summary>
    /// The area this zone reports to, which for an area is itself.
    /// </summary>
    private DropZoneScopeState? EffectiveScope => this.areaState ?? this.Scope;

    /// <summary>
    /// Whether this element wants to be the default target of its area.
    /// </summary>
    /// <remarks>
    /// An area which takes drops is that target by definition, because its own element is the area
    /// and a drop next to its zones has nothing else to hit. It claims the role nevertheless, which
    /// is what keeps a zone inside it from taking it and delivering the same drop a second time.
    /// </remarks>
    private bool WantsDefaultZoneRole => this.IsArea ? this.OnPathsDropped.HasDelegate : this.CatchAllDocuments;

    /// <summary>
    /// Whether a drop can arrive here at all.
    /// </summary>
    /// <remarks>
    /// An area which delivers to nobody is only a mark on the page, and it must not listen: the
    /// highlight would render a whole page or assistant anew several times per drag, for a highlight
    /// nobody asked to see.
    /// </remarks>
    private bool CanBeTarget => !this.IsArea || this.OnPathsDropped.HasDelegate;

    #region Overrides of MSGComponentBase

    protected override async Task OnInitializedAsync()
    {
        //
        // The ID is built once and never again: the hit test names this element by it, so it has to
        // outlive every render. The prefix is a parameter, and parameters are set before this point.
        //
        this.zoneId = $"{this.IdPrefix}-{Guid.NewGuid():N}";
        if (this.IsArea)
            this.areaState = new DropZoneScopeState(this.zoneId);

        if (this.CanBeTarget)
            this.ApplyFilters([], [ Event.HIGHLIGHT_DROP_ZONE, Event.PATHS_DROPPED ]);
        else
            this.ApplyFilters([], []);

        await base.OnInitializedAsync();
    }

    protected override void OnParametersSet()
    {
        this.UpdateDefaultZoneRole();
        base.OnParametersSet();
    }

    /// <summary>
    /// Hands the role of the default target back to the area.
    /// </summary>
    protected override void DisposeResources()
    {
        if (this.isDefaultZone)
            this.EffectiveScope?.ReleaseDefaultZone(this);

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

                if (this.Disabled())
                {
                    this.Logger.LogDebug("The drop zone '{ZoneId}' cannot take drops right now and swallowed {Count} dropped path(s).", this.zoneId, dropped.Paths.Count);
                    return;
                }

                this.Logger.LogDebug("The drop zone '{ZoneId}' caught {Count} path(s).", this.zoneId, dropped.Paths.Count);
                await this.OnPathsDropped.InvokeAsync(dropped.Paths);
                break;
        }
    }

    #endregion

    /// <summary>
    /// Keeps the role of the default target in step with the CatchAllDocuments parameter.
    /// </summary>
    /// <remarks>
    /// The flag is a parameter, so it can change while this zone lives. A zone inside a collapsed
    /// panel is the case this exists for: MudBlazor leaves the content of a collapsed panel in the
    /// DOM with a height of zero, so the zone stays alive and cannot be aimed at -- yet it would
    /// keep the role and swallow every drop meant for the part of the page one can actually see.
    /// </remarks>
    private void UpdateDefaultZoneRole()
    {
        if (this.WantsDefaultZoneRole)
        {
            this.ClaimDefaultZoneRole();
            return;
        }

        if (!this.isDefaultZone)
            return;

        this.EffectiveScope?.ReleaseDefaultZone(this);
        this.isDefaultZone = false;
    }

    /// <summary>
    /// Asks the area for the role of its default target.
    /// </summary>
    private void ClaimDefaultZoneRole()
    {
        if (this.isDefaultZone)
            return;

        if (this.EffectiveScope is null)
        {
            //
            // There is nothing to claim: the surrounding page, assistant, or dialog is not a drop
            // area at all. The flag would then do nothing, and silently -- which is how a zone ends
            // up promising a behaviour it cannot deliver. So say it out loud: either the area needs
            // a drop area of its own, or the flag does not belong here.
            //
            //
            // Reported once only: the claim is retried on every parameter change, and repeating
            // the message on every render would bury the log.
            //
            if (!this.hasReportedDefaultZoneProblem)
            {
                this.hasReportedDefaultZoneProblem = true;
                this.Logger.LogWarning("The drop zone '{ZoneId}' wants to be the default target of its area, but it does not live in a drop area. Dropping next to this zone will do nothing.", this.zoneId);
            }

            return;
        }

        this.isDefaultZone = this.EffectiveScope.TryBecomeDefaultZone(this);

        // Losing the role to a neighbour is a decision, not a defect -- and it can be undone later,
        // when that neighbour goes away. So this one only goes to the debug log, and only once:
        if (this.isDefaultZone || this.hasReportedDefaultZoneProblem)
            return;

        this.hasReportedDefaultZoneProblem = true;
        this.Logger.LogDebug("The drop zone '{ZoneId}' asked to be the default target of its area, which another zone already is. It now takes only the drops aimed at itself.", this.zoneId);
    }

    /// <summary>
    /// Decides whether the named zone is this one.
    /// </summary>
    /// <remarks>
    /// The area counts as this zone as long as this zone is its default target. That is the whole
    /// mechanism behind dropping anywhere in a page and still landing here.
    /// </remarks>
    /// <param name="targetZoneId">The ID the hit test reported, or null when it hit nothing.</param>
    private bool IsThisZone(string? targetZoneId) => targetZoneId is not null && (targetZoneId == this.zoneId || (this.isDefaultZone && targetZoneId == this.EffectiveScope?.ScopeId));

    /// <summary>
    /// Highlights the zone, or takes the highlight away.
    /// </summary>
    /// <remarks>
    /// The comparison is not for tidiness: a throttled drag-over event arrives about ten times per
    /// second, and without it every one of them would render every zone on the page anew.
    /// </remarks>
    private void ApplyHighlight(bool shouldBeHighlighted)
    {
        var highlighted = shouldBeHighlighted && !this.Disabled();
        if (highlighted == this.isHighlighted)
            return;

        this.isHighlighted = highlighted;
        this.StateHasChanged();
    }
}