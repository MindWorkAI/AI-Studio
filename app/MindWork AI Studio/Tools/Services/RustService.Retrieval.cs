using System.Text;
using System.Text.Json;

namespace AIStudio.Tools.Services;

public sealed partial class RustService
{
    public async Task<string> ReadArbitraryFileData(string path, int maxChunks)
    {
        var requestUri = $"/retrieval/fs/extract?path={Uri.EscapeDataString(path)}";
        var request = new HttpRequestMessage(HttpMethod.Get, requestUri);
        var response = await this.http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);

        if (!response.IsSuccessStatusCode)
            return string.Empty;

        await using var stream = await response.Content.ReadAsStreamAsync();
        using var reader = new StreamReader(stream);

        var resultBuilder = new StringBuilder();
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
                var sseEvent = JsonSerializer.Deserialize<SseEvent>(jsonContent);
                if (sseEvent is not null)
                {
                    var content = SseHandler.ProcessEvent(sseEvent, false);
                    resultBuilder.Append(content);
                    chunkCount++;
                }
            }
            catch (JsonException)
            {
                this.logger?.LogError("Failed to deserialize SSE event: {JsonContent}", jsonContent); 
            }
            
        }
        
        return resultBuilder.ToString();
    }
}