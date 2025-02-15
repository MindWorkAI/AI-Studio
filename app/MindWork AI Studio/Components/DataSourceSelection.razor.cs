using AIStudio.Settings;
using AIStudio.Settings.DataModel;
using AIStudio.Tools.Services;

using Microsoft.AspNetCore.Components;

namespace AIStudio.Components;

public partial class DataSourceSelection : ComponentBase, IMessageBusReceiver, IDisposable
{
    [Parameter]
    public DataSourceSelectionMode SelectionMode { get; set; } = DataSourceSelectionMode.SELECTION_MODE;
    
    [Parameter]
    public PopoverTriggerMode PopoverTriggerMode { get; set; } = PopoverTriggerMode.BUTTON;
    
    [Parameter]
    public string PopoverButtonClasses { get; set; } = string.Empty;
    
    [Parameter]
    public required AIStudio.Settings.Provider LLMProvider { get; set; }

    [Parameter]
    public required DataSourceOptions DataSourceOptions { get; set; }
    
    [Parameter]
    public EventCallback<DataSourceOptions> DataSourceOptionsChanged { get; set; }
    
    [Parameter]
    public string ConfigurationHeaderMessage { get; set; } = string.Empty;
    
    [Parameter]
    public bool AutoSaveAppSettings { get; set; }
    
    [Inject]
    private SettingsManager SettingsManager { get; init; } = null!;
    
    [Inject]
    private MessageBus MessageBus { get; init; } = null!;
    
    [Inject]
    private DataSourceService DataSourceService { get; init; } = null!;
    
    [Inject]
    private ILogger<DataSourceSelection> Logger { get; init; } = null!;

    private bool internalChange;
    private bool showDataSourceSelection;
    private bool waitingForDataSources = true;
    private IReadOnlyList<IDataSource> availableDataSources = [];
    private IReadOnlyCollection<IDataSource> selectedDataSources = [];
    private bool aiBasedSourceSelection;
    private bool aiBasedValidation;
    private bool areDataSourcesEnabled;
    
    #region Overrides of ComponentBase

    protected override async Task OnInitializedAsync()
    {
        this.MessageBus.RegisterComponent(this);
        this.MessageBus.ApplyFilters(this, [], [ Event.COLOR_THEME_CHANGED ]);
        
        //
        // Load the settings:
        //
        this.aiBasedSourceSelection = this.DataSourceOptions.AutomaticDataSourceSelection;
        this.aiBasedValidation = this.DataSourceOptions.AutomaticValidation;
        this.areDataSourcesEnabled = !this.DataSourceOptions.DisableDataSources;
        this.waitingForDataSources = this.areDataSourcesEnabled;

        //
        // Preselect the data sources. Right now, we cannot filter
        // the data sources. Later, when the component is shown, we
        // will filter the data sources.
        //
        // Right before the preselection would be used to kick off the
        // RAG process, we will filter the data sources as well.
        //
        var preselectedSources = new List<IDataSource>(this.DataSourceOptions.PreselectedDataSourceIds.Count);
        foreach (var preselectedDataSourceId in this.DataSourceOptions.PreselectedDataSourceIds)
        {
            var dataSource = this.SettingsManager.ConfigurationData.DataSources.FirstOrDefault(ds => ds.Id == preselectedDataSourceId);
            if (dataSource is not null)
                preselectedSources.Add(dataSource);
        }
        
        this.selectedDataSources = preselectedSources;
        await base.OnInitializedAsync();
    }

    protected override async Task OnParametersSetAsync()
    {
        if (!this.internalChange)
        {
            this.aiBasedSourceSelection = this.DataSourceOptions.AutomaticDataSourceSelection;
            this.aiBasedValidation = this.DataSourceOptions.AutomaticValidation;
            this.areDataSourcesEnabled = !this.DataSourceOptions.DisableDataSources;
        }

        switch (this.SelectionMode)
        {
            //
            // In selection mode, we have to load & filter the data sources
            // when the component is shown:
            //
            case DataSourceSelectionMode.SELECTION_MODE:
                
                //
                // For external changes, we have to reload & filter
                // the data sources:
                //
                if (this.showDataSourceSelection && !this.internalChange)
                    await this.LoadAndApplyFilters();
                else
                    this.internalChange = false;
                
                break;
            
            //
            // In configuration mode, we have to load all data sources:
            //
            case DataSourceSelectionMode.CONFIGURATION_MODE:
                this.availableDataSources = this.SettingsManager.ConfigurationData.DataSources;
                break;
        }
        
        await base.OnParametersSetAsync();
    }

