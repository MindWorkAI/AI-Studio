using AIStudio.Tools.Rust;
using AIStudio.Tools.Services;

using Microsoft.AspNetCore.Components;

namespace AIStudio.Components;

public partial class AttachDocuments : MSGComponentBase
{
    [Parameter]
    public HashSet<string> DocumentPaths { get; set; } = [];
    
    [Parameter]
    public EventCallback<HashSet<string>> DocumentPathsChanged { get; set; }
    
    [Parameter]
    public Func<HashSet<string>, Task> OnChange { get; set; } = _ => Task.CompletedTask;
    
    [Inject]
    private RustService RustService { get; init; } = null!;
    
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
            case Event.TAURI_EVENT_RECEIVED when data is TauriEvent { EventType: TauriEventType.FILE_DROP_HOVERED }:
                this.SetDragClass();
                break;
            
            case Event.TAURI_EVENT_RECEIVED when data is TauriEvent { EventType: TauriEventType.FILE_DROP_DROPPED, Payload: var paths }:
                this.ClearDragClass();
                foreach (var path in paths)
                    this.DocumentPaths.Add(path);
                await this.DocumentPathsChanged.InvokeAsync(this.DocumentPaths);
                await this.OnChange(this.DocumentPaths);
                break;
            
            case Event.TAURI_EVENT_RECEIVED when data is TauriEvent { EventType: TauriEventType.FILE_DROP_CANCELED }:
                this.ClearDragClass();
                break;
        }
    }

    #endregion

    private const string DEFAULT_DRAG_CLASS = "relative rounded-lg border-2 border-dashed pa-4 mt-4 mud-width-full mud-height-full";
    
    private string dragClass = DEFAULT_DRAG_CLASS;

    private async Task AddFilesManually()
    {
        var selectedFile = await this.RustService.SelectFile(T("Select a file to attach"));
        if (selectedFile.UserCancelled)
            return;

        if (!File.Exists(selectedFile.SelectedFilePath))
            return;

        var ext = Path.GetExtension(selectedFile.SelectedFilePath).TrimStart('.');
        if (Array.Exists(FileTypeFilter.Executables.FilterExtensions, x => x.Equals(ext, StringComparison.OrdinalIgnoreCase)))
        {
            await MessageBus.INSTANCE.SendError(new(Icons.Material.Filled.AppBlocking, T("Executables are not allowed")));
            return;
        }

        if (Array.Exists(FileTypeFilter.AllImages.FilterExtensions, x => x.Equals(ext, StringComparison.OrdinalIgnoreCase)))
        {
            await MessageBus.INSTANCE.SendWarning(new(Icons.Material.Filled.ImageNotSupported, T("Images are not supported yet")));
            return;
        }

        this.DocumentPaths.Add(selectedFile.SelectedFilePath);
        await this.DocumentPathsChanged.InvokeAsync(this.DocumentPaths);
        await this.OnChange(this.DocumentPaths);
    }

    private void SetDragClass() => this.dragClass = $"{DEFAULT_DRAG_CLASS} mud-border-primary";
    
    private void ClearDragClass() => this.dragClass = DEFAULT_DRAG_CLASS;
}