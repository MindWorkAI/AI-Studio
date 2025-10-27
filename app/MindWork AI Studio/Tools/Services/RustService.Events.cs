using System.Text.Json;

using AIStudio.Tools.Rust;

namespace AIStudio.Tools.Services;

public partial class RustService
{
    /// <summary>
    /// Consume the Tauri event stream and forward relevant events to the message bus.
    /// </summary>
    /// <param name="stopToken">Cancellation token to stop the stream.</param>
    private async Task StartStreamTauriEvents(CancellationToken stopToken)
    {
        // Outer try-catch to handle cancellation:
        try
        {
            while (!stopToken.IsCancellationRequested)
            {
                // Inner try-catch to handle streaming issues:
                try
                {
                    // Open the event stream:
                    await using var stream = await this.http.GetStreamAsync("/events", stopToken);
                    
                    
                    // Read events line by line:
                    using var reader = new StreamReader(stream);
                    
                    // Read until the end of the stream or cancellation:
                    while(!reader.EndOfStream && !stopToken.IsCancellationRequested)
                    {
                        // Read the next line of JSON from the stream:
                        var line = await reader.ReadLineAsync(stopToken);
                        
                        // Skip empty lines:
                        if (string.IsNullOrWhiteSpace(line))
                            continue;
                        
                        // Deserialize the Tauri event:
                        var tauriEvent = JsonSerializer.Deserialize<TauriEvent>(line, this.jsonRustSerializerOptions);
                        
                        // Log the received event for debugging:
                        this.logger!.LogDebug("Received Tauri event: {Event}", tauriEvent);
                        
                        // Forward relevant events to the message bus:
                        if (tauriEvent != default && tauriEvent.EventType is not TauriEventType.NONE
                                and not TauriEventType.UNKNOWN and not TauriEventType.PING)
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