namespace AIStudio.Tools.Services;

public sealed partial class RustService
{
    public async Task<TDatabaseInfo> GetDatabaseInfo<TDatabaseInfo>(
        string databaseName,
        string infoPath,
        Func<string, TDatabaseInfo> unavailableFactory,
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(TimeSpan.FromSeconds(45));

            var databaseInfo = await this.http.GetFromJsonAsync<TDatabaseInfo>(infoPath, this.jsonRustSerializerOptions, cts.Token);
            return databaseInfo ?? unavailableFactory("The database information response was empty.");
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            if(this.logger is not null)
                this.logger.LogWarning("Fetching {DatabaseName} info from Rust service was cancelled by caller.", databaseName);
            else
                Console.WriteLine($"Fetching {databaseName} info from Rust service was cancelled by caller.");

            return unavailableFactory("Operation cancelled by caller.");
        }
        catch (Exception e)
        {
            if(this.logger is not null)
                this.logger.LogError(e, "Error while fetching {DatabaseName} info from Rust service.", databaseName);
            else
                Console.WriteLine($"Error while fetching {databaseName} info from Rust service: '{e}'.");

            return unavailableFactory(e.Message);
        }
    }

    public async Task ExecuteDatabaseOperation<TRequest>(string databaseName, string path, TRequest request, CancellationToken cancellationToken = default)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(TimeSpan.FromMinutes(5));

        var response = await this.http.PostAsJsonAsync(path, request, this.jsonRustSerializerOptions, cts.Token);
        response.EnsureSuccessStatusCode();

        var operation = await response.Content.ReadFromJsonAsync<DatabaseOperationResponse>(this.jsonRustSerializerOptions, cts.Token);
        if (operation is not { Success: true })
            throw new InvalidOperationException(operation?.Issue ?? $"The {databaseName} operation failed.");
    }

    private sealed record DatabaseOperationResponse(bool Success, string Issue);
}
