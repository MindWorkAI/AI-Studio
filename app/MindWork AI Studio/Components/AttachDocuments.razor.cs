using AIStudio.Tools.Rust;
using AIStudio.Tools.Services;

using Microsoft.AspNetCore.Components;

namespace AIStudio.Components;

public partial class AttachDocuments : MSGComponentBase
{
    [Parameter]
    public string Name { get; set; } = string.Empty;
    
    [Parameter]
    public HashSet<string> DocumentPaths { get; set; } = [];
    
    [Parameter]
    public EventCallback<HashSet<string>> DocumentPathsChanged { get; set; }
    
    [Parameter]
    public Func<HashSet<string>, Task> OnChange { get; set; } = _ => Task.CompletedTask;
    
    /// <summary>
    /// Catch all documents that are hovered over the AI Studio window and not only over the drop zone. 
    /// </summary>
    [Parameter] 
    public bool CatchAllDocuments { get; set; }
    
    [Inject]
    private ILogger<AttachDocuments> Logger { get; set; } = null!;
    
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
                if(!this.isComponentHovered && !this.CatchAllDocuments)
                {
                    this.Logger.LogDebug("Attach documents component '{Name}' is not hovered, ignoring file drop hovered event.", this.Name);
                    return;
                }
                
                this.SetDragClass();
                this.StateHasChanged();
                break;
            
            case Event.TAURI_EVENT_RECEIVED when data is TauriEvent { EventType: TauriEventType.FILE_DROP_DROPPED, Payload: var paths }:
                if(!this.isComponentHovered && !this.CatchAllDocuments)
                {
                    this.Logger.LogDebug("Attach documents component '{Name}' is not hovered, ignoring file drop dropped event.", this.Name);
                    return;
                }
                
                foreach (var path in paths)
                {
                    if(!await this.IsFileExtensionValid(path))
                        continue;
                    
                    this.DocumentPaths.Add(path);
                }

                await this.DocumentPathsChanged.InvokeAsync(this.DocumentPaths);
                await this.OnChange(this.DocumentPaths);
                this.StateHasChanged();
                break;
        }
    }

    #endregion

    private const string DEFAULT_DRAG_CLASS = "relative rounded-lg border-2 border-dashed pa-4 mt-4 mud-width-full mud-height-full";
    
    private string dragClass = DEFAULT_DRAG_CLASS;
    
    private bool isComponentHovered;

    private async Task AddFilesManually()
    {
        var selectedFile = await this.RustService.SelectFile(T("Select a file to attach"));
        if (selectedFile.UserCancelled)
            return;

        if (!File.Exists(selectedFile.SelectedFilePath))
            return;

        if (!await this.IsFileExtensionValid(selectedFile.SelectedFilePath))
            return;

        this.DocumentPaths.Add(selectedFile.SelectedFilePath);
        await this.DocumentPathsChanged.InvokeAsync(this.DocumentPaths);
        await this.OnChange(this.DocumentPaths);
    }

    private async Task<bool> IsFileExtensionValid(string selectedFile)
    {
        var ext = Path.GetExtension(selectedFile).TrimStart('.');
        if (Array.Exists(FileTypeFilter.Executables.FilterExtensions, x => x.Equals(ext, StringComparison.OrdinalIgnoreCase)))
        {
            await MessageBus.INSTANCE.SendError(new(Icons.Material.Filled.AppBlocking, this.T("Executables are not allowed")));
            return false;
        }

        if (Array.Exists(FileTypeFilter.AllImages.FilterExtensions, x => x.Equals(ext, StringComparison.OrdinalIgnoreCase)))
        {
            await MessageBus.INSTANCE.SendWarning(new(Icons.Material.Filled.ImageNotSupported, this.T("Images are not supported yet")));
            return false;
        }
        
        if (Array.Exists(FileTypeFilter.AllVideos.FilterExtensions, x => x.Equals(ext, StringComparison.OrdinalIgnoreCase)))
        {
            await MessageBus.INSTANCE.SendWarning(new(Icons.Material.Filled.FeaturedVideo, this.T("Videos are not supported yet")));
            return false;
        }

        return true;
    }

    private async Task ClearAllFiles()
    {
        this.DocumentPaths.Clear();
        await this.DocumentPathsChanged.InvokeAsync(this.DocumentPaths);
        await this.OnChange(this.DocumentPaths);
    }

    private void SetDragClass() => this.dragClass = $"{DEFAULT_DRAG_CLASS} mud-border-primary border-4";
    
    private void ClearDragClass() => this.dragClass = DEFAULT_DRAG_CLASS;
    
    private void OnMouseEnter(EventArgs _)
    {
        this.Logger.LogDebug("Attach documents component '{Name}' is hovered.", this.Name);
        this.isComponentHovered = true;
        this.SetDragClass();
        this.StateHasChanged();
    }
    
    private void OnMouseLeave(EventArgs _)
    {
        this.Logger.LogDebug("Attach documents component '{Name}' is no longer hovered.", this.Name);
        this.isComponentHovered = false;
        this.ClearDragClass();
        this.StateHasChanged();
    }
}