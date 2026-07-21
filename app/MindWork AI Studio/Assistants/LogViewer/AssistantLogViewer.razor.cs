using System.Globalization;
using System.Net;
using System.Text;

using AIStudio.Components;
using AIStudio.Tools.Rust;
using AIStudio.Tools.Services;

using Microsoft.AspNetCore.Components;

namespace AIStudio.Assistants.LogViewer;

public partial class AssistantLogViewer : MSGComponentBase
{
    private static readonly TimeSpan AUTO_REFRESH_INTERVAL = TimeSpan.FromSeconds(5);
    private static readonly char[] WORD_SPLIT_CHARS = [' ', '\t', '\r', '\n'];
    private static readonly Dictionary<string, int> LOG_LEVEL_ORDER = new(StringComparer.OrdinalIgnoreCase)
    {
        ["ERROR"] = 0,
        ["CRITICAL"] = 1,
        ["WARN"] = 2,
        ["WARNING"] = 3,
        ["INFO"] = 4,
        ["INFORMATION"] = 5,
        ["DEBUG"] = 6,
        ["TRACE"] = 7,
    };

    private const int DEFAULT_MAX_LINES = 5000;
    protected const int MIN_MAX_LINES = 100;
    protected const int MAX_MAX_LINES = 100000;
    private const string OTHER_OPTION_VALUE = "__OTHER__";

    [Inject]
    private RustService RustService { get; init; } = null!;

    [Inject]
    private ISnackbar Snackbar { get; init; } = null!;

    [Inject]
    private NavigationManager NavigationManager { get; init; } = null!;

    [Inject]
    private ILogger<AssistantLogViewer> Logger { get; init; } = null!;

    private GetLogPathsResponse logPaths;
    private LogFileKind selectedLogFile = LogFileKind.APP;
    private List<LogLine> loadedLines = [];
    private List<LogLine> displayLines = [];
    private List<string> logLevelOptions = [OTHER_OPTION_VALUE];
    private List<string> loggerOptions = [OTHER_OPTION_VALUE];
    private List<string> sourceDetailOptions = [OTHER_OPTION_VALUE];
    private HashSet<string> selectedLogLevels = new(StringComparer.OrdinalIgnoreCase);
    private HashSet<string> selectedLoggers = new(StringComparer.OrdinalIgnoreCase);
    private HashSet<string> selectedSourceDetails = new(StringComparer.OrdinalIgnoreCase);
    private string[] activeSearchTerms = [];
    private CancellationTokenSource? autoRefreshCancellationTokenSource;
    private string filterText = string.Empty;
    private string loadError = string.Empty;
    private bool isLoading;
    private bool autoRefresh;
    private bool filterOnly = true;
    private bool invertFilters;
    private bool showTimestamps = true;
    private int maxLines = DEFAULT_MAX_LINES;
    private int totalLineCount;
    private int skippedLineCount;
    private DateTimeOffset? lastLoadedAt;

    private string CurrentLogPath => this.selectedLogFile is LogFileKind.APP ? this.logPaths.LogAppPath : this.logPaths.LogStartupPath;

    private bool CanOpenCurrentLogPath => !string.IsNullOrWhiteSpace(this.CurrentLogPath);

    private bool HasDropdownFilter => this.selectedLogLevels.Count > 0 || this.selectedLoggers.Count > 0 || this.selectedSourceDetails.Count > 0;

    private bool HasActiveFilter => !string.IsNullOrWhiteSpace(this.filterText) || this.HasDropdownFilter || this.invertFilters;

    private bool HasActiveVisibilityFilter => this.HasDropdownFilter || (this.filterOnly && this.activeSearchTerms.Length > 0);

    private bool CanCopyDisplayedLines => this.displayLines.Count > 0;

    private string FilterText
    {
        get => this.filterText;
        set
        {
            if (this.filterText == value)
                return;

            this.filterText = value;
            this.RefreshDisplayLines();
        }
    }

    private bool FilterOnly
    {
        get => this.filterOnly;
        set
        {
            if (this.filterOnly == value)
                return;

            this.filterOnly = value;
            this.RefreshDisplayLines();
        }
    }

    private bool InvertFilters
    {
        get => this.invertFilters;
        set
        {
            if (this.invertFilters == value)
                return;

            this.invertFilters = value;
            this.RefreshDisplayLines();
        }
    }

    private bool ShowTimestamps
    {
        get => this.showTimestamps;
        set
        {
            if (this.showTimestamps == value)
                return;

            this.showTimestamps = value;
            this.RefreshDisplayLines();
        }
    }

