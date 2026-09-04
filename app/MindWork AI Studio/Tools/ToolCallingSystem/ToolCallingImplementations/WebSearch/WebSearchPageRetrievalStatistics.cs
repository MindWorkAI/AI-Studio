namespace AIStudio.Tools.ToolCallingSystem.ToolCallingImplementations.WebSearch;

internal sealed record WebSearchPageRetrievalStatistics(int AttemptedCount, int BlockedCount, int PageTimedOutCount, int FailedCount, int EmptyContentCount);