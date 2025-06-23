using System.Text;
using System.Text.Json;
using AIStudio.Settings.DataModel;

namespace AIStudio.Tools.Services;

public sealed partial class RustService
{
    public async Task<string> GetPDFText(string filePath)
    {
        var response = await this.http.GetAsync($"/retrieval/fs/read/pdf?file_path={filePath}");
        if (!response.IsSuccessStatusCode)
        {
            this.logger!.LogError($"Failed to read the PDF file due to an network error: '{response.StatusCode}'");
            return string.Empty;
        }

        return await response.Content.ReadAsStringAsync();
    }

    public async Task<string> ReadArbitraryFileData(string path, int maxEvents)
    {
        var requestUri = $"/retrieval/fs/extract?path={Uri.EscapeDataString(path)}";
        this.logger?.LogInformation("The encoded path is: '{Path}'", requestUri);

        var request = new HttpRequestMessage(HttpMethod.Get, requestUri);
        var response = await this.http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);

        this.logger?.LogInformation("Response received: {StatusCode}", response.StatusCode);

        if (!response.IsSuccessStatusCode)
        {
            this.logger?.LogError("Fehler beim Empfangen des SSE-Streams: {ResponseStatusCode}", response.StatusCode);
            return string.Empty;
        }

        await using var stream = await response.Content.ReadAsStreamAsync();
        using var reader = new StreamReader(stream);

        var resultBuilder = new StringBuilder();
        var eventCount = 0;
        var images = new Dictionary<string, List<string>>();

        this.logger?.LogInformation("Starting to read SSE events");

        while (!reader.EndOfStream && eventCount < maxEvents)
        {
            var line = await reader.ReadLineAsync();
            if (string.IsNullOrEmpty(line))
            {
                continue; // SSE Format trennt Events durch leere Zeilen
            }

            if (line.StartsWith("data:"))
            {
                var jsonContent = line[5..];

                try
                {
                    var sseEvent = JsonSerializer.Deserialize<SseEvent>(jsonContent);
                    if (sseEvent != null)
                    {
                        var content = await SseHandler.ProcessEventAsync(sseEvent);
                        resultBuilder.Append(content);
                        eventCount++;
                        this.logger?.LogDebug("Processed event {Count}:\t{Content}", eventCount, line);
                    }
                }
                catch (JsonException ex)
                {
                    this.logger?.LogWarning("Failed to parse JSON data: {Error}\nLine: {Line}", ex.Message, line);
                }
            }
        }

        var result = resultBuilder.ToString();
        this.logger?.LogInformation("Finished reading. Total events: {Count}, Result length: {Length} chars",
            eventCount, result.Length);

        if (images.Count > 0)
        {
            this.logger?.LogInformation("Extracted {Count} images", images.Count);
            // Hier könntest du die Bilder weiterverarbeiten
        }

        return result;
    }
}