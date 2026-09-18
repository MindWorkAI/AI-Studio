using AIStudio.Tools.Databases.VectorStore;

namespace AIStudio.Tools.Services;

public sealed partial class RustService
{
    /// <summary>
    /// The issue code the Rust runtime sends when a vector store is there, but cannot be opened.
    /// </summary>
    /// <remarks>
    /// Mirrors ISSUE_CODE_STORE_UNREADABLE in runtime/src/qdrant_edge_database.rs. Reading the code
    /// rather than the message is what keeps a reworded message on the Rust side harmless here.
    /// </remarks>
    private const string ISSUE_CODE_STORE_UNREADABLE = "store-unreadable";

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

        using var response = await this.http.PostAsJsonAsync(path, request, this.jsonRustSerializerOptions, cts.Token);
        response.EnsureSuccessStatusCode();

        var operation = await response.Content.ReadFromJsonAsync<DatabaseOperationResponse>(this.jsonRustSerializerOptions, cts.Token);
        if (operation is not { Success: true })
            throw CreateDatabaseException(operation?.Issue, operation?.IssueCode, $"The {databaseName} operation failed.");
    }

    public async Task<TResult?> ExecuteDatabaseQuery<TRequest, TResult>(string databaseName, string path, TRequest request, CancellationToken cancellationToken = default)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(TimeSpan.FromMinutes(5));

        using var response = await this.http.PostAsJsonAsync(path, request, this.jsonRustSerializerOptions, cts.Token);
        response.EnsureSuccessStatusCode();

        var operation = await response.Content.ReadFromJsonAsync<DatabaseQueryResponse<TResult>>(this.jsonRustSerializerOptions, cts.Token);
        if (operation is not { Success: true })
            throw CreateDatabaseException(operation?.Issue, operation?.IssueCode, $"The {databaseName} query failed.");

        return operation.Data;
    }

    /// <summary>
    /// Turns a failed database response into the exception which fits its issue code.
    /// </summary>
    /// <remarks>
    /// Almost every failure says all it has to say in its message. A store which cannot be opened is
    /// the exception: the only way out of it is a rebuild which costs the user money and time, so it
    /// gets a type of its own and reaches the places which can offer that rebuild instead of
    /// starting it unasked.
    /// </remarks>
    private static Exception CreateDatabaseException(string? issue, string? issueCode, string fallbackMessage)
    {
        var message = string.IsNullOrWhiteSpace(issue) ? fallbackMessage : issue;
        return issueCode switch
        {
            ISSUE_CODE_STORE_UNREADABLE => new VectorStoreUnreadableException(message),
            _ => new InvalidOperationException(message),
        };
    }

    private sealed record DatabaseOperationResponse(bool Success, string Issue, string IssueCode);

    private sealed record DatabaseQueryResponse<TResult>(bool Success, string Issue, string IssueCode, TResult? Data);
}
