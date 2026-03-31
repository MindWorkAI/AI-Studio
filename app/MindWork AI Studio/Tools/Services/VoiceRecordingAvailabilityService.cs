namespace AIStudio.Tools.Services;

public sealed class VoiceRecordingAvailabilityService
{
    private readonly Lock stateLock = new();

    public bool IsAvailable { get; private set; } = true;

    public string? DisableReason { get; private set; }

    public bool TryDisable(string reason)
    {
        lock (this.stateLock)
        {
            if (!this.IsAvailable)
                return false;

            this.IsAvailable = false;
            this.DisableReason = reason;
            return true;
        }
    }
}
