namespace AIStudio.Tools.Services.Indexing;

/// <summary>
/// How much work one indexing run does at most, before it leaves the rest to another run.
/// </summary>
/// <remarks>
/// Only the work on documents counts, cf. DocumentRunProgress.HasDoneItsShare: the clock starts right
/// before the first one. Whatever a kind of data source does before, e.g. listing the files of a
/// folder, every run has to do anew, and counting it would leave a large folder little time for its
/// documents.
/// </remarks>
/// <param name="MaxDuration">How long a run works on documents at most. More than zero.</param>
/// <param name="MaxDocuments">How many documents a run reads at most, whether that works or not. Null when only the time counts.</param>
internal sealed record RunShare(TimeSpan MaxDuration, int? MaxDocuments = null)
{
    /// <summary>
    /// How long a run works on documents, unless its kind of data source has a reason for another time.
    /// </summary>
    public static readonly TimeSpan DEFAULT_DURATION = TimeSpan.FromMinutes(5);
}