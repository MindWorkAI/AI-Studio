using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

using AIStudio.Provider;
using AIStudio.Settings;
using AIStudio.Settings.DataModel;
using AIStudio.Tools.Databases.EmbeddingState;
using AIStudio.Tools.PluginSystem;
using AIStudio.Tools.Rust;

namespace AIStudio.Tools.Services;

public sealed partial class DataSourceEmbeddingService
{
    private const string OFFICE_LOCK_FILE_PREFIX = "~$";
    private const int DEFAULT_CHUNK_OVERLAP_TOKEN_LENGTH = 300;

    private static readonly string[] RAG_DELIMITED_TABLE_FILE_EXTENSIONS = ["csv", "tsv"];
    private static readonly string[] RAG_SPREADSHEET_FILE_EXTENSIONS = ["ods", "xlsm", "xlsb"];
    private static readonly string[] RAG_SPREADSHEET_ADD_IN_FILE_EXTENSIONS = ["xla", "xlam"];
    private static readonly string[] SKIPPED_RAG_FILE_EXTENSIONS = ["lnk"];

    private enum RagFileIndexingDecision
    {
        INDEXABLE,
        EXCLUDED,
        UNSUPPORTED,
    }

    private sealed record ExtractedFileSegment(string Text, int? TokenCount);

    private sealed record ExtractedFileContent(string Text, IReadOnlyList<ExtractedFileSegment> SourceSegments);

    private sealed record EmbeddingChunkDraft(string ChunkId, string Text, int ChunkIndex, int? PageNumber);

    private sealed record ChunkingOptions(int MaxChunkTokenLength, int OverlapTokenLength);

    private sealed record ChunkingStrategy(string Name, IReadOnlyList<ChunkingRule> Rules);

    private sealed record ChunkingRule(string Name, Func<string, IReadOnlyList<string>, IReadOnlyList<string>>? Split, bool UsesSourceSegmentCounts = false);

    private sealed record DataSourceMetadataSnapshot(string SourceHash, IReadOnlyDictionary<string, string> FileHashes);

    private async IAsyncEnumerable<string> StreamEmbeddingChunksAsync(string filePath, IDataSource dataSource, EmbeddingProvider embeddingProvider, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken token)
    {
        var options = this.GetChunkingOptions(dataSource, embeddingProvider);
        var strategy = this.GetChunkingStrategy(filePath);
        ExtractedFileContent content;

        if (this.IsImageFilePath(filePath))
        {
            var imageIndexText = this.BuildImageIndexText(filePath);
            content = new(imageIndexText, [new(imageIndexText, null)]);
        }
        else
        {
            content = await this.ReadExtractedFileContentAsync(filePath, embeddingProvider, token);
        }

        await foreach (var chunk in this.SplitByChunkingStrategyAsync(content, strategy, options, embeddingProvider, token))
            yield return chunk;
    }

    private async Task<ExtractedFileContent> ReadExtractedFileContentAsync(string filePath, EmbeddingProvider embeddingProvider, CancellationToken token)
    {
        var segments = new List<ExtractedFileSegment>();

        await foreach (var segment in rustService.StreamArbitraryFileDataWithTokenCounts(filePath, embeddingProvider, token))
        {
            var normalized = NormalizeChunkSegment(segment.Content);
            if (!string.IsNullOrWhiteSpace(normalized))
                segments.Add(new(normalized, segment.TokenCount));
        }

        return new(string.Join("\n", segments.Select(segment => segment.Text)).Trim(), segments);
    }

    private async IAsyncEnumerable<string> SplitByChunkingStrategyAsync(ExtractedFileContent content, ChunkingStrategy strategy, ChunkingOptions options, EmbeddingProvider embeddingProvider, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken token)
    {
        var estimatedTokenCount = SumTokenCounts(content.SourceSegments);
        await foreach (var chunk in this.SplitTextByRulesAsync(content.Text, content.SourceSegments, strategy, 0, options, embeddingProvider, token, estimatedTokenCount: estimatedTokenCount))
            yield return chunk;
    }

