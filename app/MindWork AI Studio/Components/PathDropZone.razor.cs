using AIStudio.Tools.Rust;

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
    /// On which layer to register the drop area. Higher layers have priority over lower layers.
    /// </summary>
    [Parameter]
    public int Layer { get; set; } = DropLayers.ROOT;

    /// <summary>
    /// Catch all documents that are hovered over the AI Studio window and not only over the drop zone.
    /// </summary>
    /// <remarks>
    /// Practically every zone needs this today. Hovering is detected through mouse events, and no
    /// webview delivers those while a native drag is in progress, so a zone without this flag
    /// hardly ever catches anything. The consequence is that two zones of the same layer cannot be
    /// told apart: the one carrying this flag takes every drop, including the ones meant for the
    /// other. A page may therefore hold only one zone per layer. Lifting that limit needs the
    /// cursor position, which the runtime receives from Tauri and currently discards in
    /// app_window.rs.
    /// </remarks>
    [Parameter]
    public bool CatchAllDocuments { get; set; }

    /// <summary>
    /// When true, the zone ignores drops and is not highlighted.
    /// </summary>
    /// <remarks>
    /// The drop area stays registered nevertheless. Releasing it during the lifetime of the
    /// component would lower the count of every zone below this one, and those zones would then
    /// catch files while this one is still on screen.
    /// </remarks>
    [Parameter]
    public bool Disabled { get; set; }

    [Inject]
    private ILogger<PathDropZone> Logger { get; init; } = null!;

    private const string DEFAULT_DRAG_CLASS = "relative rounded-lg border-2 border-dashed pa-3 mb-3 mud-width-full";

    private string dragClass = DEFAULT_DRAG_CLASS;
    private uint numDropAreasAboveThis;
    private bool isComponentHovered;

    #region Overrides of MSGComponentBase

    protected override async Task OnInitializedAsync()
    {
        this.ApplyFilters([], [ Event.TAURI_EVENT_RECEIVED, Event.REGISTER_FILE_DROP_AREA, Event.UNREGISTER_FILE_DROP_AREA ]);
        await this.MessageBus.SendMessage(this, Event.REGISTER_FILE_DROP_AREA, this.Layer);

        await base.OnInitializedAsync();
    }

    /// <summary>
    /// Releases the drop area.
    /// </summary>
    protected override void DisposeResources()
    {
        // Without this, drop areas below this one would count this component forever and would
        // stop catching dropped files:
        this.MessageBus.SendMessage(this, Event.UNREGISTER_FILE_DROP_AREA, this.Layer).Observe($"{nameof(PathDropZone)}: releasing the drop area");

        base.DisposeResources();
    }

    protected override async Task ProcessIncomingMessage<T>(ComponentBase? sendingComponent, Event triggeredEvent, T? data) where T : default
    {
        // A disabled zone takes no files. It keeps track of the zones above it, though, because
        // those come and go while this one is disabled:
        if (this.Disabled && triggeredEvent == Event.TAURI_EVENT_RECEIVED)
            return;

        switch (triggeredEvent)
        {
            case Event.REGISTER_FILE_DROP_AREA when sendingComponent != this:
            {
                if(data is int layer && layer > this.Layer)
                {
                    this.numDropAreasAboveThis++;
                    this.ClearDragClass();
                }

                break;
            }

            case Event.UNREGISTER_FILE_DROP_AREA when sendingComponent != this:
            {
                if(data is int layer && layer > this.Layer && this.numDropAreasAboveThis > 0)
                    this.numDropAreasAboveThis--;

                break;
            }

            case Event.TAURI_EVENT_RECEIVED when data is TauriEvent { EventType: TauriEventType.FILE_DROP_HOVERED }:
                if(!this.CanCatchDroppedPath())
                    return;

                this.SetDragClass();
                this.StateHasChanged();
                break;

            case Event.TAURI_EVENT_RECEIVED when data is TauriEvent { EventType: TauriEventType.FILE_DROP_CANCELED }:
            case Event.TAURI_EVENT_RECEIVED when data is TauriEvent { EventType: TauriEventType.WINDOW_NOT_FOCUSED }:
                this.isComponentHovered = false;
                this.ClearDragClass();
                this.StateHasChanged();
                break;

            case Event.TAURI_EVENT_RECEIVED when data is TauriEvent { EventType: TauriEventType.FILE_DROP_DROPPED, Payload: var paths }:
                if(!this.CanCatchDroppedPath())
                    return;

                this.Logger.LogDebug("The path drop zone on layer {Layer} caught {Count} path(s).", this.Layer, paths.Count);
                await this.OnPathsDropped.InvokeAsync(paths);
                this.ClearDragClass();
                this.StateHasChanged();
                break;
        }
    }

    #endregion

    private bool CanCatchDroppedPath() => this.numDropAreasAboveThis is 0 && (this.isComponentHovered || this.CatchAllDocuments);

    private void SetDragClass() => this.dragClass = $"{DEFAULT_DRAG_CLASS} mud-border-primary border-2";

    private void ClearDragClass() => this.dragClass = DEFAULT_DRAG_CLASS;

    private void OnMouseEnter(EventArgs _)
    {
        if(this.Disabled || this.numDropAreasAboveThis > 0)
            return;

        // A native drag delivers no DOM events at all, mouse events included. This fires before a
        // drag begins, while the pointer still moves freely, which makes it a hint about where the
        // user is aiming rather than a reliable signal. See the remarks on CatchAllDocuments:
        this.isComponentHovered = true;
        this.SetDragClass();
        this.StateHasChanged();
    }

    private void OnMouseLeave(EventArgs _)
    {
        if(this.Disabled)
            return;

        this.isComponentHovered = false;
        this.ClearDragClass();
        this.StateHasChanged();
    }
}