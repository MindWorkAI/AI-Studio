using System.Runtime.CompilerServices;
using System.Text.Json;

using AIStudio.Tools.Rust;

namespace AIStudio.Tools.Services;

public partial class RustService
{
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

    public async IAsyncEnumerable<MediaJobEvent> StreamMediaJobEventsAsync(string jobId, [EnumeratorCancellation] CancellationToken token = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, $"/media/jobs/{Uri.EscapeDataString(jobId)}/events");
        using var response = await this.http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, token);
        response.EnsureSuccessStatusCode();
        
        await using var stream = await response.Content.ReadAsStreamAsync(token);
        using var reader = new StreamReader(stream);

        while (!reader.EndOfStream && !token.IsCancellationRequested)
        {
            var line = await reader.ReadLineAsync(token);
            if (line is null || !line.StartsWith("data:", StringComparison.Ordinal))
                continue;

            var json = line["data:".Length..].Trim();
            var mediaEvent = JsonSerializer.Deserialize<MediaJobEvent>(json, this.jsonRustSerializerOptions);
            if (mediaEvent is not null)
                yield return mediaEvent;

            if (mediaEvent?.Phase is MediaJobPhase.COMPLETED or MediaJobPhase.FAILED or MediaJobPhase.CANCELLED)
                yield break;
        }
    }

    public async Task CancelMediaJobAsync(string jobId, CancellationToken token = default)
    {
        using var response = await this.http.DeleteAsync($"/media/jobs/{Uri.EscapeDataString(jobId)}", token);
        if (response is { IsSuccessStatusCode: false, StatusCode: not System.Net.HttpStatusCode.NotFound })
            response.EnsureSuccessStatusCode();
    }
}