    private async IAsyncEnumerable<string> SplitTextByRulesAsync(
        string text,
        IReadOnlyList<ExtractedFileSegment> sourceSegments,
        ChunkingStrategy strategy,
        int ruleIndex,
        ChunkingOptions options,
        EmbeddingProvider embeddingProvider,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken token,
        string requiredOverlapPrefix = "",
        int? estimatedTokenCount = null)
    {
        text = text.Trim();
        if (string.IsNullOrWhiteSpace(text))
            yield break;

        var tokenCount = estimatedTokenCount;
        var textWithOverlap = AddOverlapPrefix(text, requiredOverlapPrefix);
        if (textWithOverlap.Length <= RustService.MAX_TOKEN_COUNT_REQUEST_TEXT_LENGTH &&
            (estimatedTokenCount is null || estimatedTokenCount <= options.MaxChunkTokenLength))
        {
            tokenCount = await this.GetEmbeddingTokenCountAsync(embeddingProvider, textWithOverlap, token);
            if (tokenCount <= options.MaxChunkTokenLength)
            {
                yield return textWithOverlap;
                yield break;
            }
        }

        if (ruleIndex >= strategy.Rules.Count)
        {
            await foreach (var hardChunk in this.SplitTextByHardCutAsync(text, options, embeddingProvider, token, requiredOverlapPrefix, estimatedTokenCount))
                yield return hardChunk;

            yield break;
        }

        var rule = strategy.Rules[ruleIndex];
        if (rule.Split is null)
        {
            await foreach (var hardChunk in this.SplitTextByHardCutAsync(text, options, embeddingProvider, token, requiredOverlapPrefix, estimatedTokenCount))
                yield return hardChunk;

            yield break;
        }

        var units = NormalizeSplitUnits(rule.Split(text, sourceSegments.Select(segment => segment.Text).ToList()), text);
        if (units.Count <= 1)
        {
            await foreach (var chunk in this.SplitTextByRulesAsync(text, sourceSegments, strategy, ruleIndex + 1, options, embeddingProvider, token, requiredOverlapPrefix, estimatedTokenCount))
                yield return chunk;

            yield break;
        }

        logger.LogDebug(
            "Splitting content for embedding provider '{EmbeddingProviderName}' with strategy '{ChunkingStrategy}' and rule '{ChunkingRule}'. EstimatedTokenCount={EstimatedTokenCount}, MaxChunkTokenLength={MaxChunkTokenLength}, OverlapTokenLength={OverlapTokenLength}.",
            embeddingProvider.Name,
            strategy.Name,
            rule.Name,
            tokenCount,
            options.MaxChunkTokenLength,
            options.OverlapTokenLength);

        var index = 0;
        var overlapPrefix = requiredOverlapPrefix;
        var unitTokenCounts = EstimateSplitUnitTokenCounts(units, sourceSegments, rule.UsesSourceSegmentCounts, estimatedTokenCount);

        while (index < units.Count)
        {
            token.ThrowIfCancellationRequested();

            var unitCount = await this.FindLargestUnitCountWithinMaxChunkLengthAsync(units, unitTokenCounts, index, embeddingProvider, options.MaxChunkTokenLength, token, overlapPrefix);
            if (unitCount > 0)
            {
                var rawChunk = string.Concat(units.Skip(index).Take(unitCount)).Trim();
                var chunk = AddOverlapPrefix(rawChunk, overlapPrefix);
                overlapPrefix = string.Empty;
                if (!string.IsNullOrWhiteSpace(chunk))
                    yield return chunk;

                var nextIndex = index + unitCount;
                if (nextIndex >= units.Count)
                    yield break;

                var nextStartIndex = await this.CalculateNextStartIndexAsync(units, index, nextIndex, options, embeddingProvider, token);
                if (nextStartIndex < nextIndex)
                {
                    logger.LogDebug(
                        "Applied delimiter overlap while chunking. Strategy='{ChunkingStrategy}', Rule='{ChunkingRule}', PreviousStartUnitIndex={PreviousStartUnitIndex}, PreviousEndUnitIndex={PreviousEndUnitIndex}, NextStartUnitIndex={NextStartUnitIndex}, OverlapUnits={OverlapUnits}, OverlapTokenLength={OverlapTokenLength}.",
                        strategy.Name,
                        rule.Name,
                        index,
                        nextIndex,
                        nextStartIndex,
                        nextIndex - nextStartIndex,
                        options.OverlapTokenLength);

                    index = nextStartIndex;
                }
                else
                {
                    overlapPrefix = await this.CreateOverlapPrefixAsync(chunk, strategy, rule, options, embeddingProvider, token);
                    index = nextIndex;
                }

                continue;
            }

            string? lastSplitUnit = null;
            var unitTokenCount = unitTokenCounts?[index];
            await foreach (var splitUnit in this.SplitTextByRulesAsync(units[index], [new(units[index], unitTokenCount)], strategy, ruleIndex + 1, options, embeddingProvider, token, overlapPrefix, unitTokenCount))
            {
                lastSplitUnit = splitUnit;
                yield return splitUnit;
            }

            overlapPrefix = lastSplitUnit is null
                ? string.Empty
                : await this.CreateOverlapPrefixAsync(lastSplitUnit, strategy, rule, options, embeddingProvider, token);
            index++;
        }
    }

    private async Task<int> FindLargestUnitCountWithinMaxChunkLengthAsync(IReadOnlyList<string> units, IReadOnlyList<int>? estimatedUnitTokenCounts, int startUnitIndex, EmbeddingProvider embeddingProvider, int maxChunkTokenLength, CancellationToken token, string overlapPrefix = "")
    {
        var minimumCandidateUnitCount = 1;
        var availableUnitCount = units.Count - startUnitIndex;
        var maximumCandidateUnitCount = availableUnitCount;
        var largestValidUnitCount = 0;

        if (estimatedUnitTokenCounts is not null)
        {
            maximumCandidateUnitCount = 0;
            var cumulativeEstimatedTokenCount = 0L;
            for (var unitIndex = startUnitIndex; unitIndex < units.Count; unitIndex++)
            {
                cumulativeEstimatedTokenCount += estimatedUnitTokenCounts[unitIndex];
                if (cumulativeEstimatedTokenCount > maxChunkTokenLength)
                    break;

                maximumCandidateUnitCount++;
            }

            if (maximumCandidateUnitCount == 0)
                maximumCandidateUnitCount = 1;
        }

        while (true)
        {
            var searchedMaximumCandidateUnitCount = maximumCandidateUnitCount;
            while (minimumCandidateUnitCount <= maximumCandidateUnitCount)
            {
                token.ThrowIfCancellationRequested();

                var candidateUnitCount = minimumCandidateUnitCount + (maximumCandidateUnitCount - minimumCandidateUnitCount) / 2;
                var candidateText = AddOverlapPrefix(string.Concat(units.Skip(startUnitIndex).Take(candidateUnitCount)).Trim(), overlapPrefix);
                var candidateFits = candidateText.Length <= RustService.MAX_TOKEN_COUNT_REQUEST_TEXT_LENGTH &&
                                    await this.GetEmbeddingTokenCountAsync(embeddingProvider, candidateText, token) <= maxChunkTokenLength;
                if (candidateFits)
                {
                    largestValidUnitCount = candidateUnitCount;
                    minimumCandidateUnitCount = candidateUnitCount + 1;
                }
                else
                    maximumCandidateUnitCount = candidateUnitCount - 1;
            }

            if (largestValidUnitCount < searchedMaximumCandidateUnitCount || largestValidUnitCount >= availableUnitCount)
                break;

            minimumCandidateUnitCount = searchedMaximumCandidateUnitCount + 1;
            maximumCandidateUnitCount = (int)Math.Min(
                availableUnitCount,
                Math.Max((long)minimumCandidateUnitCount, (long)searchedMaximumCandidateUnitCount * 2));
        }

        return largestValidUnitCount;
    }

