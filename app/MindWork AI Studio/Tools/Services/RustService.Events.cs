using System.Text.Json;

using AIStudio.Tools.Rust;

namespace AIStudio.Tools.Services;

public partial class RustService
{
    private async Task StartStreamTauriEvents(CancellationToken stopToken)
    {
        try
        {
            while (!stopToken.IsCancellationRequested)
            {
                try
                {
                    await using var stream = await this.http.GetStreamAsync("/events", stopToken);
                    using var reader = new StreamReader(stream);
                    while(!reader.EndOfStream)
                    {
                        var line = await reader.ReadLineAsync(stopToken);
                        if (string.IsNullOrWhiteSpace(line))
                            continue;
                        
                        var tauriEvent = JsonSerializer.Deserialize<TauriEvent>(line, this.jsonRustSerializerOptions);
                        if (tauriEvent != default)
                            await MessageBus.INSTANCE.SendMessage(null, Event.TAURI_EVENT_RECEIVED, tauriEvent);
                    }
                }
                
                // The cancellation token was triggered, exit the loop:
                catch (OperationCanceledException)
                {
                    break;
                }
                
                // Some other error occurred, log it and retry after a delay:
                catch (Exception e)
                {
                    this.logger!.LogError("Error while streaming Tauri events: {Message}", e.Message);
                    await Task.Delay(TimeSpan.FromSeconds(3), stopToken);
                }
            }
        }
        
        // The cancellation token was triggered, exit the method:
        catch (OperationCanceledException)
        {
        }
        
        this.logger!.LogWarning("Stopped streaming Tauri events.");
    }
}