namespace AIStudio.Tools.Services;

/// <summary>
/// How a round of RuntimeOutageCheck probes the runtime.
/// </summary>
/// <param name="Probes">How many probes a round makes at most, before the runtime counts as gone.</param>
/// <param name="Interval">How long the round waits between two probes.</param>
public readonly record struct RuntimeProbeSchedule(int Probes, TimeSpan Interval)
{
    /// <summary>
    /// Seven probes, ten seconds apart: the runtime counts as gone once it did not answer for a minute.
    /// </summary>
    public static readonly RuntimeProbeSchedule DEFAULT = new(7, TimeSpan.FromSeconds(10));
}