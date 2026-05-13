using System.Text.Json;

using AIStudio.Settings;

namespace AIStudio.Tools.Services;

public sealed partial class DataSourceEmbeddingService
{
    private const string STATE_FILENAME = "rag-embedding-state.json";

    private readonly JsonSerializerOptions jsonOptions = new()
    {
        WriteIndented = true,
    };

    private async Task EnsureStateLoadedAsync(CancellationToken token)
    {
        if (this.stateLoaded)
            return;

        await this.stateLock.WaitAsync(token);
        try
        {
            if (this.stateLoaded)
                return;

            var statePath = this.GetStatePath();
            if (!string.IsNullOrWhiteSpace(statePath) && File.Exists(statePath))
            {
                var json = await File.ReadAllTextAsync(statePath, token);
                var persistedState = JsonSerializer.Deserialize<PersistedEmbeddingState>(json, this.jsonOptions);
                this.manifests = persistedState?.DataSources ?? new Dictionary<string, DataSourceEmbeddingManifest>(StringComparer.OrdinalIgnoreCase);
            }

            this.stateLoaded = true;
        }
        finally
        {
            this.stateLock.Release();
        }
    }

    private async Task<DataSourceEmbeddingManifest> GetManifestAsync(string dataSourceId, CancellationToken token)
    {
        await this.EnsureStateLoadedAsync(token);
        if (this.manifests.TryGetValue(dataSourceId, out var manifest))
            return manifest;

        manifest = new DataSourceEmbeddingManifest();
        this.manifests[dataSourceId] = manifest;
        return manifest;
    }

    private async Task SaveStateAsync(CancellationToken token)
    {
        var statePath = this.GetStatePath();
        if (string.IsNullOrWhiteSpace(statePath))
            return;

        var directory = Path.GetDirectoryName(statePath);
        if (!string.IsNullOrWhiteSpace(directory))
            Directory.CreateDirectory(directory);

        var persistedState = new PersistedEmbeddingState
        {
            DataSources = this.manifests
        };

        var json = JsonSerializer.Serialize(persistedState, this.jsonOptions);
        await File.WriteAllTextAsync(statePath, json, token);
    }

    private async Task ResetPersistedStateAsync(string dataSourceId)
    {
        await this.EnsureStateLoadedAsync(CancellationToken.None);
        this.manifests.Remove(dataSourceId);
        await this.DeleteCollectionAsync(this.GetCollectionName(dataSourceId));
        await this.SaveStateAsync(CancellationToken.None);
        this.logger.LogInformation("Reset persisted embedding state for data source '{DataSourceId}'.", dataSourceId);
    }

    private string GetStatePath()
    {
        if (string.IsNullOrWhiteSpace(SettingsManager.ConfigDirectory))
            return string.Empty;

        return Path.Combine(SettingsManager.ConfigDirectory, STATE_FILENAME);
    }
}
