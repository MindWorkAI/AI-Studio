using AIStudio.Components;
using AIStudio.Settings.DataModel;
using AIStudio.Tools.Services;

using Microsoft.AspNetCore.Components;

namespace AIStudio.Pages;

public partial class Embeddings : MSGComponentBase
{
    [Inject]
    private DataSourceEmbeddingService DataSourceEmbeddingService { get; init; } = null!;

    [Inject]
    private NavigationManager NavigationManager { get; init; } = null!;

    private IReadOnlyList<DataSourceEmbeddingStatus> Statuses { get; set; } = [];

    private int TotalIndexedFiles => this.Statuses.Sum(status => status.IndexedFiles);

    private int TotalPendingFiles => this.Statuses.Sum(status => Math.Max(0, status.TotalFiles - status.IndexedFiles - status.FailedFiles - status.PermanentlySkippedFiles));

    private int TotalFailedFiles => this.Statuses.Sum(status => status.FailedFiles);

    private int TotalPermanentlySkippedFiles => this.Statuses.Sum(status => status.PermanentlySkippedFiles);

    protected override async Task OnInitializedAsync()
    {
        //
        // This page belongs to the local RAG preview feature. Unlike the other preview pages, it
        // has a route of its own, so it can be reached by typing the address even while the feature
        // is switched off. There is nothing to show in that case.
        //
        if (!PreviewFeatures.PRE_RAG_2024.IsEnabled(this.SettingsManager))
        {
            this.NavigationManager.NavigateTo(Routes.HOME);
            return;
        }

        this.ApplyFilters([], [ Event.RAG_EMBEDDING_STATUS_CHANGED, Event.CONFIGURATION_CHANGED ]);
        await base.OnInitializedAsync();
        this.ReloadStatuses();
    }

    protected override Task ProcessIncomingMessage<T>(ComponentBase? sendingComponent, Event triggeredEvent, T? data) where T : default
    {
        if (triggeredEvent is Event.RAG_EMBEDDING_STATUS_CHANGED or Event.CONFIGURATION_CHANGED)
        {
            this.ReloadStatuses();
            this.StateHasChanged();
        }

        return Task.CompletedTask;
    }

    private void ReloadStatuses()
    {
        this.Statuses = this.DataSourceEmbeddingService
            .GetStatuses()
            .OrderBy(status => status.SortOrder)
            .ThenBy(status => status.DataSourceName, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static Color GetStatusColor(DataSourceEmbeddingStatus status) => status.State switch
    {
        DataSourceEmbeddingState.RUNNING => Color.Warning,
        DataSourceEmbeddingState.QUEUED => Color.Info,
        DataSourceEmbeddingState.FAILED => Color.Error,
        DataSourceEmbeddingState.COMPLETED when status.FailedFiles > 0 => Color.Warning,
        DataSourceEmbeddingState.COMPLETED => Color.Success,
        _ => Color.Default,
    };

    /// <summary>
    /// Names the list of failures for what it holds.
    /// </summary>
    /// <remarks>
    /// A data source whose files were all skipped for good has nothing wrong with it, so calling
    /// the list failures would contradict the green state right above it.
    /// </remarks>
    private string GetFailureListHeader(DataSourceEmbeddingStatus status) => status.FailedFiles > 0
        ? string.Format(T("Failure details ({0})"), status.Failures.Count)
        : string.Format(T("Skipped files ({0})"), status.Failures.Count);

    private static string GetFailureListIcon(DataSourceEmbeddingStatus status) => status.FailedFiles > 0
        ? Icons.Material.Filled.ReportProblem
        : Icons.Material.Filled.SkipNext;

    private bool CanRefresh(DataSourceEmbeddingStatus status)
    {
        return this.DataSourceEmbeddingService.CanRefreshDataSource(status.DataSourceId) &&
            status.State is not DataSourceEmbeddingState.RUNNING and not DataSourceEmbeddingState.QUEUED &&
            (status.State is DataSourceEmbeddingState.FAILED || status.FailedFiles > 0);
    }

    /// <summary>
    /// Takes the user to the settings, where the embedding providers are configured.
    /// </summary>
    /// <remarks>
    /// Offered only for the failures a setting fixes, such as a rejected API key. Reading what
    /// went wrong and then having to find the right page is where people give up.
    /// </remarks>
    private void OpenEmbeddingProviderSettings() => this.NavigationManager.NavigateTo(Routes.SETTINGS);

    private async Task RefreshDataSource(DataSourceEmbeddingStatus status)
    {
        await this.DataSourceEmbeddingService.RetryDataSourceAsync(status.DataSourceId);
        this.ReloadStatuses();
        await this.InvokeAsync(this.StateHasChanged);
    }
}