    private static int? SumTokenCounts(IReadOnlyList<ExtractedFileSegment> segments)
    {
        var result = 0L;
        foreach (var segment in segments)
        {
            if (segment.TokenCount is null)
                return null;

            result += segment.TokenCount.Value;
        }

        return (int)Math.Min(result, int.MaxValue);
    }

    private static IReadOnlyList<int>? EstimateSplitUnitTokenCounts(
        IReadOnlyList<string> units,
        IReadOnlyList<ExtractedFileSegment> sourceSegments,
        bool usesSourceSegmentCounts,
        int? sourceTokenCount)
    {
        if (usesSourceSegmentCounts && sourceSegments.Count == units.Count && sourceSegments.All(segment => segment.TokenCount is not null))
            return sourceSegments.Select(segment => segment.TokenCount.GetValueOrDefault()).ToList();

        if (sourceTokenCount is null)
            return null;

        var totalLength = Math.Max(1, units.Sum(unit => unit.Length));
        var result = new List<int>(units.Count);
        var allocatedTokenCount = 0;
        var consumedLength = 0L;

        foreach (var unit in units)
        {
            consumedLength += unit.Length;
            var tokenCountAtBoundary = (int)Math.Min(sourceTokenCount.Value, (long)sourceTokenCount.Value * consumedLength / totalLength);
            result.Add(Math.Max(0, tokenCountAtBoundary - allocatedTokenCount));
            allocatedTokenCount = tokenCountAtBoundary;
        }

        if (result.Count > 0 && allocatedTokenCount < sourceTokenCount.Value)
            result[^1] += sourceTokenCount.Value - allocatedTokenCount;

        return result;
    }

    private async Task<string> CreateOverlapPrefixAsync(string chunk, ChunkingStrategy strategy, ChunkingRule rule, ChunkingOptions options, EmbeddingProvider embeddingProvider, CancellationToken token)
    {
        return await this.CreateOverlapPrefixAsync(chunk, strategy.Name, rule.Name, options, embeddingProvider, token);
    }

    private async Task<string> CreateOverlapPrefixAsync(string chunk, string strategyName, string ruleName, ChunkingOptions options, EmbeddingProvider embeddingProvider, CancellationToken token)
    {
        if (options.OverlapTokenLength <= 0)
            return string.Empty;

        chunk = chunk.Trim();
        if (string.IsNullOrWhiteSpace(chunk))
            return string.Empty;

        var chunkTokenCount = await this.GetEmbeddingTokenCountAsync(embeddingProvider, chunk, token);
        if (chunkTokenCount <= options.OverlapTokenLength)
        {
            logger.LogDebug(
                "Applied whole-chunk overlap while chunking because the previous chunk is smaller than the requested overlap. Strategy='{ChunkingStrategy}', Rule='{ChunkingRule}', RequestedOverlapTokenLength={RequestedOverlapTokenLength}, ActualOverlapTokenCount={ActualOverlapTokenCount}.",
                strategyName,
                ruleName,
                options.OverlapTokenLength,
                chunkTokenCount);

            return chunk;
        }

        var overlapStartIndex = await this.CalculateHardCutOverlapStartIndexAsync(chunk, 0, chunk.Length, options, embeddingProvider, token);
        if (overlapStartIndex >= chunk.Length)
            overlapStartIndex = FindLastNonWhitespaceStartIndex(chunk);

        if (overlapStartIndex >= chunk.Length)
            return string.Empty;

        var overlapPrefix = chunk[overlapStartIndex..].Trim();
        if (string.IsNullOrWhiteSpace(overlapPrefix))
            return string.Empty;

        var tokenCount = await this.GetEmbeddingTokenCountAsync(embeddingProvider, overlapPrefix, token);
        logger.LogDebug(
            "Applied hard-cut overlap while chunking because delimiter overlap was not available. Strategy='{ChunkingStrategy}', Rule='{ChunkingRule}', RequestedOverlapTokenLength={RequestedOverlapTokenLength}, ActualOverlapTokenCount={ActualOverlapTokenCount}.",
            strategyName,
            ruleName,
            options.OverlapTokenLength,
            tokenCount);

        return overlapPrefix;
    }

    private async Task<int> CalculateNextStartIndexAsync(IReadOnlyList<string> units, int chunkStartIndex, int chunkEndIndex, ChunkingOptions options, EmbeddingProvider embeddingProvider, CancellationToken token)
    {
        if (options.OverlapTokenLength <= 0)
            return chunkEndIndex;

        var bestStartIndex = chunkEndIndex;
        var bestDistance = int.MaxValue;

        for (var candidateStartIndex = chunkEndIndex - 1; candidateStartIndex > chunkStartIndex; candidateStartIndex--)
        {
            token.ThrowIfCancellationRequested();

            var candidate = string.Concat(units.Skip(candidateStartIndex).Take(chunkEndIndex - candidateStartIndex)).Trim();
            if (string.IsNullOrWhiteSpace(candidate))
                continue;

            var tokenCount = await this.GetEmbeddingTokenCountAsync(embeddingProvider, candidate, token);
            var distance = Math.Abs(tokenCount - options.OverlapTokenLength);
            if (distance < bestDistance)
            {
                bestStartIndex = candidateStartIndex;
                bestDistance = distance;
            }

            if (tokenCount >= options.OverlapTokenLength && bestStartIndex < chunkEndIndex)
                break;
        }

        return bestStartIndex <= chunkStartIndex ? chunkEndIndex : bestStartIndex;
    }

