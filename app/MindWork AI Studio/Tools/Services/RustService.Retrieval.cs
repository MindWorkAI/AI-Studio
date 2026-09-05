using System.Text;
using System.Text.Json;
using System.Runtime.CompilerServices;

using AIStudio.Settings;
using AIStudio.Tools.Rust;
using AIStudio.Tools.Security;

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

    /// <summary>
    /// Reads the content of an arbitrary file through the Rust runtime.
    /// </summary>
    /// <param name="path">The path of the file to read.</param>
    /// <param name="maxChunks">How many chunks of the content stream we read at most.</param>
    /// <param name="extractImages">Whether we want the images of the file as well.</param>
    /// <param name="token">
    /// Cancels the extraction when the caller no longer needs the content. Reading a large document
    /// takes a while, and without this, the runtime would keep streaming into a caller which is
    /// already gone.
    /// </param>
    /// <returns>The result of reading the file.</returns>
    public async Task<FileExtractionResult> ReadArbitraryFileData(string path, int maxChunks, bool extractImages = false, CancellationToken token = default)
    {
        //
        // The runtime filters prompt injections while it streams the file. Doing it there rather
        // than here means the whole document never has to exist in memory at once, which is what
        // makes documents of a few thousand pages affordable.
        //
        var guardService = Program.SERVICE_PROVIDER.GetRequiredService<PromptInjectionGuardService>();

        var streamId = Guid.NewGuid().ToString();
        var requestUri = $"/retrieval/fs/extract?path={Uri.EscapeDataString(path)}&stream_id={streamId}&extract_images={extractImages}&include_token_count=false";

        //
        // Both reasons to stop end the same read, so we combine them: our own timeout bounds the
        // operation, and the caller's token ends it as soon as nobody needs the content anymore.
        //
        using var timeoutTokenSource = new CancellationTokenSource(EXTRACTION_TIMEOUT);
        using var cancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(timeoutTokenSource.Token, token);
        var cancellationToken = cancellationTokenSource.Token;

        var resultBuilder = new StringBuilder();
        var failedPages = new List<int>();
        var promptInjectionFindings = new List<PromptInjectionFinding>();
        var promptInjectionRedactedCount = 0;
        var hasPartialFailure = false;
        var failureCode = FileExtractionErrorCode.NONE;
        string? failureMessage = null;
        string? detectedFormat = null;

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

                        //
                        // A notice is not a failure: the file was read completely, we only learned
                        // something about it worth telling the user. It must not change the outcome.
                        //
                        if (error.IsNotice)
                        {
                            this.logger?.LogInformation(
                                "The runtime reported a notice while reading '{Path}': code={ErrorCode}, detectedFormat='{DetectedFormat}', message='{Message}'",
                                path,
                                error.ParsedCode,
                                error.DetectedFormat,
                                error.Message);

                            detectedFormat ??= error.DetectedFormat;
                            chunkCount++;
                            continue;
                        }

                        this.logger?.LogError(
                            "The runtime reported a failure while reading '{Path}': code={ErrorCode}, page={PageNumber}, partial={IsPartialFailure}, detectedFormat='{DetectedFormat}', message='{Message}'",
                            path,
                            error.ParsedCode,
                            error.PageNumber,
                            error.IsPartialFailure,
                            error.DetectedFormat,
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
                            detectedFormat = error.DetectedFormat;
                        }
                    }
                    else if (processedEvent.PromptInjection is { } promptInjection)
                    {
                        //
                        // Not a failure: the passages were removed and the document around them is
                        // intact. It only needs to reach the user, so they know their document was
                        // changed before the AI saw it.
                        //
                        promptInjectionRedactedCount += promptInjection.RedactedCount;
                        if (promptInjection.Findings is { } findings)
                            promptInjectionFindings.AddRange(findings);
                    }
                    else if (processedEvent.Content is not null)
                        resultBuilder.AppendLine(processedEvent.Content);

                    chunkCount++;
                }
                catch (JsonException e)
                {
                    // The runtime may report a failure as a bare JSON string instead of a chunk.
                    // That form still carries a readable reason, so we log it as such -- but it
                    // remains a failure and must reach the caller like any other:
                    if (!this.TryLogSseErrorMessage(jsonContent, path))
                        this.logger?.LogError(e, "Failed to deserialize SSE event while reading '{Path}': {JsonContent}", path, jsonContent);

                    if (failureCode is FileExtractionErrorCode.NONE)
                    {
                        failureCode = FileExtractionErrorCode.INVALID_RESPONSE;
                        failureMessage = "The runtime sent a response the app was not able to read.";
                    }
                }
            }
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            //
            // The caller dropped out, e.g. because the user closed the dialog which asked for this
            // file. That is not a failure, so we log it as information and leave it to the caller
            // to stay silent about it.
            //
            this.logger?.LogInformation("Reading the file '{Path}' was cancelled by the caller.", path);
            return FileExtractionResult.Failed(FileExtractionErrorCode.CANCELLED, "The caller cancelled reading the file.");
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
            return FileExtractionResult.Failed(failureCode, failureMessage, detectedFormat);

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

        var result = hasPartialFailure
            ? FileExtractionResult.Partial(content, failedPages, detectedFormat)
            : FileExtractionResult.Success(content, detectedFormat);

        if (promptInjectionRedactedCount is 0)
            return result;

        //
        // Reported from here rather than from the callers: every way of reading a file passes
        // through this method, so this is the one place where no caller can forget it.
        //
        await guardService.ReportAsync(new(PromptInjectionSource.FileContent(path), promptInjectionFindings, promptInjectionRedactedCount));

        //
        // Filtering does not change the outcome: the passages were removed and the document
        // around them is intact. The findings travel along so a caller can show them next to
        // the document they belong to.
        //
        return result with
        {
            PromptInjectionFindings = promptInjectionFindings,
            PromptInjectionRedactedCount = promptInjectionRedactedCount,
        };
    }

    public async IAsyncEnumerable<string> StreamArbitraryFileData(string path, bool extractImages = false, [EnumeratorCancellation] CancellationToken token = default)
    {
        await foreach (var segment in this.StreamArbitraryFileDataCore(path, extractImages, false, string.Empty, token))
            yield return segment.Content;
    }

    public async IAsyncEnumerable<ArbitraryFileDataSegment> StreamArbitraryFileDataWithTokenCounts(
        string path,
        EmbeddingProvider embeddingProvider,
        [EnumeratorCancellation] CancellationToken token = default)
    {
        await foreach (var segment in this.StreamArbitraryFileDataCore(path, false, true, embeddingProvider.TokenizerPath, token))
        {
            if (segment.TokenCount is null)
                throw new InvalidOperationException($"Rust did not return a token count for an extracted segment from '{path}' using provider '{embeddingProvider.Name}'.");

            yield return new(segment.Content, segment.TokenCount.Value);
        }
    }

    private async IAsyncEnumerable<(string Content, int? TokenCount)> StreamArbitraryFileDataCore(
        string path,
        bool extractImages,
        bool includeTokenCount,
        string tokenizerPath,
        [EnumeratorCancellation] CancellationToken token)
    {
        var streamId = Guid.NewGuid().ToString();
        var requestUri = $"/retrieval/fs/extract?path={Uri.EscapeDataString(path)}&stream_id={streamId}&extract_images={extractImages}&include_token_count={includeTokenCount}&tokenizer_path={Uri.EscapeDataString(tokenizerPath)}";
        using var request = new HttpRequestMessage(HttpMethod.Get, requestUri);
        using var response = await this.http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, token);

        if (!response.IsSuccessStatusCode)
        {
            var responseBody = await response.Content.ReadAsStringAsync(token);
            this.logger?.LogError(
                "Failed to stream arbitrary file data from Rust runtime. Status: {StatusCode}, reason: '{ReasonPhrase}', path: '{Path}', body: '{Body}'",
                response.StatusCode,
                response.ReasonPhrase,
                path,
                responseBody);

            if (includeTokenCount)
                throw new InvalidOperationException($"Rust could not extract and count '{path}'. HTTP {(int)response.StatusCode} ({response.ReasonPhrase}): {responseBody}");

            yield break;
        }

        string? finalContentChunk = null;
        try
        {
            await using var stream = await response.Content.ReadAsStreamAsync(token);
            using var reader = new StreamReader(stream);

            while (!reader.EndOfStream && !token.IsCancellationRequested)
            {
                var line = await reader.ReadLineAsync(token);
                if (string.IsNullOrWhiteSpace(line))
                    continue;

                if (!line.StartsWith("data:", StringComparison.InvariantCulture))
                    continue;

                var jsonContent = line[5..];
                ContentStreamSseEvent? sseEvent = null;
                try
                {
                    sseEvent = JsonSerializer.Deserialize<ContentStreamSseEvent>(jsonContent);
                }
                catch (JsonException)
                {
                    if (this.TryLogSseErrorMessage(jsonContent, path))
                    {
                        if (includeTokenCount)
                            throw new InvalidOperationException($"Rust could not extract and count a segment from '{path}'. See the runtime log for details.");

                        continue;
                    }

                    this.logger?.LogError("Failed to deserialize SSE event: {JsonContent}", jsonContent);
                }

                if (sseEvent is null)
                    continue;

                var processedEvent = ContentStreamSseHandler.ProcessEvent(sseEvent, extractImages);
                if (processedEvent.Error is { } error)
                {
                    // A notice says something about the file without failing the read, so the
                    // remaining content still belongs into the index:
                    if (error.IsNotice)
                    {
                        this.logger?.LogInformation(
                            "The runtime reported a notice while reading '{Path}' for embedding: code={ErrorCode}, detectedFormat='{DetectedFormat}', message='{Message}'",
                            path,
                            error.ParsedCode,
                            error.DetectedFormat,
                            error.Message);

                        continue;
                    }

                    //
                    // Everything else stops the read. Embedding a document which was only read in
                    // part would put a silently incomplete text into the index, and nothing after
                    // this point would reveal the gap:
                    //
                    this.logger?.LogError(
                        "The runtime reported a failure while reading '{Path}' for embedding: code={ErrorCode}, page={PageNumber}, detectedFormat='{DetectedFormat}', message='{Message}'",
                        path,
                        error.ParsedCode,
                        error.PageNumber,
                        error.DetectedFormat,
                        error.Message);

                    throw new InvalidOperationException($"Rust could not extract '{path}': {error.Message}");
                }

                if (!string.IsNullOrWhiteSpace(processedEvent.Content))
                    yield return (processedEvent.Content, sseEvent.TokenCount);
            }
        }
        finally
        {
            finalContentChunk = ContentStreamSseHandler.Clear(streamId);
        }

        if (!string.IsNullOrWhiteSpace(finalContentChunk))
            yield return (finalContentChunk, null);
    }

    private bool TryLogSseErrorMessage(string jsonContent, string path)
    {
        try
        {
            var errorMessage = JsonSerializer.Deserialize<string>(jsonContent);
            if (string.IsNullOrWhiteSpace(errorMessage))
                return false;

            this.logger?.LogError("Rust retrieval stream error for '{Path}': {ErrorMessage}", path, errorMessage);
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }
}
