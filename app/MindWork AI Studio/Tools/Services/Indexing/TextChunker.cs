using System.Text.RegularExpressions;

using AIStudio.Settings;
using AIStudio.Tools.PluginSystem;
using AIStudio.Tools.Rust;

namespace AIStudio.Tools.Services.Indexing;

/// <summary>
/// Cuts a text into chunks which fit the embedding provider, with some overlap between them.
/// </summary>
/// <remarks>
/// Knows nothing about where the text came from. Whoever reads a document hands in its text in the
/// pieces the source delivered, together with the strategy which suits that kind of text, and gets
/// the chunks back one by one. Every size is measured with the tokenizer of the embedding provider,
/// since only its count decides whether a chunk fits.
/// </remarks>
/// <param name="rustService">The runtime, which counts the tokens.</param>
/// <param name="logger">The logger of the embedding service, so the log reads the same whoever writes it.</param>
internal sealed partial class TextChunker(RustService rustService, ILogger logger)
{
    /// <summary>
    /// For documents: pages, then headings, paragraphs, lines and words.
    /// </summary>
    public static readonly ChunkingStrategy DOCUMENT_STRATEGY = new("document", [
        new("Page or extracted section", SplitBySourceSegments, true),
        new("Heading", SplitByDocumentHeadings),
        new("Paragraph", SplitByParagraphs),
        new("Line break", SplitByLineBreaks),
        new("Whitespace", SplitByWhitespace),
        new("Hard cut", null),
    ]);

    /// <summary>
    /// For presentations: slides, then lines and words.
    /// </summary>
    public static readonly ChunkingStrategy PRESENTATION_STRATEGY = new("presentation", [
        new("Slide", SplitBySourceSegments, true),
        new("Line break", SplitByLineBreaks),
        new("Whitespace", SplitByWhitespace),
        new("Hard cut", null),
    ]);

    /// <summary>
    /// For tables and spreadsheets: rows or sheets, then lines and words.
    /// </summary>
    public static readonly ChunkingStrategy TABLE_STRATEGY = new("table", [
        new("Row or sheet", SplitBySourceSegments, true),
        new("Line break", SplitByLineBreaks),
        new("Whitespace", SplitByWhitespace),
        new("Hard cut", null),
    ]);

    /// <summary>
    /// For source code: extracted sections, then lines and words.
    /// </summary>
    public static readonly ChunkingStrategy SOURCE_CODE_STRATEGY = new("source-code", [
        new("Extracted section", SplitBySourceSegments, true),
        new("Line break", SplitByLineBreaks),
        new("Whitespace", SplitByWhitespace),
        new("Hard cut", null),
    ]);

    private static string TB(string fallbackEN) => I18N.I.T(fallbackEN, typeof(TextChunker).Namespace, nameof(TextChunker));

    /// <summary>
    /// Brings the line breaks of one piece of text into the form the chunking expects.
    /// </summary>
    /// <param name="input">The piece as the source delivered it.</param>
    /// <returns>The piece with Unix line breaks and without surrounding whitespace.</returns>
    public static string NormalizeSegment(string input)
    {
        return input
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n')
            .Trim();
    }

    /// <summary>
    /// Picks the strategy which suits a file, by its type.
    /// </summary>
    /// <remarks>
    /// For every reader of files alike, be the file on disk or attached to a mail. HTML counts as a
    /// document, not as source code: the runtime turns it into text before it gets here.
    /// </remarks>
    /// <param name="filePath">The path or the name of the file.</param>
    /// <returns>The strategy to cut its text by.</returns>
    public static ChunkingStrategy GetStrategyForFile(string filePath)
    {
        if (FileTypes.IsAllowedPath(filePath, FileTypes.POWER_POINT))
            return PRESENTATION_STRATEGY;

        if (FileTypes.IsAllowedPath(filePath, FileTypes.TABULAR, FileTypes.SPREADSHEET))
            return TABLE_STRATEGY;

        if (!FileTypes.IsAllowedPath(filePath, FileTypes.HTML) && FileTypes.IsAllowedPath(filePath, FileTypes.SOURCE_CODE))
            return SOURCE_CODE_STRATEGY;

        return DOCUMENT_STRATEGY;
    }

