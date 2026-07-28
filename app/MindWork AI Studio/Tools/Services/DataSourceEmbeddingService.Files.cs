using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

using AIStudio.Settings;
using AIStudio.Settings.DataModel;
using AIStudio.Tools.PluginSystem;
using AIStudio.Tools.Rust;

namespace AIStudio.Tools.Services;

public sealed partial class DataSourceEmbeddingService
{
    private const string OFFICE_LOCK_FILE_PREFIX = "~$";

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

    private async IAsyncEnumerable<string> StreamEmbeddingChunksAsync(string filePath, EmbeddingProvider embeddingProvider, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken token)
    {
        if (this.IsImageFilePath(filePath))
        {
            await foreach (var imageChunk in this.SplitChunkByEmbeddingTokenLimitAsync(this.BuildImageIndexText(filePath), embeddingProvider, token))
                yield return imageChunk;

            yield break;
        }

        var currentChunk = new StringBuilder();

        await foreach (var segment in rustService.StreamArbitraryFileData(filePath, token: token))
        {
            var normalized = NormalizeChunkSegment(segment);
            if (string.IsNullOrWhiteSpace(normalized))
                continue;

            if (currentChunk.Length > 0 && currentChunk.Length + normalized.Length + Environment.NewLine.Length > MAX_CHUNK_LENGTH)
            {
                if (currentChunk.Length >= MIN_CHUNK_LENGTH)
                {
                    var chunk = currentChunk.ToString().Trim();
                    if (!string.IsNullOrWhiteSpace(chunk))
                    {
                        await foreach (var splitChunk in this.SplitChunkByEmbeddingTokenLimitAsync(chunk, embeddingProvider, token))
                            yield return splitChunk;
                    }

                    var overlap = chunk.Length > CHUNK_OVERLAP_LENGTH
                        ? chunk[^CHUNK_OVERLAP_LENGTH..]
                        : chunk;

                    currentChunk.Clear();
                    currentChunk.Append(overlap);
                    currentChunk.AppendLine();
                }
                else
                {
                    currentChunk.AppendLine();
                }
            }

            currentChunk.Append(normalized);
            currentChunk.AppendLine();
        }

        var finalChunk = currentChunk.ToString().Trim();
        if (!string.IsNullOrWhiteSpace(finalChunk))
        {
            await foreach (var chunk in this.SplitChunkByEmbeddingTokenLimitAsync(finalChunk, embeddingProvider, token))
                yield return chunk;
        }
    }

    private async IAsyncEnumerable<string> SplitChunkByEmbeddingTokenLimitAsync(string chunk, EmbeddingProvider embeddingProvider, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken token)
    {
        var tokenLimit = embeddingProvider.EffectiveChunkTokenLimit;
        var tokenCount = await this.GetEmbeddingTokenCountAsync(embeddingProvider, chunk, token);
        if (tokenCount <= tokenLimit)
        {
            yield return chunk;
            yield break;
        }

        if (embeddingProvider.UsesAssumedTokenSizing)
        {
            logger.LogDebug(
                "Using conservative embedding chunk limit {ChunkTokenLimit} for provider '{EmbeddingProviderName}' because tokenizer or token limit sizing is assumed. ConfiguredTokenLimit={ConfiguredTokenLimit}.",
                tokenLimit,
                embeddingProvider.Name,
                embeddingProvider.EffectiveTokenLimit);
        }

        logger.LogDebug(
            "Splitting an embedding chunk for provider '{EmbeddingProviderName}' because it has {TokenCount} tokens and the configured limit is {TokenLimit}.",
            embeddingProvider.Name,
            tokenCount,
            tokenLimit);

        await foreach (var splitChunk in this.SplitTextByTokenLimitAsync(chunk, embeddingProvider, tokenLimit, token))
            yield return splitChunk;
    }

    private async IAsyncEnumerable<string> SplitTextByTokenLimitAsync(string text, EmbeddingProvider embeddingProvider, int tokenLimit, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken token)
    {
        var units = SplitTextIntoTokenUnits(text);
        var index = 0;

        while (index < units.Count)
        {
            token.ThrowIfCancellationRequested();

            var unitCount = await this.FindLargestUnitCountWithinTokenLimitAsync(units, index, embeddingProvider, tokenLimit, token);
            if (unitCount > 0)
            {
                var chunk = string.Concat(units.Skip(index).Take(unitCount)).Trim();
                if (!string.IsNullOrWhiteSpace(chunk))
                    yield return chunk;

                index += unitCount;
                continue;
            }

            await foreach (var splitUnit in this.SplitOversizedTextUnitByTokenLimitAsync(units[index], embeddingProvider, tokenLimit, token))
                yield return splitUnit;

            index++;
        }
    }

