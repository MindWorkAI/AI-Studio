using System.Text;
using System.Text.Json;
using System.Runtime.CompilerServices;

using AIStudio.Tools.Rust;

namespace AIStudio.Tools.Services;

public sealed record ArbitraryFileDataSegment(string Content, int TokenCount);

public sealed partial class RustService
{
    public async Task<string> ReadArbitraryFileData(string path, int maxChunks, bool extractImages = false)
    {
        var streamId = Guid.NewGuid().ToString();
        var requestUri = $"/retrieval/fs/extract?path={Uri.EscapeDataString(path)}&stream_id={streamId}&extract_images={extractImages}&include_token_count=false";
        var request = new HttpRequestMessage(HttpMethod.Get, requestUri);
        var response = await this.http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);

        if (!response.IsSuccessStatusCode)
        {
            var responseBody = await response.Content.ReadAsStringAsync();
            this.logger?.LogError(
                "Failed to read arbitrary file data from Rust runtime. Status: {StatusCode}, reason: '{ReasonPhrase}', path: '{Path}', body: '{Body}'",
                response.StatusCode,
                response.ReasonPhrase,
                path,
                responseBody);
            return string.Empty;
        }

        var resultBuilder = new StringBuilder();

        try
        {
            await using var stream = await response.Content.ReadAsStreamAsync();
            using var reader = new StreamReader(stream);
            var chunkCount = 0;

            while (!reader.EndOfStream && chunkCount < maxChunks)
            {
                var line = await reader.ReadLineAsync();
                if (string.IsNullOrWhiteSpace(line))
                    continue;

                if (!line.StartsWith("data:", StringComparison.InvariantCulture))
                    continue;

                var jsonContent = line[5..];

                try
                {
                    var sseEvent = JsonSerializer.Deserialize<ContentStreamSseEvent>(jsonContent);
                    if (sseEvent is not null)
                    {
                        var content = ContentStreamSseHandler.ProcessEvent(sseEvent, extractImages);
                        if (content is not null)
                            resultBuilder.AppendLine(content);

                        chunkCount++;
                    }
                }
                catch (JsonException)
                {
                    if (this.TryLogSseErrorMessage(jsonContent, path))
                        continue;

                    this.logger?.LogError("Failed to deserialize SSE event: {JsonContent}", jsonContent);
                }
            }
        }
        catch(Exception e)
        {
            this.logger?.LogError(e, "Error reading file data from stream: {Path}", path);
        }
        finally
        {
            var finalContentChunk = ContentStreamSseHandler.Clear(streamId);
            if (!string.IsNullOrWhiteSpace(finalContentChunk))
                resultBuilder.AppendLine(finalContentChunk);
        }
        
        return resultBuilder.ToString();
    }

    public async IAsyncEnumerable<string> StreamArbitraryFileData(string path, bool extractImages = false, [EnumeratorCancellation] CancellationToken token = default)
    {
        await foreach (var segment in this.StreamArbitraryFileDataCore(path, extractImages, false, token))
            yield return segment.Content;
    }

    public async IAsyncEnumerable<ArbitraryFileDataSegment> StreamArbitraryFileDataWithTokenCounts(
        string path,
        string providerName,
        string tokenizerPath,
        [EnumeratorCancellation] CancellationToken token = default)
    {
        await this.tokenizerLock.WaitAsync(token);
        try
        {
            var tokenizerResponse = await this.EnsureTokenizerCoreAsync(providerName, tokenizerPath);
            if (tokenizerResponse is not { Success: true, Status: TokenizerStatus.AVAILABLE })
            {
                var message = tokenizerResponse?.Message ?? "No response was returned by the tokenizer service.";
                throw new InvalidOperationException($"Could not initialize tokenizer for provider '{providerName}'. {message}");
            }

            await foreach (var segment in this.StreamArbitraryFileDataCore(path, false, true, token))
            {
                if (segment.TokenCount is null)
                    throw new InvalidOperationException($"Rust did not return a token count for an extracted segment from '{path}'.");

                yield return new(segment.Content, segment.TokenCount.Value);
            }
        }
        finally
        {
            this.tokenizerLock.Release();
        }
    }

    private async IAsyncEnumerable<(string Content, int? TokenCount)> StreamArbitraryFileDataCore(
        string path,
        bool extractImages,
        bool includeTokenCount,
        [EnumeratorCancellation] CancellationToken token)
    {
        var streamId = Guid.NewGuid().ToString();
        var requestUri = $"/retrieval/fs/extract?path={Uri.EscapeDataString(path)}&stream_id={streamId}&extract_images={extractImages}&include_token_count={includeTokenCount}";
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

                var content = ContentStreamSseHandler.ProcessEvent(sseEvent, extractImages);
                if (!string.IsNullOrWhiteSpace(content))
                    yield return (content, sseEvent.TokenCount);
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