    private async IAsyncEnumerable<string> SplitTextByHardCutAsync(
        string text,
        ChunkingOptions options,
        EmbeddingProvider embeddingProvider,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken token,
        string requiredOverlapPrefix = "",
        int? estimatedTokenCount = null)
    {
        text = text.Trim();
        var startIndex = 0;
        var overlapPrefix = requiredOverlapPrefix;
        while (startIndex < text.Length)
        {
            token.ThrowIfCancellationRequested();

            while (startIndex < text.Length && char.IsWhiteSpace(text[startIndex]))
                startIndex++;

            if (startIndex >= text.Length)
                yield break;

            var bestEndIndex = startIndex;
            var maximumCandidateEndIndex = Math.Min(text.Length, startIndex + RustService.MAX_TOKEN_COUNT_REQUEST_TEXT_LENGTH);

            if (estimatedTokenCount > options.MaxChunkTokenLength)
            {
                var estimatedChunkLength = Math.Max(1L, (long)text.Length * options.MaxChunkTokenLength / estimatedTokenCount.Value);
                maximumCandidateEndIndex = (int)Math.Min(text.Length, startIndex + estimatedChunkLength);
            }

            while (true)
            {
                var minimumCandidateEndIndex = bestEndIndex + 1;
                var currentMaximumCandidateEndIndex = maximumCandidateEndIndex;
                while (minimumCandidateEndIndex <= currentMaximumCandidateEndIndex)
                {
                    var candidateEndIndex = minimumCandidateEndIndex + (currentMaximumCandidateEndIndex - minimumCandidateEndIndex) / 2;
                    var candidate = AddOverlapPrefix(text[startIndex..candidateEndIndex].Trim(), overlapPrefix);
                    var candidateFits = candidate.Length <= RustService.MAX_TOKEN_COUNT_REQUEST_TEXT_LENGTH &&
                                        await this.GetEmbeddingTokenCountAsync(embeddingProvider, candidate, token) <= options.MaxChunkTokenLength;
                    if (candidateFits)
                    {
                        bestEndIndex = candidateEndIndex;
                        minimumCandidateEndIndex = candidateEndIndex + 1;
                    }
                    else
                        currentMaximumCandidateEndIndex = candidateEndIndex - 1;
                }

                if (bestEndIndex < maximumCandidateEndIndex || bestEndIndex >= text.Length ||
                    maximumCandidateEndIndex - startIndex >= RustService.MAX_TOKEN_COUNT_REQUEST_TEXT_LENGTH)
                    break;

                var previousCandidateLength = maximumCandidateEndIndex - startIndex;
                maximumCandidateEndIndex = (int)Math.Min(
                    Math.Min(text.Length, startIndex + (long)RustService.MAX_TOKEN_COUNT_REQUEST_TEXT_LENGTH),
                    startIndex + Math.Max(previousCandidateLength + 1L, previousCandidateLength * 2L));
            }

            if (bestEndIndex == startIndex)
            {
                if (!string.IsNullOrWhiteSpace(overlapPrefix))
                {
                    var smallestOverlapPrefix = GetSmallestOverlapPrefix(overlapPrefix);
                    if (!string.IsNullOrWhiteSpace(smallestOverlapPrefix) && !string.Equals(smallestOverlapPrefix, overlapPrefix, StringComparison.Ordinal))
                    {
                        logger.LogDebug(
                            "Reduced hard-cut overlap because the configured overlap leaves no room for new content. RequestedOverlapTokenLength={RequestedOverlapTokenLength}, MaxChunkTokenLength={MaxChunkTokenLength}.",
                            options.OverlapTokenLength,
                            options.MaxChunkTokenLength);

                        overlapPrefix = smallestOverlapPrefix;
                        continue;
                    }
                }

                var smallestCandidate = AddOverlapPrefix(text[startIndex..Math.Min(startIndex + 1, text.Length)].Trim(), overlapPrefix);
                var smallestCandidateTokenCount = await this.GetEmbeddingTokenCountAsync(embeddingProvider, smallestCandidate, token);
                throw new InvalidOperationException($"The max chunk length for embedding provider '{embeddingProvider.Name}' is too low. The smallest possible split still has {smallestCandidateTokenCount} tokens, but the configured limit is {options.MaxChunkTokenLength}.");
            }

            var chunk = AddOverlapPrefix(text[startIndex..bestEndIndex].Trim(), overlapPrefix);
            overlapPrefix = string.Empty;
            if (!string.IsNullOrWhiteSpace(chunk))
                yield return chunk;

            if (bestEndIndex >= text.Length)
                yield break;

            overlapPrefix = await this.CreateOverlapPrefixAsync(chunk, "hard-cut", "Hard cut", options, embeddingProvider, token);
            startIndex = bestEndIndex;
        }
    }

    private async Task<int> CalculateHardCutOverlapStartIndexAsync(string text, int chunkStartIndex, int chunkEndIndex, ChunkingOptions options, EmbeddingProvider embeddingProvider, CancellationToken token)
    {
        if (options.OverlapTokenLength <= 0 || chunkEndIndex - chunkStartIndex <= 1)
            return chunkEndIndex;

        var low = chunkStartIndex + 1;
        var high = chunkEndIndex - 1;
        var bestStartIndex = chunkEndIndex;

        while (low <= high)
        {
            token.ThrowIfCancellationRequested();

            var mid = low + (high - low) / 2;
            var candidate = text[mid..chunkEndIndex].Trim();
            var tokenCount = await this.GetEmbeddingTokenCountAsync(embeddingProvider, candidate, token);
            if (tokenCount <= options.OverlapTokenLength)
            {
                bestStartIndex = mid;
                high = mid - 1;
            }
            else
                low = mid + 1;
        }

        return bestStartIndex <= chunkStartIndex ? chunkEndIndex : bestStartIndex;
    }

    private static int FindLastNonWhitespaceStartIndex(string text)
    {
        for (var index = text.Length - 1; index >= 0; index--)
        {
            if (!char.IsWhiteSpace(text[index]))
                return index;
        }

        return text.Length;
    }

    private static string GetSmallestOverlapPrefix(string text)
    {
        var index = FindLastNonWhitespaceStartIndex(text);
        return index >= text.Length ? string.Empty : text[index..].Trim();
    }

