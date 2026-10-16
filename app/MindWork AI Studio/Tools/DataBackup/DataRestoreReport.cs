namespace AIStudio.Tools.DataBackup;

/// <summary>
/// How the runtime applied a restore at this start, as it writes it to DataRestoreFormat.RESULT_FILE_NAME.
/// </summary>
/// <remarks>
/// The points in time stay text: the runtime leaves them empty when it could not read the plan.
/// </remarks>
/// <param name="Outcome">How the restore ended.</param>
/// <param name="Detail">What happened, in English, for the log and for the support.</param>
/// <param name="PreparedAtUtc">When the restore was prepared, or an empty string.</param>
/// <param name="BackupCreatedAtUtc">When the backup was made, or an empty string.</param>
/// <param name="BackupAppVersion">The version of AI Studio which made the backup, or an empty string.</param>
/// <param name="FinishedAtUtc">When the runtime finished.</param>
public sealed record DataRestoreReport(DataRestoreOutcome Outcome, string Detail, string PreparedAtUtc, string BackupCreatedAtUtc, string BackupAppVersion, string FinishedAtUtc);