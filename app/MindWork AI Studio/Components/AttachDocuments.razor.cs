using AIStudio.Dialogs;
using AIStudio.Tools.Rust;
using AIStudio.Tools.Services;
using AIStudio.Tools.Validation;

using Microsoft.AspNetCore.Components;

namespace AIStudio.Components;

using DialogOptions = Dialogs.DialogOptions;

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
    
    [Parameter]
    public bool UseSmallForm { get; set; }
    
    [Inject]
    private ILogger<AttachDocuments> Logger { get; set; } = null!;
    
    [Inject]
    private RustService RustService { get; init; } = null!;
    
    [Inject]
    private IDialogService DialogService { get; init; } = null!;

    [Inject]
    private PandocAvailabilityService PandocAvailabilityService { get; init; } = null!;

    private const Placement TOOLBAR_TOOLTIP_PLACEMENT = Placement.Top;
    
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

                // Ensure that Pandoc is installed and ready:
                var pandocState = await this.PandocAvailabilityService.EnsureAvailabilityAsync(
                    showSuccessMessage: false,
                    showDialog: true);

                // If Pandoc is not available (user cancelled installation), abort file drop:
                if (!pandocState.IsAvailable)
                {
                    this.Logger.LogWarning("The user cancelled the Pandoc installation or Pandoc is not available. Aborting file drop.");
                    this.ClearDragClass();
                    this.StateHasChanged();
                    return;
                }

                foreach (var path in paths)
                {
                    if(!await FileExtensionValidation.IsExtensionValidWithNotifyAsync(path))
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
        // Ensure that Pandoc is installed and ready:
        var pandocState = await this.PandocAvailabilityService.EnsureAvailabilityAsync(
            showSuccessMessage: false,
            showDialog: true);

        // If Pandoc is not available (user cancelled installation), abort file selection:
        if (!pandocState.IsAvailable)
        {
            this.Logger.LogWarning("The user cancelled the Pandoc installation or Pandoc is not available. Aborting file selection.");
            return;
        }

        var selectFiles = await this.RustService.SelectFiles(T("Select a file to attach"));
        if (selectFiles.UserCancelled)
            return;

        foreach (var selectedFilePath in selectFiles.SelectedFilePaths)
        {
            if (!File.Exists(selectedFilePath))
                continue;

            if (!await FileExtensionValidation.IsExtensionValidWithNotifyAsync(selectedFilePath))
                return;

            this.DocumentPaths.Add(selectedFilePath);
        }
        
        await this.DocumentPathsChanged.InvokeAsync(this.DocumentPaths);
        await this.OnChange(this.DocumentPaths);
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

    private async Task RemoveDocumentPathFromDocumentPaths(FileInfo file)
    {
        this.DocumentPaths.Remove(file.ToString());
        
        await this.DocumentPathsChanged.InvokeAsync(this.DocumentPaths);
        await this.OnChange(this.DocumentPaths);
    }

    /// <summary>
    /// The user might want to check what we actually extract from his file and therefore give the LLM as an input. 
    /// </summary>
    /// <param name="file">The file to check.</param>
    private async Task InvestigateFile(FileInfo file)
    {
        var dialogParameters = new DialogParameters<DocumentCheckDialog>
        {
            { x => x.FilePath, file.FullName },
        };

        await this.DialogService.ShowAsync<DocumentCheckDialog>(T("Document Preview"), dialogParameters, DialogOptions.FULLSCREEN);
    }
}