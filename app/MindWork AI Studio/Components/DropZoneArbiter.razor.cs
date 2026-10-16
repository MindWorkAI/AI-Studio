using AIStudio.Tools.Rust;

using Microsoft.AspNetCore.Components;

namespace AIStudio.Components;

/// <summary>
/// Decides which drop zone a native drag and drop event was aimed at.
/// </summary>
/// <remarks>
/// <para>
/// AI Studio knows no browser drag and drop. The Tauri runtime reports the native events together
/// with the cursor position, and this component turns that position into the ID of the zone
/// underneath. It lets the browser answer that question, because only the browser knows what the
/// page looks like right now: which dialog is open, which zone is scrolled out of sight, which
/// overlay is in the way.
/// </para>
/// <para>
/// It renders nothing and exists once per circuit, rendered from Routes.razor beside the MudBlazor
/// providers and thus outside the router. A component rather than a service, because a service has
/// no reliable moment at which JS interop becomes possible; and not a part of MainLayout, because
/// arbitration would be a foreign body in that file.
/// </para>
/// <para>
/// There is deliberately no fallback for a hit test which cannot be carried out: a circuit whose
/// browser is gone learns nothing about the page and must therefore do nothing. The app keeps
/// disconnected circuits for a long time, see the retention settings in Program.cs, and the message
/// bus reaches all of them. Anything which caught a drop without asking the browser would process
/// one and the same drop once per circuit.
/// </para>
/// </remarks>
public partial class DropZoneArbiter : MSGComponentBase
{
    [Inject]
    private IJSRuntime JsRuntime { get; init; } = null!;

    [Inject]
    private ILogger<DropZoneArbiter> Logger { get; init; } = null!;

    /// <summary>
    /// Which zone we named last, so that an unchanged highlight costs no message.
    /// </summary>
    private string? highlightedZoneId;

    /// <summary>
    /// True while a hit test for the highlight is on its way to the browser.
    /// </summary>
    private bool isHighlightHitTestRunning;

    #region Overrides of MSGComponentBase

    protected override async Task OnInitializedAsync()
    {
        this.ApplyFilters([], [ Event.TAURI_EVENT_RECEIVED ]);
        await base.OnInitializedAsync();
    }

    protected override async Task ProcessIncomingMessage<T>(ComponentBase? sendingComponent, Event triggeredEvent, T? data) where T : default
    {
        switch (triggeredEvent)
        {
            //
            // A drag entered the window or moved inside it. Both say where the cursor is, and
            // nothing more, so both lead to the same question: which zone lights up?
            //
            case Event.TAURI_EVENT_RECEIVED when data is TauriEvent { EventType: TauriEventType.FILE_DROP_HOVERED or TauriEventType.FILE_DROP_OVER } tauriEvent:
                await this.MoveHighlight(tauriEvent);
                break;

            case Event.TAURI_EVENT_RECEIVED when data is TauriEvent { EventType: TauriEventType.FILE_DROP_DROPPED, Payload: var paths } tauriEvent:
                await this.DeliverDroppedPaths(tauriEvent, paths);
                break;

            //
            // The drag left the window, or the window lost the focus while a drag was running. Tauri
            // reports no position for either, and there is nothing left to aim at anyway.
            //
            case Event.TAURI_EVENT_RECEIVED when data is TauriEvent { EventType: TauriEventType.FILE_DROP_CANCELED or TauriEventType.WINDOW_NOT_FOCUSED }:
                await this.NameHighlightedZone(null);
                break;
        }
    }

    #endregion

    /// <summary>
    /// Highlights the zone under the cursor of a running drag.
    /// </summary>
    /// <remarks>
    /// A drag-over event arrives faster than one interop round trip takes, and the message bus
    /// delivers without awaiting the receiver. A hit test which is still on its way therefore
    /// suppresses the next one instead of queueing it: the following event catches up with the
    /// movement anyway, and a queue would only ever fall further behind the cursor.
    /// </remarks>
    private async Task MoveHighlight(TauriEvent tauriEvent)
    {
        if (this.isHighlightHitTestRunning)
            return;

        this.isHighlightHitTestRunning = true;
        try
        {
            var (wasTested, zoneId) = await this.DetermineZoneUnderCursor(tauriEvent);
            if (!wasTested)
                return;

            await this.NameHighlightedZone(zoneId);
        }
        finally
        {
            this.isHighlightHitTestRunning = false;
        }
    }

