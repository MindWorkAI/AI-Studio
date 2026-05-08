using AIStudio.Components;
using AIStudio.Tools.Services;

using Microsoft.AspNetCore.Components;

namespace AIStudio.Pages;

public partial class Embeddings : MSGComponentBase
{
    [Inject]
    private DataSourceEmbeddingService DataSourceEmbeddingService { get; init; } = null!;

    private IReadOnlyList<DataSourceEmbeddingStatus> Statuses { get; set; } = [];

    private int TotalIndexedFiles => this.Statuses.Sum(status => status.IndexedFiles);

    private int TotalPendingFiles => this.Statuses.Sum(status => Math.Max(0, status.TotalFiles - status.IndexedFiles - status.FailedFiles));

    private int TotalFailedFiles => this.Statuses.Sum(status => status.FailedFiles);

    protected override async Task OnInitializedAsync()
    {
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
}