    private async Task<int> FindLargestUnitCountWithinTokenLimitAsync(IReadOnlyList<string> units, int startIndex, EmbeddingProvider embeddingProvider, int tokenLimit, CancellationToken token)
    {
        var low = 1;
        var high = units.Count - startIndex;
        var best = 0;

        while (low <= high)
        {
            token.ThrowIfCancellationRequested();

            var mid = low + (high - low) / 2;
            var candidate = string.Concat(units.Skip(startIndex).Take(mid)).Trim();
            var tokenCount = await this.GetEmbeddingTokenCountAsync(embeddingProvider, candidate, token);
            if (tokenCount <= tokenLimit)
            {
                best = mid;
                low = mid + 1;
            }
            else
                high = mid - 1;
        }

        return best;
    }

    private async IAsyncEnumerable<string> SplitOversizedTextUnitByTokenLimitAsync(string text, EmbeddingProvider embeddingProvider, int tokenLimit, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken token)
    {
        var startIndex = 0;
        while (startIndex < text.Length)
        {
            token.ThrowIfCancellationRequested();

            var low = startIndex + 1;
            var high = text.Length;
            var bestEndIndex = startIndex;

            while (low <= high)
            {
                var mid = low + (high - low) / 2;
                var candidate = text[startIndex..mid].Trim();
                var tokenCount = await this.GetEmbeddingTokenCountAsync(embeddingProvider, candidate, token);
                if (tokenCount <= tokenLimit)
                {
                    bestEndIndex = mid;
                    low = mid + 1;
                }
                else
                    high = mid - 1;
            }

            if (bestEndIndex == startIndex)
            {
                var smallestCandidate = text[startIndex..Math.Min(startIndex + 1, text.Length)].Trim();
                var smallestCandidateTokenCount = await this.GetEmbeddingTokenCountAsync(embeddingProvider, smallestCandidate, token);
                throw new InvalidOperationException($"The token limit for embedding provider '{embeddingProvider.Name}' is too low. The smallest possible split still has {smallestCandidateTokenCount} tokens, but the configured limit is {tokenLimit}.");
            }

            var chunk = text[startIndex..bestEndIndex].Trim();
            if (!string.IsNullOrWhiteSpace(chunk))
                yield return chunk;

            startIndex = bestEndIndex;
        }
    }

    private async Task<int> GetEmbeddingTokenCountAsync(EmbeddingProvider embeddingProvider, string text, CancellationToken token)
    {
        var response = await rustService.GetTokenCount(embeddingProvider.Name, embeddingProvider.TokenizerPath, text, token);
        if (response is { Success: true, Status: TokenizerStatus.AVAILABLE })
            return response.Value.TokenCount;

        var message = response?.Message ?? "No response was returned by the tokenizer service.";
        throw new InvalidOperationException($"Could not count tokens for embedding provider '{embeddingProvider.Name}'. {message}");
    }

    private static List<string> SplitTextIntoTokenUnits(string text)
    {
        var matches = Regex.Matches(text, @"\S+\s*", RegexOptions.CultureInvariant);
        if (matches.Count == 0)
            return [text];

        return matches.Cast<Match>().Select(match => match.Value).ToList();
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

    private string BuildEmbeddingSignature(EmbeddingProvider embeddingProvider)
    {
        return string.Join('|',
            embeddingProvider.Id,
            embeddingProvider.UsedLLMProvider,
            embeddingProvider.Model.Id,
            embeddingProvider.Host,
            embeddingProvider.Hostname,
            embeddingProvider.TokenizerPath,
            embeddingProvider.EffectiveTokenLimit,
            embeddingProvider.EffectiveChunkTokenLimit);
    }

    private async Task<string> BuildFingerprintAsync(FileInfo file, CancellationToken token)
    {
        await using var stream = new FileStream(
            file.FullName,
            FileMode.Open,
            FileAccess.Read,
            FileShare.ReadWrite | FileShare.Delete,
            1024 * 128,
            FileOptions.Asynchronous | FileOptions.SequentialScan);
        var contentHash = await SHA256.HashDataAsync(stream, token);
        var fingerprintSource = $"{file.FullName}|{Convert.ToHexString(contentHash)}";
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(fingerprintSource));
        return Convert.ToHexString(bytes);
    }

    private string GetCollectionName(string dataSourceName, string dataSourceId)
    {
        var safeId = dataSourceId
            .ToLowerInvariant()
            .Replace("-", string.Empty, StringComparison.Ordinal);
        
        var safeName = new string(dataSourceName
            .ToLowerInvariant()
            .Where(c => c is >= 'a' and <= 'z' or >= '0' and <= '9')
            .Take(32)
            .ToArray());
        
        safeName = string.IsNullOrWhiteSpace(safeName) ? "datasource" : safeName;
        
        return $"rag_{safeName}_{safeId}";
    }

    private string CreatePointId(string dataSourceId, string fingerprint, int chunkIndex)
    {
        var source = $"{dataSourceId}:{fingerprint}:{chunkIndex}";
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(source));
        var guidBytes = hash[..16].ToArray();

        guidBytes[6] = (byte)((guidBytes[6] & 0x0F) | 0x40);
        guidBytes[8] = (byte)((guidBytes[8] & 0x3F) | 0x80);

        return new Guid(guidBytes).ToString();
    }
}