    #endregion

    public void ChangeOptionWithoutSaving(DataSourceOptions options)
    {
        this.DataSourceOptions = options;
        this.aiBasedSourceSelection = this.DataSourceOptions.AutomaticDataSourceSelection;
        this.aiBasedValidation = this.DataSourceOptions.AutomaticValidation;
        this.areDataSourcesEnabled = !this.DataSourceOptions.DisableDataSources;
        this.selectedDataSources = this.SettingsManager.ConfigurationData.DataSources.Where(ds => this.DataSourceOptions.PreselectedDataSourceIds.Contains(ds.Id)).ToList();

        //
        // Remark: We do not apply the filters here. This is done later
        // when either the parameters are changed or just before the
        // RAG process is started (outside of this component).
        //
        // In fact, when we apply the filters here, multiple calls
        // to the filter method would be made. We would get conflicts.
        //
    }
    
    private async Task LoadAndApplyFilters()
    {
        if(this.DataSourceOptions.DisableDataSources)
            return;
        
        this.waitingForDataSources = true;
        this.StateHasChanged();
            
        // Load the data sources:
        var sources = await this.DataSourceService.GetDataSources(this.LLMProvider, this.selectedDataSources);
        this.availableDataSources = sources.AllowedDataSources;
        this.selectedDataSources = sources.SelectedDataSources;
        this.waitingForDataSources = false;
        this.StateHasChanged();
    }
    
    private async Task EnabledChanged(bool state)
    {
        this.areDataSourcesEnabled = state;
        this.DataSourceOptions.DisableDataSources = !this.areDataSourcesEnabled;
        
        await this.LoadAndApplyFilters();
        await this.OptionsChanged();
        this.StateHasChanged();
    }
    
    private async Task AutoModeChanged(bool state)
    {
        this.aiBasedSourceSelection = state;
        this.DataSourceOptions.AutomaticDataSourceSelection = this.aiBasedSourceSelection;
        
        await this.OptionsChanged();
    }
    
    private async Task ValidationModeChanged(bool state)
    {
        this.aiBasedValidation = state;
        this.DataSourceOptions.AutomaticValidation = this.aiBasedValidation;
        
        await this.OptionsChanged();
    }
    
    private async Task SelectionChanged(IReadOnlyCollection<IDataSource>? chosenDataSources)
    {
        this.selectedDataSources = chosenDataSources ?? [];
        this.DataSourceOptions.PreselectedDataSourceIds = this.selectedDataSources.Select(ds => ds.Id).ToList();

        await this.OptionsChanged();
    }

    private async Task OptionsChanged()
    {
        this.internalChange = true;
        
        await this.DataSourceOptionsChanged.InvokeAsync(this.DataSourceOptions);
        
        if(this.AutoSaveAppSettings)
            await this.SettingsManager.StoreSettings();
    }
    
    private async Task ToggleDataSourceSelection()
    {
        this.showDataSourceSelection = !this.showDataSourceSelection;
        if (this.showDataSourceSelection)
            await this.LoadAndApplyFilters();
    }
    
    private void HideDataSourceSelection() => this.showDataSourceSelection = false;

    #region Implementation of IMessageBusReceiver

    public string ComponentName => nameof(ConfidenceInfo);
    
    public Task ProcessMessage<T>(ComponentBase? sendingComponent, Event triggeredEvent, T? data)
    {
        switch (triggeredEvent)
        {
            case Event.COLOR_THEME_CHANGED:
                this.showDataSourceSelection = false;
                this.StateHasChanged();
                break;
        }
        
        return Task.CompletedTask;
    }

    public Task<TResult?> ProcessMessageWithResult<TPayload, TResult>(ComponentBase? sendingComponent, Event triggeredEvent, TPayload? data)
    {
        return Task.FromResult<TResult?>(default);
    }

    #endregion
    
    #region Implementation of IDisposable

    public void Dispose()
    {
        this.MessageBus.Unregister(this);
    }

    #endregion
}