    private string StatusText
    {
        get
        {
            if (this.isLoading)
                return T("Loading...");

            var visibleLineCount = this.displayLines.Count.ToString("N0", CultureInfo.CurrentCulture);
            var loadedLineCount = this.loadedLines.Count.ToString("N0", CultureInfo.CurrentCulture);
            var totalLineCountText = this.totalLineCount.ToString("N0", CultureInfo.CurrentCulture);
            var lastLoadedText = this.lastLoadedAt?.LocalDateTime.ToString("g", CultureInfo.CurrentCulture) ?? T("not loaded yet");

            if (this.loadedLines.Count == 0)
                return string.Format(T("Loaded {0} lines. Last refresh: {1}."), loadedLineCount, lastLoadedText);

            if (this.skippedLineCount > 0)
            {
                var skippedLineCountText = this.skippedLineCount.ToString("N0", CultureInfo.CurrentCulture);
                return string.Format(T("Showing {0} of {1} loaded lines. {2} older lines were skipped. Last refresh: {3}."), visibleLineCount, loadedLineCount, skippedLineCountText, lastLoadedText);
            }

            return string.Format(T("Showing {0} of {1} lines. Last refresh: {2}."), visibleLineCount, totalLineCountText, lastLoadedText);
        }
    }

    protected override async Task OnInitializedAsync()
    {
        await base.OnInitializedAsync();

        if (!this.SettingsManager.IsAssistantVisible(Tools.Components.LOG_VIEWER_ASSISTANT, assistantName: T("Log Viewer")))
        {
            this.NavigationManager.NavigateTo(Routes.ASSISTANTS);
            return;
        }

        await this.RefreshLogAsync();
    }

    protected override void DisposeResources()
    {
        this.StopAutoRefresh();
    }

    private async Task SelectedLogFileChanged(LogFileKind value)
    {
        if (this.selectedLogFile == value)
            return;

        this.selectedLogFile = value;
        await this.RefreshLogAsync();
    }

    private Task SelectedLogLevelsChanged(IEnumerable<string?>? selectedValues)
    {
        UpdateSelectedValues(this.selectedLogLevels, selectedValues);
        this.RefreshDisplayLines();
        return Task.CompletedTask;
    }

    private Task SelectedLoggersChanged(IEnumerable<string?>? selectedValues)
    {
        UpdateSelectedValues(this.selectedLoggers, selectedValues);
        this.RefreshDisplayLines();
        return Task.CompletedTask;
    }

    private Task SelectedSourceDetailsChanged(IEnumerable<string?>? selectedValues)
    {
        UpdateSelectedValues(this.selectedSourceDetails, selectedValues);
        this.RefreshDisplayLines();
        return Task.CompletedTask;
    }

    private async Task AutoRefreshChanged(bool value)
    {
        this.autoRefresh = value;

        if (this.autoRefresh)
            this.StartAutoRefresh();
        else
            this.StopAutoRefresh();

        await Task.CompletedTask;
    }

    private async Task MaxLinesChanged(int value)
    {
        var normalizedValue = Math.Clamp(value, MIN_MAX_LINES, MAX_MAX_LINES);
        if (this.maxLines == normalizedValue)
            return;

        this.maxLines = normalizedValue;
        await this.RefreshLogAsync();
    }

    private async Task CopyDisplayedLines()
    {
        var text = string.Join(Environment.NewLine, this.displayLines.Select(this.GetPlainRenderedLine));
        await this.RustService.CopyText2Clipboard(this.Snackbar, text);
    }

    private async Task OpenCurrentLogInFileManager()
    {
        try
        {
            this.logPaths = await this.RustService.GetLogPaths();
        }
        catch (Exception e)
        {
            this.Logger.LogWarning(e, "Could not refresh the log file paths before opening the file manager.");
            this.Snackbar.Add(T("The log file path is not available yet."), Severity.Warning, config =>
            {
                config.Icon = Icons.Material.Filled.Folder;
                config.IconSize = Size.Large;
            });
            return;
        }

        var path = this.CurrentLogPath;
        if (string.IsNullOrWhiteSpace(path))
        {
            this.Snackbar.Add(T("The log file path is not available yet."), Severity.Warning, config =>
            {
                config.Icon = Icons.Material.Filled.Folder;
                config.IconSize = Size.Large;
            });
            return;
        }

        OpenPathResponse response;
        try
        {
            response = await this.RustService.OpenPathInFileManager(path);
        }
        catch (Exception e)
        {
            this.Logger.LogWarning(e, "Could not open the log file location in the file manager.");
            this.Snackbar.Add(T("Could not open the log file location."), Severity.Error, config =>
            {
                config.Icon = Icons.Material.Filled.Folder;
                config.IconSize = Size.Large;
            });
            return;
        }

        if (response.Success)
        {
            this.Snackbar.Add(T("Opened the log file location."), Severity.Success, config =>
            {
                config.Icon = Icons.Material.Filled.FolderOpen;
                config.IconSize = Size.Large;
            });
            return;
        }

        var issue = string.IsNullOrWhiteSpace(response.Issue) ? T("Unknown error") : response.Issue;
        this.Snackbar.Add(string.Format(T("Could not open the log file location: {0}"), issue), Severity.Error, config =>
        {
            config.Icon = Icons.Material.Filled.Folder;
            config.IconSize = Size.Large;
        });
    }

