namespace AIStudio.Tools.AIJobs;

public sealed record AIJobSnapshot
{
    public Guid JobId { get; init; }

    public AIJobKind Kind { get; init; }

    public Guid SubjectId { get; init; }

    public Guid? ParentJobId { get; init; }

    public Guid RootJobId { get; init; }

    public int Priority { get; init; }

    public bool IsForeground { get; init; }

    public AIJobSchedulingClass SchedulingClass { get; init; }

    public AIJobStatus Status { get; init; }

    public string Title { get; init; } = string.Empty;

    public string ProviderId { get; init; } = string.Empty;

    public string ModelId { get; init; } = string.Empty;

    public DateTimeOffset UpdatedAt { get; init; }

    public string ErrorMessage { get; init; } = string.Empty;

    /// <summary>
    /// Whether the user has seen how the job ended.
    /// </summary>
    /// <remarks>
    /// True right away when the job ended while its subject was in the foreground, such as the open
    /// chat; otherwise once its subject comes to the foreground afterward, see
    /// <see cref="AIJobService.SetForegroundAsync"/>. False while the job runs. Kept in memory only,
    /// like the outcome itself.
    /// </remarks>
    public bool IsOutcomeSeen { get; init; }

    public bool IsActive => this.Status is AIJobStatus.QUEUED or AIJobStatus.WAITING_FOR_REMOTE or AIJobStatus.RUNNING;
}