namespace AIStudio.Tools.DataBackup;

/// <summary>
/// What the runtime replaces at the next start, cf. DataRestoreFormat.
/// </summary>
/// <param name="FormatVersion">The version of the plan, cf. DataRestoreFormat.PLAN_FORMAT_VERSION.</param>
/// <param name="PreparedAtUtc">When the restore was prepared.</param>
/// <param name="BackupCreatedAtUtc">When the backup was made.</param>
/// <param name="BackupAppVersion">The version of AI Studio which made the backup.</param>
/// <param name="Elements">The elements to replace. An element the backup holds nothing for stays as it is.</param>
public sealed record DataRestorePlan(int FormatVersion, DateTimeOffset PreparedAtUtc, DateTimeOffset BackupCreatedAtUtc, string BackupAppVersion, IReadOnlyList<DataRestoreElement> Elements);