    private void ClearFilters()
    {
        this.filterText = string.Empty;
        this.selectedLogLevels.Clear();
        this.selectedLoggers.Clear();
        this.selectedSourceDetails.Clear();
        this.invertFilters = false;
        this.RefreshDisplayLines();
    }

    private async Task RefreshLogAsync()
    {
        if (this.isLoading)
            return;

        this.isLoading = true;
        this.loadError = string.Empty;
        await this.InvokeAsync(this.StateHasChanged);

        try
        {
            this.logPaths = await this.RustService.GetLogPaths();
            var path = this.CurrentLogPath;

            if (string.IsNullOrWhiteSpace(path))
            {
                this.loadedLines = [];
                this.totalLineCount = 0;
                this.skippedLineCount = 0;
                this.lastLoadedAt = null;
                this.loadError = T("The log file path is not available yet.");
                return;
            }

            if (!File.Exists(path))
            {
                this.loadedLines = [];
                this.totalLineCount = 0;
                this.skippedLineCount = 0;
                this.lastLoadedAt = null;
                this.loadError = string.Format(T("The log file does not exist: {0}"), path);
                return;
            }

            var snapshot = await ReadLogSnapshotAsync(path, this.maxLines);
            this.loadedLines = snapshot.Lines;
            this.totalLineCount = snapshot.TotalLineCount;
            this.skippedLineCount = snapshot.SkippedLineCount;
            this.lastLoadedAt = DateTimeOffset.Now;
        }
        catch (Exception e)
        {
            this.Logger.LogWarning(e, "Could not read the log file for the log viewer assistant.");
            this.loadedLines = [];
            this.totalLineCount = 0;
            this.skippedLineCount = 0;
            this.lastLoadedAt = null;
            this.loadError = string.Format(T("The log file could not be read: {0}"), e.Message);
        }
        finally
        {
            this.isLoading = false;
            this.RebuildFilterOptions();
            this.RefreshDisplayLines();
            await this.InvokeAsync(this.StateHasChanged);
        }
    }

    private static async Task<LogSnapshot> ReadLogSnapshotAsync(string path, int maxLines)
    {
        var queue = new Queue<string>(Math.Min(maxLines, 4096));
        var totalLineCount = 0;
        var skippedLineCount = 0;

        await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete, 65536, true);
        using var reader = new StreamReader(stream, Encoding.UTF8, true);

        while (await reader.ReadLineAsync() is { } line)
        {
            totalLineCount++;
            queue.Enqueue(line);

            if (queue.Count <= maxLines)
                continue;

            queue.Dequeue();
            skippedLineCount++;
        }

        var firstLineNumber = skippedLineCount + 1;
        var lines = queue
            .Select((line, index) => new LogLine(firstLineNumber + index, line, ParseLogSegments(line)))
            .ToList();

