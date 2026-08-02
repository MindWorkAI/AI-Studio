using System.Collections.Concurrent;
using System.Text.Json;

namespace AIStudio.Assistants.VisualBriefing;

/// <summary>
/// Publishes content-free live build snapshots while persistent records remain authoritative.
/// </summary>
public sealed class VisualBriefingBuildProgressService
{
    private readonly ConcurrentDictionary<Guid, VisualBriefingBuildRecord> latest = [];

    /// <summary>
    /// Raised whenever the latest safe build snapshot changes.
    /// </summary>
    public event Action<Guid>? Changed;

    /// <summary>
    /// Publishes the latest build record for one briefing.
    /// </summary>
    public void Publish(VisualBriefingBuildRecord build)
    {
        var snapshot = JsonSerializer.Deserialize<VisualBriefingBuildRecord>(
            JsonSerializer.Serialize(build, VisualBriefingJson.Canonical),
            VisualBriefingJson.Canonical)!;
        snapshot.Instruction = string.Empty;
        this.latest[build.BriefingId] = snapshot;
        this.Changed?.Invoke(build.BriefingId);
    }

    /// <summary>
    /// Gets the most recent live snapshot, if one exists.
    /// </summary>
    public VisualBriefingBuildRecord? GetLatest(Guid briefingId) =>
        this.latest.GetValueOrDefault(briefingId);
}