    private static string AddOverlapPrefix(string chunk, string overlapPrefix)
    {
        if (string.IsNullOrWhiteSpace(overlapPrefix))
            return chunk.Trim();

        return $"{overlapPrefix.TrimEnd()}\n{chunk.TrimStart()}".Trim();
    }

    private async Task<int> GetEmbeddingTokenCountAsync(EmbeddingProvider embeddingProvider, string text, CancellationToken token)
    {
        var response = await rustService.GetTokenCount(embeddingProvider, text, token);
        if (response is { Success: true })
            return response.Value.TokenCount;

        var message = response?.Message ?? "No response was returned by the tokenizer service.";
        throw new InvalidOperationException($"Could not count tokens for embedding provider '{embeddingProvider.Name}'. {message}");
    }

    private ChunkingOptions GetChunkingOptions(IDataSource dataSource, EmbeddingProvider embeddingProvider)
    {
        var providerMaxChunkTokenLength = Math.Max(1, embeddingProvider.EffectiveTokenLimit);
        var dataSourceMaxChunkTokenLength = dataSource is IInternalDataSource { MaxChunkTokenLength: > 0 } internalDataSource
            ? internalDataSource.MaxChunkTokenLength
            : 0;
        var maxChunkTokenLength = dataSourceMaxChunkTokenLength > 0
            ? Math.Min(dataSourceMaxChunkTokenLength, providerMaxChunkTokenLength)
            : providerMaxChunkTokenLength;

        var configuredOverlapTokenLength = dataSource is IInternalDataSource overlapDataSource
            ? overlapDataSource.ChunkOverlapTokenLength
            : 0;
        var requestedOverlapTokenLength = configuredOverlapTokenLength > 0
            ? configuredOverlapTokenLength
            : DEFAULT_CHUNK_OVERLAP_TOKEN_LENGTH;
        var overlapTokenLength = Math.Clamp(requestedOverlapTokenLength, 0, Math.Max(0, maxChunkTokenLength - 1));

        return new(maxChunkTokenLength, overlapTokenLength);
    }

    private ChunkingStrategy GetChunkingStrategy(string filePath)
    {
        if (this.IsImageFilePath(filePath))
            return new("image", [
                new("Whitespace", SplitByWhitespace),
                new("Hard cut", null),
            ]);

        if (this.IsPresentationFilePath(filePath))
            return new("presentation", [
                new("Slide", SplitBySourceSegments, true),
                new("Line break", SplitByLineBreaks),
                new("Whitespace", SplitByWhitespace),
                new("Hard cut", null),
            ]);

        if (this.IsDelimitedTableFilePath(filePath) || this.IsSpreadsheetFilePath(filePath))
            return new("table", [
                new("Row or sheet", SplitBySourceSegments, true),
                new("Line break", SplitByLineBreaks),
                new("Whitespace", SplitByWhitespace),
                new("Hard cut", null),
            ]);

        if (this.IsSourceCodeFilePath(filePath))
            return GetSourceCodeChunkingStrategy(filePath);

        return new("document", [
            new("Page or extracted section", SplitBySourceSegments, true),
            new("Heading", SplitByDocumentHeadings),
            new("Paragraph", SplitByParagraphs),
            new("Line break", SplitByLineBreaks),
            new("Whitespace", SplitByWhitespace),
            new("Hard cut", null),
        ]);
    }

    private static ChunkingStrategy GetSourceCodeChunkingStrategy(string filePath)
    {
        var rules = new List<ChunkingRule>
        {
            new("Extracted section", SplitBySourceSegments, true),
        };
        rules.AddRange(GetSourceCodeDelimiterRules(filePath));
        rules.Add(new("Line break", SplitByLineBreaks));
        rules.Add(new("Whitespace", SplitByWhitespace));
        rules.Add(new("Hard cut", null));

        return new("source-code", rules);
    }

    private static IReadOnlyList<ChunkingRule> GetSourceCodeDelimiterRules(string filePath) => Path.GetExtension(filePath).TrimStart('.') switch
    {
        _ => [],
    };

    private static List<string> NormalizeSplitUnits(IReadOnlyList<string> units, string fallbackText)
    {
        var result = units
            .Where(unit => !string.IsNullOrWhiteSpace(unit))
            .ToList();

        return result.Count == 0 ? [fallbackText] : result;
    }

    private static IReadOnlyList<string> SplitBySourceSegments(string text, IReadOnlyList<string> sourceSegments)
    {
        return sourceSegments.Count > 1
            ? sourceSegments.Select(segment => segment + "\n").ToList()
            : [text];
    }

    private static IReadOnlyList<string> SplitByDocumentHeadings(string text, IReadOnlyList<string> sourceSegments)
    {
        var lines = ReadLines(text);
        if (lines.Count < 2)
            return [text];

        var result = new List<string>();
        var segmentStart = 0;

        for (var i = 0; i < lines.Count; i++)
        {
            var (lineStart, _, lineText) = lines[i];
            if (lineStart == 0)
                continue;

            var previousLine = i > 0 ? lines[i - 1].Text : string.Empty;
            var nextLine = i + 1 < lines.Count ? lines[i + 1].Text : string.Empty;
            if (!IsDocumentHeadingLine(lineText, previousLine, nextLine))
                continue;

            result.Add(text[segmentStart..lineStart]);
            segmentStart = lineStart;
        }

        if (segmentStart == 0)
            return [text];

        result.Add(text[segmentStart..]);
        return result;
    }

    private static IReadOnlyList<string> SplitByParagraphs(string text, IReadOnlyList<string> sourceSegments)
    {
        var matches = Regex.Matches(text, @"\n[ \t]*\n", RegexOptions.CultureInvariant);
        if (matches.Count == 0)
            return [text];

        var result = new List<string>();
        var start = 0;
        foreach (Match match in matches)
        {
            var end = match.Index + match.Length;
            result.Add(text[start..end]);
            start = end;
        }

        if (start < text.Length)
            result.Add(text[start..]);

        return result;
    }

