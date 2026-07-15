using System.Runtime.CompilerServices;
using System.Text.Json;

using AIStudio.Tools.Rust;

namespace AIStudio.Tools.Services;

public partial class RustService
{
    /// <summary>Starts a Rust media normalization job.</summary>
    /// <param name="inputPath">Absolute source path.</param>
    /// <param name="outputPath">Absolute operation-owned output path.</param>
    /// <param name="token">Request cancellation token.</param>
    /// <returns>The opaque runtime job identifier.</returns>
    public async Task<string> StartMediaJobAsync(string inputPath, string outputPath, CancellationToken token = default)
    {
        using var response = await this.http.PostAsJsonAsync(
            "/media/jobs",
            new CreateMediaJobRequest(inputPath, outputPath),
            this.jsonRustSerializerOptions,
            token);

        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<CreateMediaJobResponse>(this.jsonRustSerializerOptions, token);
        return result?.JobId ?? throw new InvalidOperationException("The Rust runtime did not return a media job ID.");
    }

    /// <summary>Streams replayed and live snapshots until the media job becomes terminal.</summary>
    /// <param name="jobId">Runtime job identifier.</param>
    /// <param name="token">Stream cancellation token.</param>
    /// <returns>Asynchronous media job snapshots.</returns>
    public async IAsyncEnumerable<MediaJobEvent> StreamMediaJobEventsAsync(string jobId, [EnumeratorCancellation] CancellationToken token = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, $"/media/jobs/{Uri.EscapeDataString(jobId)}/events");
        using var response = await this.http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, token);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(token);
        using var reader = new StreamReader(stream);

        while (!token.IsCancellationRequested)
        {
            var line = await reader.ReadLineAsync(token);
            if (line is null)
                yield break;

            if (!line.StartsWith("data:", StringComparison.Ordinal))
                continue;

            var json = line["data:".Length..].Trim();
            var mediaEvent = JsonSerializer.Deserialize<MediaJobEvent>(json, this.jsonRustSerializerOptions);
            if (mediaEvent is not null)
                yield return mediaEvent;

            if (mediaEvent?.Phase is MediaJobPhase.COMPLETED or MediaJobPhase.FAILED or MediaJobPhase.CANCELLED)
                yield break;
        }
    }

    /// <summary>Requests cooperative cancellation of a Rust media job.</summary>
    /// <param name="jobId">Runtime job identifier.</param>
    /// <param name="token">Request cancellation token.</param>
    public async Task CancelMediaJobAsync(string jobId, CancellationToken token = default)
    {
        using var response = await this.http.DeleteAsync($"/media/jobs/{Uri.EscapeDataString(jobId)}", token);
        if (response is { IsSuccessStatusCode: false, StatusCode: not System.Net.HttpStatusCode.NotFound })
            response.EnsureSuccessStatusCode();
    }
}