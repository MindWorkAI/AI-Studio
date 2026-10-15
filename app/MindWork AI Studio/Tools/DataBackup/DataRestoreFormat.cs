namespace AIStudio.Tools.DataBackup;

/// <summary>
/// How a prepared restore waits for the next start, which applies it.
/// </summary>
/// <remarks>
/// This is the contract between the app, which prepares a restore, and the runtime, which applies it
/// before the app starts again, when no file is in use:
/// <list type="bullet">
/// <item>Below the data folder and below the configuration folder, PENDING_DIRECTORY holds what the
/// backup brings for that folder, in the layout of that folder. Staging lies on the same disk as its
/// target, so the runtime can move every element into place instead of copying it.</item>
/// <item>The plan, PLAN_FILE_NAME in the pending directory of the data folder, names the elements to
/// replace, cf. DataRestorePlan.</item>
/// <item>READY_FILE_NAME in the same directory comes last. Without it, the restore was not prepared
/// completely, and the runtime leaves everything as it is.</item>
/// </list>
/// What the runtime adds, cf. runtime/src/data_restore.rs:
/// <list type="bullet">
/// <item>PREVIOUS_DIRECTORY, below both folders, keeps what the restore replaced. The app removes it
/// once it loaded the restored settings.</item>
/// <item>A journal in the pending directory of the data folder lists every move. While it lies next to
/// READY_FILE_NAME, the runtime moves everything back at the next start, so the app never removes a
/// pending directory which holds READY_FILE_NAME.</item>
/// <item>RESULT_FILE_NAME in the data folder tells the app how the restore ended.</item>
/// </list>
/// Keep in sync with the runtime, which applies the restore.
/// </remarks>
public static class DataRestoreFormat
{
    /// <summary>
    /// The version of the plan this version of AI Studio writes.
    /// </summary>
    public const int PLAN_FORMAT_VERSION = 1;

    /// <summary>
    /// The directory, below the data folder and below the configuration folder, which holds what a
    /// prepared restore brings for that folder.
    /// </summary>
    public const string PENDING_DIRECTORY = ".restore-pending/";

    /// <summary>
    /// The plan of the restore, in the pending directory of the data folder.
    /// </summary>
    public const string PLAN_FILE_NAME = "restore.json";

    /// <summary>
    /// Marks a restore as prepared completely, in the pending directory of the data folder.
    /// </summary>
    public const string READY_FILE_NAME = "READY";

    /// <summary>
    /// The directory, below the data folder and below the configuration folder, which keeps what the
    /// runtime replaced, in the layout of that folder.
    /// </summary>
    public const string PREVIOUS_DIRECTORY = ".restore-previous/";

    /// <summary>
    /// How the runtime applied a restore, in the data folder.
    /// </summary>
    public const string RESULT_FILE_NAME = ".restore-result.json";
}