    private static IReadOnlyList<string> SplitByLineBreaks(string text, IReadOnlyList<string> sourceSegments)
    {
        var result = new List<string>();
        var start = 0;

        for (var i = 0; i < text.Length; i++)
        {
            if (text[i] != '\n')
                continue;

            result.Add(text[start..(i + 1)]);
            start = i + 1;
        }

        if (start < text.Length)
            result.Add(text[start..]);

        return result.Count == 0 ? [text] : result;
    }

    private static IReadOnlyList<string> SplitByWhitespace(string text, IReadOnlyList<string> sourceSegments)
    {
        var matches = Regex.Matches(text, @"\S+\s*", RegexOptions.CultureInvariant);
        if (matches.Count == 0)
            return [text];

        return matches.Cast<Match>().Select(match => match.Value).ToList();
    }

    private static List<(int Start, int End, string Text)> ReadLines(string text)
    {
        var result = new List<(int Start, int End, string Text)>();
        var start = 0;

        for (var i = 0; i < text.Length; i++)
        {
            if (text[i] != '\n')
                continue;

            result.Add((start, i + 1, text[start..(i + 1)]));
            start = i + 1;
        }

        if (start < text.Length)
            result.Add((start, text.Length, text[start..]));

        return result;
    }

    private static bool IsDocumentHeadingLine(string line, string previousLine, string nextLine)
    {
        var trimmed = line.Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
            return false;

        if (Regex.IsMatch(trimmed, @"^#{1,6}\s+\S", RegexOptions.CultureInvariant))
            return true;

        if (!string.IsNullOrWhiteSpace(previousLine) || !string.IsNullOrWhiteSpace(nextLine))
            return false;

        if (trimmed.Length is < 3 or > 120)
            return false;

        if (trimmed.Contains("|", StringComparison.Ordinal) || trimmed.EndsWith(".", StringComparison.Ordinal))
            return false;

        return Regex.IsMatch(trimmed, @"^(\d+(\.\d+)*\.?\s+\S|(?i:chapter|section)\s+\S|[A-Z0-9][A-Z0-9 ,:;'/&()_-]{2,})$", RegexOptions.CultureInvariant);
    }

    private FileEnumerationResult GetInputFiles(IDataSource dataSource)
    {
        var result = new FileEnumerationResult();

        switch (dataSource)
        {
            case DataSourceLocalFile localFile when File.Exists(localFile.FilePath):
                var file = new FileInfo(localFile.FilePath);
                switch (this.GetRagFileIndexingDecision(file))
                {
                    case RagFileIndexingDecision.INDEXABLE:
                        result.Files.Add(file);
                        break;

                    case RagFileIndexingDecision.EXCLUDED:
                        logger.LogDebug("Skipping excluded file '{FilePath}' while indexing.", file.FullName);
                        break;

                    default:
                        result.AddFailure(localFile.FilePath, $"The selected file '{localFile.FilePath}' is not supported for background embeddings.");
                        break;
                }

                return result;

            case DataSourceLocalDirectory localDirectory when Directory.Exists(localDirectory.Path):
                this.EnumerateAccessibleFiles(localDirectory.Path, result);
                return result;
        }

        switch (dataSource)
        {
            case DataSourceLocalFile localFile:
                result.AddFailure(localFile.FilePath, $"The selected file '{localFile.FilePath}' does not exist.");
                break;

            case DataSourceLocalDirectory localDirectory:
                result.AddFailure(localDirectory.Path, $"The selected directory '{localDirectory.Path}' does not exist.");
                break;
        }

        return result;
    }

    private void EnumerateAccessibleFiles(string rootPath, FileEnumerationResult result)
    {
        var pendingDirectories = new Stack<string>();
        pendingDirectories.Push(rootPath);

        while (pendingDirectories.Count > 0)
        {
            var currentPath = pendingDirectories.Pop();
            IEnumerable<string> subDirectories;
            IEnumerable<string> files;

            try
            {
                subDirectories = Directory.EnumerateDirectories(currentPath);
                files = Directory.EnumerateFiles(currentPath);
            }
            catch (Exception exception)
            {
                logger.LogWarning(exception, "Cannot access directory '{DirectoryPath}' while indexing.", currentPath);
                result.AddFailure(currentPath, $"The directory '{currentPath}' could not be accessed.");
                continue;
            }

            foreach (var filePath in files)
            {
                FileInfo fileInfo;
                try
                {
                    fileInfo = new FileInfo(filePath);
                    if (!fileInfo.Exists)
                        continue;
                }
                catch (Exception exception)
                {
                    logger.LogWarning(exception, "Cannot inspect file '{FilePath}' while indexing.", filePath);
                    result.AddFailure(filePath, $"The file '{filePath}' could not be inspected.");
                    continue;
                }

                switch (this.GetRagFileIndexingDecision(fileInfo))
                {
                    case RagFileIndexingDecision.INDEXABLE:
                        result.Files.Add(fileInfo);
                        break;

                    case RagFileIndexingDecision.EXCLUDED:
                        logger.LogDebug("Skipping excluded file '{FilePath}' while indexing.", fileInfo.FullName);
                        break;
                }
            }

            foreach (var subDirectory in subDirectories)
            {
                if (this.IsSkippedRagDirectory(subDirectory))
                    continue;

                pendingDirectories.Push(subDirectory);
            }
        }
    }

    private string TryGetRelativePath(IDataSource dataSource, FileInfo file) => dataSource switch
    {
        DataSourceLocalDirectory localDirectory => Path.GetRelativePath(localDirectory.Path, file.FullName),
        _ => file.Name
    };

    private static string NormalizeChunkSegment(string input)
    {
        return input
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n')
            .Trim();
    }

    private bool IsImageFilePath(string filePath)
    {
        return FileTypes.IsAllowedPath(filePath, FileTypes.IMAGE);
    }