    /// <summary>
    /// Cuts a text into chunks.
    /// </summary>
    /// <param name="content">The text, in the pieces its source delivered it in.</param>
    /// <param name="strategy">The rules to cut it by.</param>
    /// <param name="options">How large a chunk may become, and how much the next one repeats.</param>
    /// <param name="embeddingProvider">The embedding provider whose tokenizer measures the chunks.</param>
    /// <param name="token">The cancellation token.</param>
    /// <param name="heading">
    /// A line the first chunk starts with, e.g. which attachment of a mail the text comes from, or
    /// empty for none. It counts against the size of that chunk, like the overlap of every later one.
    /// </param>
    /// <returns>The chunks, in the order of the text.</returns>
    public async IAsyncEnumerable<EmbeddingChunk> SplitAsync(SegmentedText content, ChunkingStrategy strategy, ChunkingOptions options, EmbeddingProvider embeddingProvider, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken token, string heading = "")
    {
        var requiredPrefix = heading;
        foreach (var part in SplitBySheets(content))
        {
            var estimatedTokenCount = SumTokenCounts(part.SourceSegments);

            // The part starts where its first segment starts, so that is where it is until the
            // splitting reaches a segment boundary:
            var firstLocation = part.SourceSegments.Count > 0 ? part.SourceSegments[0].Location : default;
            await foreach (var chunk in this.SplitTextByRulesAsync(part.Text, part.SourceSegments, strategy, 0, options, embeddingProvider, firstLocation, token, requiredPrefix, estimatedTokenCount))
                yield return chunk;

            requiredPrefix = string.Empty;
        }
    }

    /// <summary>
    /// The parts of a text which no chunk may run across: each sheet of a spreadsheet on its own,
    /// and any other text as a whole.
    /// </summary>
    /// <remarks>
    /// Every sheet is a table of its own, with columns of its own, so a chunk which ran from one
    /// sheet into the next would mix what two tables mean. Neither does a chunk start with the
    /// overlap of another sheet. A sheet without a single row holds nothing but its heading and its
    /// fences, and is left out.
    /// </remarks>
    /// <param name="content">The text, in the pieces its source delivered it in.</param>
    /// <returns>The parts, in the order of the text.</returns>
    private static IEnumerable<SegmentedText> SplitBySheets(SegmentedText content)
    {
        if (content.SourceSegments.All(segment => segment.Location.SheetName is null))
        {
            yield return content;
            yield break;
        }

        var sheet = new List<TextSegment>();
        string? sheetName = null;
        foreach (var segment in content.SourceSegments)
        {
            if (segment.Location.SheetName is { } segmentSheetName && sheetName is not null && !string.Equals(segmentSheetName, sheetName, StringComparison.Ordinal))
            {
                if (TryCreateSheet(sheet) is { } completeSheet)
                    yield return completeSheet;

                sheet = [];
            }

            sheetName = segment.Location.SheetName ?? sheetName;
            sheet.Add(segment);
        }

        if (TryCreateSheet(sheet) is { } lastSheet)
            yield return lastSheet;
    }

    /// <summary>
    /// The text of one sheet, joined the way the whole text was, or null when the sheet has no row.
    /// </summary>
    private static SegmentedText? TryCreateSheet(List<TextSegment> segments) => segments.Any(segment => segment.Location.RowNumber is not null)
        ? new SegmentedText(string.Join("\n", segments.Select(segment => segment.Text)).Trim(), segments)
        : null;

