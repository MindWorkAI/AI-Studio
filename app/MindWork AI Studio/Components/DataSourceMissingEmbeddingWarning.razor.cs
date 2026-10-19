using AIStudio.Settings;
using AIStudio.Tools.Services;

using Microsoft.AspNetCore.Components;

namespace AIStudio.Components;

/// <summary>
/// Tells that no embedding provider exists, which every local data source needs, and offers to add
/// one right there.
/// </summary>
/// <remarks>
/// Shown where a local data source is set up: above the data source table, and in the dialogs for
/// adding or editing one. Without it, the missing provider only came up when the user saved the
/// dialog, after filling in everything else, and leaving the dialog to add one lost all of it.
/// </remarks>
public partial class DataSourceMissingEmbeddingWarning : MSGComponentBase
{
    /// <summary>
    /// Whether the warning sits in the dialog of a data source rather than above the data source table.
    /// </summary>
    [Parameter]
    public bool IsInDataSourceDialog { get; set; }

    /// <summary>
    /// Receives the embedding provider the user just added, so that a dialog can select it.
    /// </summary>
    [Parameter]
    public EventCallback<EmbeddingProvider> EmbeddingProviderAdded { get; set; }

    [Inject]
    private IDialogService DialogService { get; init; } = null!;

    [Inject]
    private DataSourceEmbeddingService DataSourceEmbeddingService { get; init; } = null!;

    /// <summary>
    /// Whether the dialog for adding a provider is open, so that a second click does not open another one.
    /// </summary>
    private bool isAdding;

    private bool IsMissing => EmbeddingProviderSetup.IsMissing(this.SettingsManager);

    private bool MayAdd => EmbeddingProviderSetup.MayAdd(this.SettingsManager);

    private bool MayImport => EmbeddingProviderSetup.MayImport(this.SettingsManager);

    private string Text
    {
        get
        {
            var problem = this.IsInDataSourceDialog
                ? T("No embedding provider is configured yet, but this data source needs one.")
                : T("No embedding provider is configured yet. Local data sources, such as your files and folders, need one: it prepares your data so that the AI can search it.");

            if (!this.MayAdd)
                return $"{problem} {T("Your organization manages the embedding providers and has not provided one yet. Please ask your IT department.")}";

            // Above the table, the buttons speak for themselves. In a dialog, the worry is about what was entered so far:
            return this.IsInDataSourceDialog
                ? $"{problem} {T("You can add one right here; everything you entered in this dialog stays as it is.")}"
                : problem;
        }
    }

    #region Overrides of ComponentBase

    protected override async Task OnInitializedAsync()
    {
        await base.OnInitializedAsync();

        // The warning goes away as soon as a provider exists, also one which a configuration plugin brings along:
        this.ApplyFilters([], [ Event.CONFIGURATION_CHANGED ]);
    }

    #endregion

    #region Overrides of MSGComponentBase

    protected override Task ProcessIncomingMessage<T>(ComponentBase? sendingComponent, Event triggeredEvent, T? data) where T : default
    {
        if (triggeredEvent is Event.CONFIGURATION_CHANGED)
            this.StateHasChanged();

        return base.ProcessIncomingMessage(sendingComponent, triggeredEvent, data);
    }

    #endregion

    private Task AddEmbeddingProvider() => this.AddOnceAsync(() => EmbeddingProviderSetup.AddAsync(this.DialogService, this.SettingsManager, this.DataSourceEmbeddingService));

    private Task ImportEmbeddingProvider() => this.AddOnceAsync(() => EmbeddingProviderSetup.ImportAsync(this.DialogService, this.SettingsManager, this.DataSourceEmbeddingService));

    private async Task AddOnceAsync(Func<Task<EmbeddingProvider?>> addProvider)
    {
        if (this.isAdding)
            return;

        this.isAdding = true;
        try
        {
            var addedProvider = await addProvider();
            if (addedProvider is null)
                return;

            await this.MessageBus.SendMessage<bool>(this, Event.CONFIGURATION_CHANGED);
            await this.EmbeddingProviderAdded.InvokeAsync(addedProvider);
        }
        finally
        {
            this.isAdding = false;
        }
    }
}