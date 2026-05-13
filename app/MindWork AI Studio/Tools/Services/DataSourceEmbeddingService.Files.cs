using System.Security.Cryptography;
using System.Text;

using AIStudio.Settings;
using AIStudio.Settings.DataModel;
using AIStudio.Tools.PluginSystem;
using AIStudio.Tools.Rust;

namespace AIStudio.Tools.Services;

public sealed partial class DataSourceEmbeddingService
{
    private static readonly string[] ADDITIONAL_RAG_FILE_EXTENSIONS = ["csv", "tsv", "ods", "xlsm", "xlsb", "xla", "xlam"];
    private static readonly string[] SKIPPED_RAG_FILE_EXTENSIONS = ["lnk"];

    private async IAsyncEnumerable<string> StreamEmbeddingChunksAsync(string filePath, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken token)
    {
        if (this.IsImageFilePath(filePath))
        {
            yield return this.BuildImageIndexText(filePath);
            yield break;
        }

        var currentChunk = new StringBuilder();

        await foreach (var segment in this.rustService.StreamArbitraryFileData(filePath, token: token))
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
                        yield return chunk;

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
            yield return finalChunk;
    }

    private FileEnumerationResult GetInputFiles(IDataSource dataSource)
    {
        var result = new FileEnumerationResult();

        switch (dataSource)
        {
            case DataSourceLocalFile localFile when File.Exists(localFile.FilePath):
                if (this.IsSupportedRagFilePath(localFile.FilePath))
                {
                    result.Files.Add(new FileInfo(localFile.FilePath));
                }
                else
                {
                    result.FailedFiles = 1;
                    result.LastError = $"The selected file '{localFile.FilePath}' is not supported for background embeddings.";
                }

                return result;

            case DataSourceLocalDirectory localDirectory when Directory.Exists(localDirectory.Path):
                this.EnumerateAccessibleFiles(localDirectory.Path, result);
                return result;
        }

        switch (dataSource)
        {
            case DataSourceLocalFile localFile:
                result.FailedFiles = 1;
                result.LastError = $"The selected file '{localFile.FilePath}' does not exist.";
                break;

            case DataSourceLocalDirectory localDirectory:
                result.FailedFiles = 1;
                result.LastError = $"The selected directory '{localDirectory.Path}' does not exist.";
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
                this.logger.LogWarning(exception, "Cannot access directory '{DirectoryPath}' while indexing.", currentPath);
                result.FailedFiles++;
                result.LastError = $"The directory '{currentPath}' could not be accessed.";
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
                    this.logger.LogWarning(exception, "Cannot inspect file '{FilePath}' while indexing.", filePath);
                    result.FailedFiles++;
                    result.LastError = $"The file '{filePath}' could not be inspected.";
                    continue;
                }

                if (!this.IsSupportedRagFilePath(fileInfo.FullName))
                    continue;

                result.Files.Add(fileInfo);
            }

            foreach (var subDirectory in subDirectories)
                pendingDirectories.Push(subDirectory);
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
        if (SKIPPED_RAG_FILE_EXTENSIONS.Contains(extension, StringComparer.OrdinalIgnoreCase))
            return false;

        return FileTypes.IsAllowedPath(filePath, FileTypes.DOCUMENT, FileTypes.IMAGE)
               || ADDITIONAL_RAG_FILE_EXTENSIONS.Contains(extension, StringComparer.OrdinalIgnoreCase);
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
            embeddingProvider.TokenizerPath);
    }

    private string BuildFingerprint(FileInfo file)
    {
        var fingerprintSource = $"{file.FullName}|{file.Length}|{file.LastWriteTimeUtc.Ticks}";
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(fingerprintSource));
        return Convert.ToHexString(bytes);
    }

    private string GetCollectionName(string dataSourceId)
    {
        var safeId = dataSourceId
            .ToLowerInvariant()
            .Replace("-", string.Empty, StringComparison.Ordinal);

        return $"rag_{safeId}";
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
