using System.Text;
using System.Text.Json;

namespace AIStudio.Tools.Services;

public sealed partial class RustService
{
    /// <summary>
    /// How long one file extraction may take.
    /// </summary>
    /// <remarks>
    /// Reading a large file from a slow network share is legitimately slow, so this is well above
    /// the default HTTP client timeout. It still bounds the operation, because an unbounded read
    /// would keep the caller waiting forever.
    /// </remarks>
    private static readonly TimeSpan EXTRACTION_TIMEOUT = TimeSpan.FromMinutes(10);

    public async Task<FileExtractionResult> ReadArbitraryFileData(string path, int maxChunks, bool extractImages = false)
    {
        var streamId = Guid.NewGuid().ToString();
        var requestUri = $"/retrieval/fs/extract?path={Uri.EscapeDataString(path)}&stream_id={streamId}&extract_images={extractImages}";

        using var timeoutTokenSource = new CancellationTokenSource(EXTRACTION_TIMEOUT);
        var cancellationToken = timeoutTokenSource.Token;

        var resultBuilder = new StringBuilder();
        var failedPages = new List<int>();
        var hasPartialFailure = false;
        var failureCode = FileExtractionErrorCode.NONE;
        string? failureMessage = null;

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, requestUri);
            using var response = await this.extractionHttp.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
                this.logger?.LogError(
                    "Failed to read arbitrary file data from Rust runtime. Status: {StatusCode}, reason: '{ReasonPhrase}', path: '{Path}', body: '{Body}'",
                    response.StatusCode,
                    response.ReasonPhrase,
                    path,
                    responseBody);

                return FileExtractionResult.Failed(FileExtractionErrorCode.REQUEST_FAILED, $"The runtime answered with the status {(int)response.StatusCode} ({response.ReasonPhrase}).");
            }

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var reader = new StreamReader(stream);
            var chunkCount = 0;

            while (chunkCount < maxChunks)
            {
                // We read line by line instead of checking EndOfStream: the latter blocks on a
                // network stream and cannot be cancelled, which would defeat the timeout above.
                var line = await reader.ReadLineAsync(cancellationToken);
                if (line is null)
                    break;

                if (string.IsNullOrWhiteSpace(line))
                    continue;

                if (!line.StartsWith("data:", StringComparison.InvariantCulture))
                    continue;

                var jsonContent = line[5..];

                try
                {
                    var sseEvent = JsonSerializer.Deserialize<ContentStreamSseEvent>(jsonContent);
                    if (sseEvent is null)
                        continue;

                    var processedEvent = ContentStreamSseHandler.ProcessEvent(sseEvent, extractImages);
                    if (processedEvent.Error is not null)
                    {
                        var error = processedEvent.Error;
                        this.logger?.LogError(
                            "The runtime reported a failure while reading '{Path}': code={ErrorCode}, page={PageNumber}, partial={IsPartialFailure}, message='{Message}'",
                            path,
                            error.ParsedCode,
                            error.PageNumber,
                            error.IsPartialFailure,
                            error.Message);

                        //
                        // A partial failure costs us one part of the file, e.g. a single PDF page,
                        // but keeps the rest usable. Any other failure means what we collected is
                        // not the document the user picked, so we must not pass it on as content.
                        //
                        if (error.IsPartialFailure)
                        {
                            hasPartialFailure = true;
                            if (error.PageNumber is { } pageNumber)
                                failedPages.Add(pageNumber);
                        }
                        else if (failureCode is FileExtractionErrorCode.NONE)
                        {
                            failureCode = error.ParsedCode;
                            failureMessage = error.Message;
                        }
                    }
                    else if (processedEvent.Content is not null)
                        resultBuilder.AppendLine(processedEvent.Content);

                    chunkCount++;
                }
                catch (JsonException e)
                {
                    this.logger?.LogError(e, "Failed to deserialize SSE event while reading '{Path}': {JsonContent}", path, jsonContent);

                    if (failureCode is FileExtractionErrorCode.NONE)
                    {
                        failureCode = FileExtractionErrorCode.INVALID_RESPONSE;
                        failureMessage = "The runtime sent a response the app was not able to read.";
                    }
                }
            }
        }
        catch (OperationCanceledException) when (timeoutTokenSource.IsCancellationRequested)
        {
            this.logger?.LogError("Reading the file '{Path}' timed out after {Timeout}.", path, EXTRACTION_TIMEOUT);
            return FileExtractionResult.Failed(FileExtractionErrorCode.TIMEOUT, $"Reading the file timed out after {EXTRACTION_TIMEOUT.TotalMinutes:0} minutes.");
        }
        catch (Exception e)
        {
            this.logger?.LogError(e, "Error reading file data from stream: {Path}", path);
            return FileExtractionResult.Failed(FileExtractionErrorCode.INTERNAL, e.Message);
        }
        finally
        {
            var finalContentChunk = ContentStreamSseHandler.Clear(streamId);
            if (!string.IsNullOrWhiteSpace(finalContentChunk))
                resultBuilder.AppendLine(finalContentChunk);
        }

        if (failureCode is not FileExtractionErrorCode.NONE)
            return FileExtractionResult.Failed(failureCode, failureMessage);

        var content = resultBuilder.ToString();

        //
        // Nothing failed, yet nothing came out either. We report this as a failure as well:
        // handing an empty document to the AI looks like a file without content, and the user
        // would never learn that reading the file did not work.
        //
        if (string.IsNullOrWhiteSpace(content))
        {
            this.logger?.LogWarning("Reading the file '{Path}' produced no content at all.", path);
            return FileExtractionResult.Failed(FileExtractionErrorCode.NO_CONTENT, "Reading the file produced no content.");
        }

        return hasPartialFailure
            ? FileExtractionResult.Partial(content, failedPages)
            : FileExtractionResult.Success(content);
    }
}