    /// <summary>
    /// Hands the dropped paths to the zone under the cursor, if there is one.
    /// </summary>
    /// <remarks>
    /// Unlike the highlight, this hit test is never suppressed: a drop happens once and must not be
    /// lost. The highlight goes away first and in every case, because the drag is over no matter
    /// whether the drop finds a zone.
    /// </remarks>
    private async Task DeliverDroppedPaths(TauriEvent tauriEvent, List<string> paths)
    {
        await this.NameHighlightedZone(null);

        var (wasTested, zoneId) = await this.DetermineZoneUnderCursor(tauriEvent);
        if (!wasTested)
            return;

        if (zoneId is null)
        {
            //
            // Nothing under the cursor takes drops, so nothing happens -- which is the point of the
            // whole exercise. The zones which were available are worth logging here, though: this is
            // the one moment where the question "which one should it have been?" gets asked, and it
            // happens once per drag rather than ten times a second.
            //
            if (this.Logger.IsEnabled(LogLevel.Debug))
            {
                var (_, availableZones) = await this.JsRuntime.TryInvokeAsync<string[]>(this.CircuitState, "dropZones.list");
                this.Logger.LogDebug("{Count} dropped path(s) reached no drop zone. Available zones: {Zones}", paths.Count, availableZones is null ? "unknown" : string.Join(", ", availableZones));
            }

            return;
        }

        this.Logger.LogDebug("{Count} path(s) were dropped on the zone '{ZoneId}'.", paths.Count, zoneId);
        await this.SendMessage(Event.PATHS_DROPPED, new DroppedPaths(zoneId, paths));
    }

    /// <summary>
    /// Tells the zones which one of them is under the cursor, unless they know already.
    /// </summary>
    /// <param name="zoneId">The ID of the zone under the cursor, or null for none.</param>
    private async Task NameHighlightedZone(string? zoneId)
    {
        if (zoneId == this.highlightedZoneId)
            return;

        this.highlightedZoneId = zoneId;
        await this.SendMessage(Event.HIGHLIGHT_DROP_ZONE, new DropZoneHighlight(zoneId));
    }

    /// <summary>
    /// Asks the browser which drop zone lies under the cursor of a drag and drop event.
    /// </summary>
    /// <remarks>
    /// The two parts of the result must stay apart. Whether the browser answered at all comes first:
    /// while a circuit is disconnected nobody answers, and acting on an answer we never got is
    /// exactly the mistake this design exists to avoid. Only then comes what the answer was, and
    /// there a null is a legitimate one -- the browser looked and found no zone.
    /// </remarks>
    private async Task<(bool WasTested, string? ZoneId)> DetermineZoneUnderCursor(TauriEvent tauriEvent)
    {
        if (!tauriEvent.TryGetDropPosition(out var x, out var y))
        {
            // The runtime sends a position with every drag and drop event which has one, so this
            // means we are talking to a runtime which does not, i.e. an older one:
            this.Logger.LogWarning("The Tauri event {EventType} carried no cursor position, so the drop zone under it stays unknown.", tauriEvent.EventType);
            return (false, null);
        }

        // A failed or skipped call is already logged by the extension method, which tells a
        // disconnected circuit from a broken call. Nothing to add here, and nothing to do:
        var hitTest = await this.JsRuntime.TryInvokeAsync<string?>(this.CircuitState, "dropZones.hitTest", x, y);

        //
        // One line per hit test, which is about ten per second while a drag lasts. That is the
        // instrument for checking the coordinate space: the position has to follow the cursor, in
        // the middle of the window as well as in all four corners, and on a display with a scale
        // factor other than one. A mistake there shows up as a factor, an offset, or a mirrored y.
        //
        if (hitTest.WasInvoked)
            this.Logger.LogDebug("The event {EventType} at ({X}, {Y}) hit the drop zone '{ZoneId}'.", tauriEvent.EventType, x, y, hitTest.Value ?? "<none>");

        return hitTest;
    }
}