    private bool IsPresentationFilePath(string filePath)
    {
        return FileTypes.IsAllowedPath(filePath, FileTypes.POWER_POINT);
    }

    private bool IsDelimitedTableFilePath(string filePath)
    {
        var extension = Path.GetExtension(filePath).TrimStart('.');
        return RAG_DELIMITED_TABLE_FILE_EXTENSIONS.Contains(extension, StringComparer.OrdinalIgnoreCase);
    }

    private bool IsSpreadsheetFilePath(string filePath)
    {
        var extension = Path.GetExtension(filePath).TrimStart('.');
        return FileTypes.IsAllowedPath(filePath, FileTypes.EXCEL)
               || RAG_SPREADSHEET_FILE_EXTENSIONS.Contains(extension, StringComparer.OrdinalIgnoreCase)
               || RAG_SPREADSHEET_ADD_IN_FILE_EXTENSIONS.Contains(extension, StringComparer.OrdinalIgnoreCase);
    }

    private bool IsSourceCodeFilePath(string filePath)
    {
        return !this.IsHtmlFilePath(filePath) && FileTypes.IsAllowedPath(filePath, FileTypes.SOURCE_CODE);
    }

    private bool IsHtmlFilePath(string filePath)
    {
        var extension = Path.GetExtension(filePath).TrimStart('.');
        return extension.Equals("html", StringComparison.OrdinalIgnoreCase)
               || extension.Equals("htm", StringComparison.OrdinalIgnoreCase);
    }

    private bool IsSupportedRagFilePath(string filePath)
    {
        var extension = Path.GetExtension(filePath).TrimStart('.');
        return FileTypes.IsAllowedPath(filePath, FileTypes.DOCUMENT, FileTypes.IMAGE)
               || RAG_DELIMITED_TABLE_FILE_EXTENSIONS.Contains(extension, StringComparer.OrdinalIgnoreCase)
               || RAG_SPREADSHEET_FILE_EXTENSIONS.Contains(extension, StringComparer.OrdinalIgnoreCase)
               || RAG_SPREADSHEET_ADD_IN_FILE_EXTENSIONS.Contains(extension, StringComparer.OrdinalIgnoreCase);
    }

    private RagFileIndexingDecision GetRagFileIndexingDecision(FileInfo file)
    {
        if (this.IsSkippedRagFile(file))
            return RagFileIndexingDecision.EXCLUDED;

        return this.IsSupportedRagFilePath(file.FullName)
            ? RagFileIndexingDecision.INDEXABLE
            : RagFileIndexingDecision.UNSUPPORTED;
    }

    private bool IsSkippedRagFile(FileInfo file)
    {
        if (IsSkippedRagFileName(file.Name))
            return true;

        try
        {
            return file.Attributes.HasFlag(FileAttributes.ReparsePoint)
                   || file.Attributes.HasFlag(FileAttributes.Offline)
                   || file.Attributes.HasFlag(FileAttributes.Temporary)
                   || file.Attributes.HasFlag(FileAttributes.System);
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Cannot inspect file '{FilePath}' while indexing.", file.FullName);
            return true;
        }
    }

    private static bool IsSkippedRagFileName(string fileName)
    {
        var extension = Path.GetExtension(fileName).TrimStart('.');
        return SKIPPED_RAG_FILE_EXTENSIONS.Contains(extension, StringComparer.OrdinalIgnoreCase)
               || fileName.StartsWith(OFFICE_LOCK_FILE_PREFIX, StringComparison.Ordinal);
    }

    private bool IsSkippedRagDirectory(string path)
    {
        try
        {
            var directory = new DirectoryInfo(path);
            return directory.Attributes.HasFlag(FileAttributes.ReparsePoint)
                   || directory.Attributes.HasFlag(FileAttributes.Offline)
                   || directory.Attributes.HasFlag(FileAttributes.System);
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Cannot inspect directory '{DirectoryPath}' while indexing.", path);
            return true;
        }
    }

    private string BuildImageIndexText(string filePath)
    {
        var fileName = Path.GetFileName(filePath);
        var fileNameWithoutExtension = Path.GetFileNameWithoutExtension(filePath);
        var extension = Path.GetExtension(filePath).TrimStart('.');
        var normalizedName = fileNameWithoutExtension
            .Replace('_', ' ')
            .Replace('-', ' ')
            .Trim();

        return $$"""
                 Image asset
                 File name: {{fileName}}
                 Type: {{extension}}
                 Search terms: {{normalizedName}}
                 Path: {{filePath}}
                 Note: The current RAG embedding pipeline stores image files by metadata only. Visual content is not OCRed or captioned yet.
                 """;
    }

    private string BuildEmbeddingSignature(IDataSource dataSource, EmbeddingProvider embeddingProvider, ChunkingOptions chunkingOptions)
    {
        return string.Join('|',
            embeddingProvider.Id,
            embeddingProvider.UsedLLMProvider,
            embeddingProvider.Model.Id,
            embeddingProvider.Host,
            embeddingProvider.Hostname,
            embeddingProvider.TokenizerPath,
            embeddingProvider.EffectiveTokenLimit,
            GetDataSourceComplianceLevel(dataSource).ToString(),
            dataSource is IInternalDataSource internalDataSource ? internalDataSource.MaxChunkTokenLength : 0,
            dataSource is IInternalDataSource overlapDataSource ? overlapDataSource.ChunkOverlapTokenLength : 0,
            chunkingOptions.MaxChunkTokenLength,
            chunkingOptions.OverlapTokenLength);
    }

