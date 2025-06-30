using System.Text;
using System.Text.Json;

namespace AIStudio.Tools.Services;

public sealed partial class RustService
{
    public async Task<string> ReadArbitraryFileData(string path, int maxChunks)
    {
        var streamId = Guid.NewGuid().ToString();
        var requestUri = $"/retrieval/fs/extract?path={Uri.EscapeDataString(path)}&stream_id={streamId}";
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
                var sseEvent = JsonSerializer.Deserialize<ContentStreamSseEvent>(jsonContent);
                if (sseEvent is not null)
                {
                    var content = ContentStreamSseHandler.ProcessEvent(sseEvent, false);
                    if(content is not null)
                        resultBuilder.AppendLine(content);
                    
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