        return new(lines, totalLineCount, skippedLineCount);
    }

    private void RebuildFilterOptions()
    {
        this.logLevelOptions = BuildFilterOptions(this.loadedLines.Select(line => line.Segments.Level), CompareLogLevels);
        this.loggerOptions = BuildFilterOptions(this.loadedLines.Select(line => line.Segments.Logger), (left, right) => StringComparer.OrdinalIgnoreCase.Compare(left, right));
        this.sourceDetailOptions = BuildFilterOptions(this.loadedLines.Select(line => line.Segments.SourceDetails), (left, right) => StringComparer.OrdinalIgnoreCase.Compare(left, right));

        NormalizeSelectedValues(this.selectedLogLevels, this.logLevelOptions);
        NormalizeSelectedValues(this.selectedLoggers, this.loggerOptions);
        NormalizeSelectedValues(this.selectedSourceDetails, this.sourceDetailOptions);
    }

    private void RefreshDisplayLines()
    {
        this.activeSearchTerms = BuildSearchTerms(this.filterText);
        this.displayLines = this.loadedLines
            .Where(this.LineMatchesFilters)
            .ToList();
    }

    private bool LineMatchesFilters(LogLine line)
    {
        if (this.invertFilters && this.HasActiveVisibilityFilter)
            return !this.LineMatchesAnyActiveFilter(line);

        return this.LineMatchesActiveFilters(line);
    }

    private bool LineMatchesActiveFilters(LogLine line)
    {
        if (!MatchesSelection(line.Segments.Level, this.selectedLogLevels))
            return false;

        if (!MatchesSelection(line.Segments.Logger, this.selectedLoggers))
            return false;

        if (!MatchesSelection(line.Segments.SourceDetails, this.selectedSourceDetails))
            return false;

        if (!this.filterOnly || this.activeSearchTerms.Length == 0)
            return true;

        return MatchesSearchTerms(this.GetPlainRenderedLine(line), this.activeSearchTerms);
    }

    private bool LineMatchesAnyActiveFilter(LogLine line)
    {
        if (ActiveSelectionMatches(line.Segments.Level, this.selectedLogLevels))
            return true;

        if (ActiveSelectionMatches(line.Segments.Logger, this.selectedLoggers))
            return true;

        if (ActiveSelectionMatches(line.Segments.SourceDetails, this.selectedSourceDetails))
            return true;

        return this.filterOnly && this.activeSearchTerms.Length > 0 && MatchesSearchTerms(this.GetPlainRenderedLine(line), this.activeSearchTerms);
    }

    private string RenderLine(LogLine line)
    {
        var text = this.GetPlainRenderedLine(line);
        var ranges = new List<HighlightRange>();
        AddSearchTermRanges(text, this.activeSearchTerms, ranges);

        if (ranges.Count == 0)
            return WebUtility.HtmlEncode(text);

        ranges = MergeRanges(ranges);
        var sb = new StringBuilder();
        var position = 0;

        foreach (var range in ranges)
        {
            AppendEncoded(sb, text, position, range.Start - position);
            sb.Append("""<mark class="log-viewer-highlight">""");
            AppendEncoded(sb, text, range.Start, range.Length);
            sb.Append("</mark>");
            position = range.Start + range.Length;
        }

        AppendEncoded(sb, text, position, text.Length - position);
        return sb.ToString();
    }

    private string GetPlainRenderedLine(LogLine line)
    {
        var parts = new List<string>();
        var segments = line.Segments;

        if (this.showTimestamps && !string.IsNullOrWhiteSpace(segments.Timestamp))
            parts.Add(segments.Timestamp);

        if (!ShouldHideSelectedSegment(segments.Level, this.selectedLogLevels))
            AddIfNotWhiteSpace(parts, segments.Level);

        if (!ShouldHideSelectedSegment(segments.Logger, this.selectedLoggers))
            AddIfNotWhiteSpace(parts, segments.Logger);

        if (!ShouldHideSelectedSegment(segments.SourceDetails, this.selectedSourceDetails))
            AddIfNotWhiteSpace(parts, segments.SourceDetails);

        AddIfNotWhiteSpace(parts, segments.Message);

        return parts.Count == 0 ? string.Empty : string.Join(" ", parts);
    }

    private string GetLineClass(LogLine line)
    {
        var level = line.Segments.Level ?? string.Empty;

        if (level.Contains("ERROR", StringComparison.OrdinalIgnoreCase) || level.Contains("CRITICAL", StringComparison.OrdinalIgnoreCase))
            return "log-viewer-line log-viewer-line-error";

        if (level.Contains("WARN", StringComparison.OrdinalIgnoreCase))
            return "log-viewer-line log-viewer-line-warn";

        if (level.Contains("DEBUG", StringComparison.OrdinalIgnoreCase))
            return "log-viewer-line log-viewer-line-debug";

        if (level.Contains("TRACE", StringComparison.OrdinalIgnoreCase))
            return "log-viewer-line log-viewer-line-trace";

        return "log-viewer-line";
    }

    private string GetFilterOptionDisplay(string value)
    {
        return value == OTHER_OPTION_VALUE ? T("Other") : value;
    }

    private string GetMultiSelectionText(List<string?>? selectedValues)
    {
        if (selectedValues is null || selectedValues.Count == 0)
            return T("All");

        var selectedLabels = selectedValues
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => this.GetFilterOptionDisplay(value!))
            .ToList();

        return selectedLabels.Count == 0 ? T("All") : string.Join(", ", selectedLabels);
    }

    private void StartAutoRefresh()
    {
        this.StopAutoRefresh();
        this.autoRefreshCancellationTokenSource = new CancellationTokenSource();
        _ = this.AutoRefreshLoopAsync(this.autoRefreshCancellationTokenSource.Token);
    }

    private void StopAutoRefresh()
    {
        this.autoRefreshCancellationTokenSource?.Cancel();
        this.autoRefreshCancellationTokenSource?.Dispose();
        this.autoRefreshCancellationTokenSource = null;
    }

    private async Task AutoRefreshLoopAsync(CancellationToken token)
    {
        try
        {
            using var timer = new PeriodicTimer(AUTO_REFRESH_INTERVAL);
            while (await timer.WaitForNextTickAsync(token))
                await this.InvokeAsync(this.RefreshLogAsync);
        }
        catch (OperationCanceledException)
        {
        }
    }

    private static LogSegments ParseLogSegments(string line)
    {
        var index = 0;
        var parsedAnySegment = false;
        string? timestamp = null;
        string? level = null;
        string? logger = null;
        string? sourceDetails = null;

        if (TryReadBracket(line, index, out var bracket, out var content, out var nextIndex) && IsTimestamp(content))
        {
            timestamp = bracket;
            index = nextIndex;
            parsedAnySegment = true;
        }

        var candidateIndex = SkipWhiteSpace(line, index);
        if (TryReadLogLevel(line, candidateIndex, out var detectedLevel, out nextIndex))
        {
            level = detectedLevel;
            index = nextIndex;
            parsedAnySegment = true;
        }

        candidateIndex = SkipWhiteSpace(line, index);
        if (TryReadBracket(line, candidateIndex, out bracket, out content, out nextIndex))
        {
            if (IsSourceDetails(content))
            {
                sourceDetails = bracket;
                index = nextIndex;
                parsedAnySegment = true;
            }
            else
            {
                logger = bracket;
                index = nextIndex;
                parsedAnySegment = true;

                candidateIndex = SkipWhiteSpace(line, index);
                if (TryReadBracket(line, candidateIndex, out bracket, out content, out nextIndex) && IsSourceDetails(content))
                {
                    sourceDetails = bracket;
                    index = nextIndex;
                    parsedAnySegment = true;
                }
            }
        }

        var message = parsedAnySegment ? ReadMessage(line, index) : line;
        return new(timestamp, level, logger, sourceDetails, message);
    }

    private static bool TryReadBracket(string text, int start, out string bracket, out string content, out int nextIndex)
    {
        bracket = string.Empty;
        content = string.Empty;
        nextIndex = start;

        if (start >= text.Length || text[start] != '[')
            return false;

        var end = text.IndexOf(']', start + 1);
        if (end < 0)
            return false;

        bracket = text[start..(end + 1)];
        content = text[(start + 1)..end];
        nextIndex = end + 1;
        return true;
    }

    private static bool TryReadLogLevel(string text, int start, out string level, out int nextIndex)
    {
        level = string.Empty;
        nextIndex = start;

        if (start >= text.Length || text[start] == '[')
            return false;

        var end = start;
        while (end < text.Length && !char.IsWhiteSpace(text[end]))
            end++;

        if (end == start)
            return false;

        var candidate = text[start..end];
        if (candidate.Length > 20 || candidate.Any(character => !char.IsLetter(character)))
            return false;

        var afterCandidate = SkipWhiteSpace(text, end);
        if (afterCandidate >= text.Length || text[afterCandidate] != '[')
            return false;

        level = candidate;
        nextIndex = end;
        return true;
    }

    private static bool IsTimestamp(string content)
    {
        return DateTimeOffset.TryParse(content, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out _);
    }

    private static bool IsSourceDetails(string content)
    {
        return content.Contains("=", StringComparison.Ordinal);
    }

    private static int SkipWhiteSpace(string text, int start)
    {
        var index = start;
        while (index < text.Length && char.IsWhiteSpace(text[index]))
            index++;

        return index;
    }

    private static string ReadMessage(string text, int start)
    {
        if (start >= text.Length)
            return string.Empty;

        if (char.IsWhiteSpace(text[start]))
            start++;

        return start >= text.Length ? string.Empty : text[start..];
    }

    private static List<string> BuildFilterOptions(IEnumerable<string?> values, Comparison<string> comparison)
    {
        var options = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var value in values)
        {
            if (string.IsNullOrWhiteSpace(value))
                continue;

            options.TryAdd(value, value);
        }

        var sortedOptions = options.Values.ToList();
        sortedOptions.Sort(comparison);
        sortedOptions.Add(OTHER_OPTION_VALUE);
        return sortedOptions;
    }

    private static int CompareLogLevels(string left, string right)
    {
        var leftRank = LOG_LEVEL_ORDER.GetValueOrDefault(left, int.MaxValue);
        var rightRank = LOG_LEVEL_ORDER.GetValueOrDefault(right, int.MaxValue);
        var rankComparison = leftRank.CompareTo(rightRank);
        return rankComparison != 0 ? rankComparison : StringComparer.OrdinalIgnoreCase.Compare(left, right);
    }

    private static void NormalizeSelectedValues(HashSet<string> selectedValues, List<string> options)
    {
        var validOptions = options.ToHashSet(StringComparer.OrdinalIgnoreCase);
        selectedValues.RemoveWhere(value => !validOptions.Contains(value));
    }

    private static void UpdateSelectedValues(HashSet<string> target, IEnumerable<string?>? selectedValues)
    {
        target.Clear();
        if (selectedValues is null)
            return;

        foreach (var value in selectedValues)
            if (!string.IsNullOrWhiteSpace(value))
                target.Add(value);
    }

    private static bool MatchesSelection(string? value, HashSet<string> selectedValues)
    {
        return selectedValues.Count == 0 || ActiveSelectionMatches(value, selectedValues);
    }

    private static bool ActiveSelectionMatches(string? value, HashSet<string> selectedValues)
    {
        if (selectedValues.Count == 0)
            return false;

        var normalizedValue = string.IsNullOrWhiteSpace(value) ? OTHER_OPTION_VALUE : value;
        return selectedValues.Contains(normalizedValue);
    }

    private static bool ShouldHideSelectedSegment(string? value, HashSet<string> selectedValues)
    {
        return selectedValues.Count == 1 && !string.IsNullOrWhiteSpace(value) && selectedValues.Contains(value);
    }

    private static string[] BuildSearchTerms(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return [];

        return text
            .Split(WORD_SPLIT_CHARS, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static bool MatchesSearchTerms(string text, string[] terms)
    {
        return terms.Length == 0 || terms.Any(term => text.Contains(term, StringComparison.OrdinalIgnoreCase));
    }

    private static void AddSearchTermRanges(string text, string[] terms, List<HighlightRange> ranges)
    {
        foreach (var term in terms)
            AddLiteralRanges(text, term, ranges);
    }

    private static void AddLiteralRanges(string line, string value, List<HighlightRange> ranges)
    {
        var index = 0;
        while ((index = line.IndexOf(value, index, StringComparison.OrdinalIgnoreCase)) >= 0)
        {
            ranges.Add(new(index, value.Length));
            index += value.Length;
        }
    }

    private static List<HighlightRange> MergeRanges(List<HighlightRange> ranges)
    {
        var mergedRanges = new List<HighlightRange>();
        foreach (var range in ranges.OrderBy(x => x.Start).ThenByDescending(x => x.Length))
        {
            if (mergedRanges.Count == 0)
            {
                mergedRanges.Add(range);
                continue;
            }

            var previous = mergedRanges[^1];
            var previousEnd = previous.Start + previous.Length;
            var currentEnd = range.Start + range.Length;
            if (range.Start <= previousEnd)
            {
                mergedRanges[^1] = previous with { Length = Math.Max(previousEnd, currentEnd) - previous.Start };
                continue;
            }

            mergedRanges.Add(range);
        }

        return mergedRanges;
    }

    private static void AppendEncoded(StringBuilder sb, string value, int start, int length)
    {
        if (length <= 0)
            return;

        sb.Append(WebUtility.HtmlEncode(value.Substring(start, length)));
    }

    private static void AddIfNotWhiteSpace(List<string> parts, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
            parts.Add(value);
    }

    private readonly record struct LogLine(int Number, string Text, LogSegments Segments);

    private readonly record struct LogSegments(string? Timestamp, string? Level, string? Logger, string? SourceDetails, string Message);

    private readonly record struct LogSnapshot(List<LogLine> Lines, int TotalLineCount, int SkippedLineCount);

    private readonly record struct HighlightRange(int Start, int Length);
}

public enum LogFileKind
{
    APP,
    STARTUP,
}
