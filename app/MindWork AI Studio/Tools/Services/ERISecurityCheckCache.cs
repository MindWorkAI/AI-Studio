using AIStudio.Settings.DataModel;
using AIStudio.Tools.ERIClient.DataModel;

namespace AIStudio.Tools.Services;

/// <summary>
/// Keeps what the servers of ERI data sources answered to the check of their security requirements.
/// </summary>
/// <remarks>
/// One message asks this several times: Semantic Search checks the data sources when it is
/// offered and again before every search, and the classic RAG process checks them with every
/// message. Each check signs in, and a server which limits sign-ins or sessions would start
/// refusing. A check which is still running is joined rather than started a second time.<br/><br/>
/// The server's operator may change the requirements at any time, so an answer is kept for one
/// minute only. That covers the rounds of one answer, while a change still takes effect soon. A
/// server which did not answer is asked again sooner: it may well be back, and until then, its
/// data source is not searched anyway. A changed configuration, e.g., another server or another
/// account, asks anew right away.<br/><br/>
/// Whenever a check starts, the expired ones are dropped, so that data sources which were deleted
/// or changed do not pile up.
/// </remarks>
/// <param name="fetch">Signs in to the server of a data source and asks for its requirements; true when the server answered.</param>
/// <param name="timeProvider">Tells the time the checks are started at.</param>
internal sealed class ERISecurityCheckCache(Func<DataSourceERI_V1, Task<(bool IsReachable, SecurityRequirements? Requirements)>> fetch, TimeProvider timeProvider)
{
    /// <summary>
    /// How long the answer of an ERI server to the check of its security requirements is used.
    /// </summary>
    private static readonly TimeSpan ANSWER_LIFETIME = TimeSpan.FromMinutes(1);

    /// <summary>
    /// How long an ERI server which did not answer the check is not asked again, counted from the start of the check.
    /// </summary>
    private static readonly TimeSpan NO_ANSWER_LIFETIME = TimeSpan.FromSeconds(15);

    /// <summary>
    /// A check of an ERI server, which may still be running, together with the configuration it was started with.
    /// </summary>
    private readonly record struct SecurityCheck(DataSourceERI_V1 DataSource, Task<(bool IsReachable, SecurityRequirements? Requirements)> Result, DateTimeOffset StartedAt);

    private readonly Dictionary<string, SecurityCheck> checks = new(StringComparer.Ordinal);

    /// <summary>
    /// What the server of an ERI data source requires, asked anew only once the last answer expired.
    /// </summary>
    /// <param name="source">The ERI data source.</param>
    /// <returns>Whether the server answered, and its requirements when it did.</returns>
    public Task<(bool IsReachable, SecurityRequirements? Requirements)> GetAsync(DataSourceERI_V1 source)
    {
        lock (this.checks)
        {
            var now = timeProvider.GetUtcNow();
            if (this.checks.TryGetValue(source.Id, out var known) && known.DataSource.Equals(source) && IsValid(known.Result, now - known.StartedAt))
                return known.Result;

            this.RemoveExpired(now);
            var check = new SecurityCheck(source, fetch(source), now);
            this.checks[source.Id] = check;
            return check.Result;
        }
    }

    /// <summary>
    /// How many checks are kept right now, expired ones included.
    /// </summary>
    internal int Count
    {
        get
        {
            lock (this.checks)
                return this.checks.Count;
        }
    }

    /// <summary>
    /// Whether a check of an ERI server may still be used instead of asking the server again.
    /// </summary>
    /// <param name="result">The check, which may still be running.</param>
    /// <param name="age">How long ago the check was started.</param>
    /// <returns>True when the check is still running or its answer has not expired yet.</returns>
    internal static bool IsValid(Task<(bool IsReachable, SecurityRequirements? Requirements)> result, TimeSpan age)
    {
        if (!result.IsCompleted)
            return true;

        if (!result.IsCompletedSuccessfully)
            return false;

        return age < (result.Result.IsReachable ? ANSWER_LIFETIME : NO_ANSWER_LIFETIME);
    }

    private void RemoveExpired(DateTimeOffset now)
    {
        var expiredIds = this.checks
            .Where(pair => !IsValid(pair.Value.Result, now - pair.Value.StartedAt))
            .Select(pair => pair.Key)
            .ToList();

        foreach (var id in expiredIds)
            this.checks.Remove(id);
    }
}