    private DataSourceMetadataSnapshot BuildDataSourceMetadataSnapshot(IDataSource dataSource, IReadOnlyList<FileInfo> indexedFiles)
    {
        var fileHashes = indexedFiles
            .OrderBy(file => file.FullName, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(file => file.FullName, BuildFileMetadataHash, StringComparer.OrdinalIgnoreCase);

        var sourceHash = dataSource switch
        {
            DataSourceLocalFile localFile => indexedFiles.Count > 0
                ? fileHashes[indexedFiles[0].FullName]
                : BuildMetadataHash("file", localFile.FilePath, Path.GetFileName(localFile.FilePath) ?? string.Empty, "missing", "0"),

            DataSourceLocalDirectory localDirectory => this.BuildDirectoryMetadataHash(localDirectory, indexedFiles, fileHashes),

            _ => BuildMetadataHash(dataSource.Type.ToString(), dataSource.Id, dataSource.Name)
        };

        return new(sourceHash, fileHashes);
    }

    private string BuildDirectoryMetadataHash(DataSourceLocalDirectory dataSource, IReadOnlyList<FileInfo> indexedFiles, IReadOnlyDictionary<string, string> fileHashes)
    {
        var directory = new DirectoryInfo(dataSource.Path);
        directory.Refresh();

        var totalSize = 0L;
        var latestFileWriteTicks = 0L;
        foreach (var file in indexedFiles)
        {
            file.Refresh();
            if (!file.Exists)
                continue;

            totalSize += file.Length;
            latestFileWriteTicks = Math.Max(latestFileWriteTicks, file.LastWriteTimeUtc.Ticks);
        }

        var latestWriteTicks = Math.Max(directory.LastWriteTimeUtc.Ticks, latestFileWriteTicks);
        var parts = new List<string>
        {
            "directory",
            directory.FullName,
            directory.Name,
            latestWriteTicks.ToString(),
            totalSize.ToString(),
            indexedFiles.Count.ToString()
        };

        foreach (var file in indexedFiles.OrderBy(file => file.FullName, StringComparer.OrdinalIgnoreCase))
        {
            parts.Add(this.TryGetRelativePath(dataSource, file));
            parts.Add(fileHashes[file.FullName]);
        }

        return BuildMetadataHash(parts);
    }

    private static string BuildFileMetadataHash(FileInfo file)
    {
        file.Refresh();
        if (!file.Exists)
        {
            return BuildMetadataHash(
                "file",
                file.FullName,
                file.Name,
                "missing",
                "0");
        }

        return BuildMetadataHash(
            "file",
            file.FullName,
            file.Name,
            file.LastWriteTimeUtc.Ticks.ToString(),
            file.Length.ToString());
    }

    private static string BuildMetadataHash(params string[] parts)
    {
        return BuildMetadataHash((IEnumerable<string>)parts);
    }

    private static string BuildMetadataHash(IEnumerable<string> parts)
    {
        var fingerprintSource = new StringBuilder();
        foreach (var part in parts)
            fingerprintSource.Append(part.Length).Append(':').Append(part).Append('|');

        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(fingerprintSource.ToString()));
        return Convert.ToHexString(bytes);
    }

    private EmbeddingStateFile CreateEmbeddingStateFile(IDataSource dataSource, FileInfo file, string fingerprint, int chunkCount, DateTimeOffset embeddedAtUtc)
    {
        file.Refresh();
        var absolutePath = Path.GetFullPath(file.FullName);
        var complianceLevel = GetDataSourceComplianceLevel(dataSource);
        return new(
            this.CreateParentFileId(dataSource.Id, absolutePath),
            absolutePath,
            file.Name,
            this.TryGetRelativePath(dataSource, file),
            GetFileType(file),
            fingerprint,
            file.Exists ? file.Length : 0,
            file.Exists ? new DateTimeOffset(file.CreationTimeUtc) : DateTimeOffset.UnixEpoch,
            file.Exists ? new DateTimeOffset(file.LastWriteTimeUtc) : DateTimeOffset.UnixEpoch,
            embeddedAtUtc,
            chunkCount,
            complianceLevel.ToString(),
            (int)complianceLevel);
    }

    private IReadOnlyList<EmbeddingStateChunk> CreateEmbeddingStateChunks(EmbeddingStateFile parentFile, IReadOnlyList<EmbeddingChunkDraft> batch, DateTimeOffset embeddedAtUtc)
    {
        return batch
            .Select(chunk => new EmbeddingStateChunk(
                chunk.ChunkId,
                parentFile.ParentFileId,
                chunk.PageNumber,
                chunk.ChunkIndex,
                chunk.Text,
                embeddedAtUtc))
            .ToList();
    }

    private static ConfidenceLevel GetDataSourceComplianceLevel(IDataSource dataSource) =>
        dataSource.ComplianceLevel is ConfidenceLevel.NONE
            ? ConfidenceLevel.UNKNOWN
            : dataSource.ComplianceLevel;

    private static string GetFileType(FileInfo file)
    {
        var extension = file.Extension.TrimStart('.').ToLowerInvariant();
        return string.IsNullOrWhiteSpace(extension) ? "unknown" : extension;
    }

    private static int? TryExtractPageNumber(string chunk)
    {
        var match = Regex.Match(chunk, @"^\s*#\s+Page\s+(\d+)\b", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
        return match.Success && int.TryParse(match.Groups[1].Value, out var pageNumber) && pageNumber > 0
            ? pageNumber
            : null;
    }

    private string GetCollectionName(string dataSourceName, string dataSourceId) =>
        DataSourceEmbeddingNames.GetCollectionName(dataSourceName, dataSourceId);

    private string CreatePointId(string dataSourceId, string fingerprint, int chunkIndex) =>
        CreateStableGuid($"{dataSourceId}:chunk:{fingerprint}:{chunkIndex}");

    private string CreateParentFileId(string dataSourceId, string absolutePath) =>
        CreateStableGuid($"{dataSourceId}:parent-file:{absolutePath}");

    private static string CreateStableGuid(string source)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(source));
        var guidBytes = hash[..16].ToArray();

        guidBytes[6] = (byte)((guidBytes[6] & 0x0F) | 0x40);
        guidBytes[8] = (byte)((guidBytes[8] & 0x3F) | 0x80);

        return new Guid(guidBytes).ToString();
    }
}
