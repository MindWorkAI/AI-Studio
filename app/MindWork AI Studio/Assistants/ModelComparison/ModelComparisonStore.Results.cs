using System.Runtime.CompilerServices;

namespace AIStudio.Assistants.ModelComparison;

public sealed partial class ModelComparisonStore
{
    /// <summary>
    /// The folder of the stored comparisons, one file per batch.
    /// </summary>
    private const string RESULTS_DIRECTORY_NAME = "results";

    /// <summary>
    /// Gets the folder of the stored comparisons.
    /// </summary>
    private string ResultsDirectory => Path.Combine(this.RootDirectory, RESULTS_DIRECTORY_NAME);

    /// <summary>
    /// Gets the file of a stored comparison.
    /// </summary>
    /// <param name="recordId">The ID of the batch.</param>
    private string ResultPath(Guid recordId) => Path.Combine(this.ResultsDirectory, $"{recordId:D}.json");

    /// <summary>
    /// Stores what the assistant keeps of a batch, in place of what it kept of the same batch before.
    /// </summary>
    /// <param name="record">The stored comparison.</param>
    /// <param name="token">The cancellation token.</param>
    /// <exception cref="ArgumentException">The stored comparison has no ID.</exception>
    public async Task SaveResultAsync(ModelComparisonRecord record, CancellationToken token = default)
    {
        if (record.Id == Guid.Empty)
            throw new ArgumentException("A stored comparison needs an ID.", nameof(record));

        await this.InitializeAsync(token);
        var gate = this.GetLock(record.Id);
        await gate.WaitAsync(token);
        try
        {
            await WriteJsonAtomicAsync(this.ResultPath(record.Id), record, token);
        }
        finally
        {
            gate.Release();
        }
    }

    /// <summary>
    /// Reads the stored comparisons one by one, so that never more than one of them is in memory at once.
    /// </summary>
    /// <remarks>
    /// A file this version cannot read is skipped, but stays on the disk, see ModelComparisonJson.
    /// </remarks>
    /// <param name="token">The cancellation token.</param>
    /// <returns>The stored comparisons, in no particular order.</returns>
    public async IAsyncEnumerable<ModelComparisonRecord> ReadResultsAsync([EnumeratorCancellation] CancellationToken token = default)
    {
        await this.InitializeAsync(token);
        if (!Directory.Exists(this.ResultsDirectory))
            yield break;

        foreach (var path in Directory.EnumerateFiles(this.ResultsDirectory, "*.json"))
        {
            token.ThrowIfCancellationRequested();
            if (!Guid.TryParseExact(Path.GetFileNameWithoutExtension(path), "D", out var recordId))
                continue;

            // The ID inside the file must not lead to another file than the one it was read from:
            if (await this.ReadJsonAsync<ModelComparisonRecord>(path, token) is { } record)
                yield return record with { Id = recordId };
        }
    }

    /// <summary>
    /// Deletes all stored comparisons, the presets aside.
    /// </summary>
    /// <remarks>
    /// Unlike reading, deleting takes the files this version cannot read along: the user asked for all results to go.
    /// </remarks>
    /// <param name="token">The cancellation token.</param>
    public async Task DeleteAllResultsAsync(CancellationToken token = default)
    {
        await this.InitializeAsync(token);
        if (!Directory.Exists(this.ResultsDirectory))
            return;

        foreach (var path in Directory.EnumerateFiles(this.ResultsDirectory, "*.json").ToList())
        {
            token.ThrowIfCancellationRequested();
            if (!Guid.TryParseExact(Path.GetFileNameWithoutExtension(path), "D", out var recordId))
                continue;

            var gate = this.GetLock(recordId);
            await gate.WaitAsync(token);
            try
            {
                File.Delete(path);
            }
            finally
            {
                gate.Release();
                this.ForgetLock(recordId);
            }
        }
    }
}