namespace AIStudio.Tools.DataBackup;

/// <summary>
/// How the runtime applied a restore, cf. DataRestoreReport.
/// </summary>
public enum DataRestoreOutcome
{
    /// <summary>
    /// The restored data are in place.
    /// </summary>
    APPLIED,

    /// <summary>
    /// The runtime did not start the restore, since the plan or the folders did not allow it. All data
    /// stayed as they were.
    /// </summary>
    REFUSED,

    /// <summary>
    /// The restore failed, and the runtime moved all data back.
    /// </summary>
    ROLLED_BACK,

    /// <summary>
    /// The restore failed, and moving the data back failed as well. The runtime tries again at every
    /// start.
    /// </summary>
    ROLLBACK_FAILED,
}