    private async IAsyncEnumerable<EmbeddingChunk> SplitTextByRulesAsync(
        string text,
        IReadOnlyList<TextSegment> sourceSegments,
        ChunkingStrategy strategy,
        int ruleIndex,
        ChunkingOptions options,
        EmbeddingProvider embeddingProvider,
        SourceLocation currentLocation,
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
                // The whole text is one chunk, so it spans all of its segments:
                var span = Locate(sourceSegments.Select(segment => segment.Location).ToList(), currentLocation);
                yield return new(textWithOverlap, span.Start, span.End);
                yield break;
            }
        }

        if (ruleIndex >= strategy.Rules.Count)
        {
            await foreach (var hardChunk in this.SplitTextByHardCutAsync(text, options, embeddingProvider, currentLocation, token, requiredOverlapPrefix, estimatedTokenCount))
                yield return hardChunk;

            yield break;
        }

        var rule = strategy.Rules[ruleIndex];
        if (rule.Split is null)
        {
            await foreach (var hardChunk in this.SplitTextByHardCutAsync(text, options, embeddingProvider, currentLocation, token, requiredOverlapPrefix, estimatedTokenCount))
                yield return hardChunk;

            yield break;
        }

        var units = NormalizeSplitUnits(rule.Split(text, sourceSegments.Select(segment => segment.Text).ToList()), text);
        if (units.Count <= 1)
        {
            await foreach (var chunk in this.SplitTextByRulesAsync(text, sourceSegments, strategy, ruleIndex + 1, options, embeddingProvider, currentLocation, token, requiredOverlapPrefix, estimatedTokenCount))
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

        //
        // The first rule of every strategy cuts along the segments the runtime delivered, so there
        // a unit is a segment and carries that segment's location. Every later rule cuts inside a
        // single segment, where all units share the location they were handed. This is what ties a
        // chunk to its pages, rows or lines without anybody reading the text. A segment without a
        // page is on the page of the text it is part of:
        //
        var unitsAreSourceSegments = rule.UsesSourceSegmentCounts && sourceSegments.Count == units.Count;
        SourceLocation LocationOfUnit(int unitIndex) => unitsAreSourceSegments
            ? sourceSegments[unitIndex].Location with { PageNumber = sourceSegments[unitIndex].Location.PageNumber ?? currentLocation.PageNumber }
            : currentLocation;

        while (index < units.Count)
        {
            token.ThrowIfCancellationRequested();

            var unitCount = await this.FindLargestUnitCountWithinMaxChunkLengthAsync(units, unitTokenCounts, index, embeddingProvider, options.MaxChunkTokenLength, token, overlapPrefix);
            if (unitCount > 0)
            {
                var rawChunk = string.Concat(units.Skip(index).Take(unitCount)).Trim();
                var chunk = AddOverlapPrefix(rawChunk, overlapPrefix);
                overlapPrefix = string.Empty;

                //
                // From the first unit this chunk covers, not from the overlap prefix in front of
                // it: the prefix repeats what the chunk before already said, while the location has
                // to name where this chunk's own content begins. To its last unit.
                //
                if (!string.IsNullOrWhiteSpace(chunk))
                {
                    var span = Locate(Enumerable.Range(index, unitCount).Select(LocationOfUnit).ToList(), currentLocation);
                    yield return new(chunk, span.Start, span.End);
                }

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
            var unitLocation = LocationOfUnit(index);
            await foreach (var splitUnit in this.SplitTextByRulesAsync(units[index], [new(units[index], unitTokenCount, unitLocation)], strategy, ruleIndex + 1, options, embeddingProvider, unitLocation, token, overlapPrefix, unitTokenCount))
            {
                lastSplitUnit = splitUnit.Text;
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
                Math.Max(minimumCandidateUnitCount, (long)searchedMaximumCandidateUnitCount * 2));
        }

        return largestValidUnitCount;
    }

    private static int? SumTokenCounts(IReadOnlyList<TextSegment> segments)
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
        IReadOnlyList<TextSegment> sourceSegments,
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
            var tokenCountAtBoundary = (int)Math.Min(sourceTokenCount.Value, sourceTokenCount.Value * consumedLength / totalLength);
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

    /// <summary>
    /// Where a chunk starts and ends, from the locations of the units it covers, in their order.
    /// </summary>
    /// <remarks>
    /// The chunk starts on the page of its first unit and ends on the last page among its units.
    /// Its rows and lines run from the first unit which has one to the last which has one: the
    /// heading of a sheet and the fences around a table are no row or line, so a chunk starting
    /// with them starts at the row after them. A chunk lies within one sheet, cf. SplitBySheets.
    /// </remarks>
    /// <param name="locations">The locations of the units the chunk covers.</param>
    /// <param name="current">The location of the text being cut, for what none of the units states.</param>
    /// <returns>Where the chunk starts and where it ends.</returns>
    private static ChunkSpan Locate(IReadOnlyList<SourceLocation> locations, SourceLocation current)
    {
        if (locations.Count is 0)
            return new(current, current);

        var sheetName = locations.FirstOrDefault(location => location.SheetName is not null).SheetName ?? current.SheetName;
        var start = new SourceLocation(
            locations[0].PageNumber ?? current.PageNumber,
            sheetName,
            locations.FirstOrDefault(location => location.RowNumber is not null).RowNumber ?? current.RowNumber,
            locations.FirstOrDefault(location => location.LineNumber is not null).LineNumber ?? current.LineNumber);

        var end = new SourceLocation(
            locations.LastOrDefault(location => location.PageNumber is not null).PageNumber ?? current.PageNumber,
            sheetName,
            locations.LastOrDefault(location => location.RowNumber is not null).RowNumber ?? current.RowNumber,
            locations.LastOrDefault(location => location.LineNumber is not null).LineNumber ?? current.LineNumber);

        return new(start, end);
    }

    /// <remarks>
    /// The hard cut is only ever reached inside a single piece of text which no rule could split
    /// any further, so every chunk it produces sits where that piece was handed.
    /// </remarks>
    private async IAsyncEnumerable<EmbeddingChunk> SplitTextByHardCutAsync(
        string text,
        ChunkingOptions options,
        EmbeddingProvider embeddingProvider,
        SourceLocation currentLocation,
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
                throw new InvalidOperationException(string.Format(TB("The chunk size configured for the embedding provider '{0}' is too small: the smallest piece the text can be cut into still has {1} tokens, while the limit is {2}."), embeddingProvider.Name, smallestCandidateTokenCount, options.MaxChunkTokenLength));
            }

            // A hard cut works inside a single piece of text, so the chunk stays where it is:
            var chunk = AddOverlapPrefix(text[startIndex..bestEndIndex].Trim(), overlapPrefix);
            if (!string.IsNullOrWhiteSpace(chunk))
                yield return new(chunk, currentLocation, currentLocation);

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
        throw new InvalidOperationException(string.Format(TB("The tokens of the text could not be counted for the embedding provider '{0}'. {1}"), embeddingProvider.Name, message));
    }

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
        var matches = ParagraphBreakRegex().Matches(text);
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
        var matches = WordWithTrailingWhitespaceRegex().Matches(text);
        if (matches.Count == 0)
            return [text];

        return matches.Select(match => match.Value).ToList();
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

        if (MarkdownHeadingRegex().IsMatch(trimmed))
            return true;

        if (!string.IsNullOrWhiteSpace(previousLine) || !string.IsNullOrWhiteSpace(nextLine))
            return false;

        if (trimmed.Length is < 3 or > 120)
            return false;

        if (trimmed.Contains("|", StringComparison.Ordinal) || trimmed.EndsWith(".", StringComparison.Ordinal))
            return false;

        return PlainHeadingRegex().IsMatch(trimmed);
    }

    [GeneratedRegex(@"\n[ \t]*\n", RegexOptions.CultureInvariant)]
    private static partial Regex ParagraphBreakRegex();

    [GeneratedRegex(@"\S+\s*", RegexOptions.CultureInvariant)]
    private static partial Regex WordWithTrailingWhitespaceRegex();

    [GeneratedRegex(@"^#{1,6}\s+\S", RegexOptions.CultureInvariant)]
    private static partial Regex MarkdownHeadingRegex();

    /// <summary>
    /// A heading without Markdown: a numbered one, a chapter or section, or a line in capitals.
    /// </summary>
    [GeneratedRegex(@"^(\d+(\.\d+)*\.?\s+\S|(?i:chapter|section)\s+\S|[A-Z0-9][A-Z0-9 ,:;'/&()_-]{2,})$", RegexOptions.CultureInvariant)]
    private static partial Regex PlainHeadingRegex();
}