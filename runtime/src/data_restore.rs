//! Applies a restore of a backup which the app prepared, before anything opens the data.
//!
//! While AI Studio runs, its files are open, and on Windows, they cannot be replaced at all. The app
//! therefore unpacks a backup next to the data and marks it as ready. The next start puts it in place,
//! before the .NET server and the vector database open a single file.
//!
//! The contract with the app, cf. `DataRestoreFormat` in the app; keep both in sync:
//!
//! - `.restore-pending/`, below the data folder and below the configuration folder, holds what the
//!   backup brings for that folder, in the layout of that folder.
//! - `.restore-pending/restore.json` in the data folder names the elements to replace, each with the
//!   paths below it which stay as they are.
//! - `.restore-pending/READY` in the data folder comes last. Without it, the restore was not prepared
//!   completely, and nothing happens.
//!
//! What the runtime adds to it:
//!
//! - `.restore-previous/`, below both folders, keeps what the restore replaced, in the layout of that
//!   folder. The app removes it once it loaded the restored settings.
//! - `.restore-pending/journal.json` in the data folder lists every move before the first one happens,
//!   and how many of them started. A start which finds it next to `READY` knows that an earlier one
//!   stopped in the middle, and moves back what that one moved.
//! - `.restore-result.json` in the data folder tells the app how the restore ended.
//!
//! Every move renames a file or folder within the same folder, and thus on the same disk: it is atomic,
//! it takes no time for any amount of data, and it can be undone.

use std::collections::HashSet;
use std::fs;
use std::io;
use std::io::Write;
use std::path::{Path, PathBuf};
use std::thread;
use std::time::Duration;
use log::{error, info, warn};
use serde::{Deserialize, Serialize};

const PENDING_DIRECTORY: &str = ".restore-pending";
const PREVIOUS_DIRECTORY: &str = ".restore-previous";
const PLAN_FILE_NAME: &str = "restore.json";
const READY_FILE_NAME: &str = "READY";
const JOURNAL_FILE_NAME: &str = "journal.json";
const JOURNAL_TEMPORARY_FILE_NAME: &str = "journal.json.tmp";
const RESULT_FILE_NAME: &str = ".restore-result.json";

/// The version of the plan this runtime reads.
const PLAN_FORMAT_VERSION: i32 = 1;

const SETTINGS_FILE_NAME: &str = "settings.json";

/// The elements a restore may replace: those which the list of the backup contents in the app
/// (`DataBackupContents`) includes. The runtime refuses a plan which names anything else, so that
/// whatever goes wrong in the app, a restore never replaces the logs, the downloaded tools, or the
/// data folder as a whole.
const REPLACEABLE_ELEMENTS: [(Location, &str); 11] = [
    (Location::ConfigDirectory, SETTINGS_FILE_NAME),
    (Location::DataDirectory, "workspaces/"),
    (Location::DataDirectory, "tempChats/"),
    (Location::DataDirectory, "databases/provenance/"),
    (Location::DataDirectory, "plugins/"),
    (Location::DataDirectory, "tokenizers/"),
    (Location::DataDirectory, "visualBriefings/"),
    (Location::DataDirectory, "databases/sqlite/"),
    (Location::DataDirectory, "databases/vector_database/stores/"),
    (Location::DataDirectory, "mediaTranscripts/"),
    (Location::DataDirectory, "glossaries/"),
];

/// Paths which a restore always keeps, whatever the plan says: the plugins which come with AI Studio
/// or from the organization. The plan adds the plugins of the organization which lie among the plugins
/// of the user, since only the app knows them.
const ALWAYS_KEPT: [&str; 3] = ["plugins/.internal/", "plugins/.config/", "plugins/.config-tests/"];

/// The settings file which every version of AI Studio keeps for itself, `settings.v6.json` for
/// example. When `settings.json` is older than the app, the app prefers the file of its own version,
/// which would undo a restore of older settings. These files therefore go together with
/// `settings.json`.
const SETTINGS_OF_ONE_VERSION_PREFIX: &str = "settings.v";
const SETTINGS_OF_ONE_VERSION_SUFFIX: &str = ".json";

/// How long to wait before each further attempt of a move. On Windows, a file stays locked for a
/// moment after the process which held it ended, and virus scanners or the search indexer lock files
/// briefly, too.
const MOVE_RETRY_DELAYS: [Duration; 6] = [
    Duration::from_millis(100),
    Duration::from_millis(200),
    Duration::from_millis(400),
    Duration::from_millis(800),
    Duration::from_millis(1_600),
    Duration::from_millis(3_200),
];

/// The folder a path lies in.
#[derive(Clone, Copy, Debug, PartialEq, Eq, Hash, Serialize, Deserialize)]
#[serde(rename_all = "SCREAMING_SNAKE_CASE")]
enum Location {
    DataDirectory,
    ConfigDirectory,
}

/// What the app prepared, cf. `DataRestorePlan` in the app.
#[derive(Debug, Deserialize)]
struct RestorePlan {
    format_version: i32,
    prepared_at_utc: String,
    backup_created_at_utc: String,
    backup_app_version: String,
    elements: Vec<RestoreElement>,
}

/// One element to replace, cf. `DataRestoreElement` in the app.
#[derive(Debug, Deserialize)]
struct RestoreElement {
    location: Location,

    /// The path within its folder, with forward slashes. A folder ends with a slash.
    relative_path: String,

    /// The paths below the element which stay as they are, relative to the same folder.
    keep: Vec<String>,
}

/// Renames one file or folder. Both paths are relative to the folder, with forward slashes.
#[derive(Clone, Debug, PartialEq, Eq, Serialize, Deserialize)]
struct Move {
    location: Location,
    from: String,
    to: String,
}

impl Move {
    fn new(location: Location, from: &str, to: &str) -> Self {
        Move { location, from: from.to_string(), to: to.to_string() }
    }
}

/// Every move of a restore, written before the first one happens, and written anew before every
/// further one and after every one which was moved back.
///
/// The file system alone cannot tell which moves happened: a move which brings back a kept plugin finds
/// its destination taken before the restore began. Only the last move the journal counts may or may
/// not have happened, and for that one, the file system tells.
#[derive(Debug, Serialize, Deserialize)]
struct Journal {
    moves: Vec<Move>,

    /// How many moves started. All of them happened, except perhaps the last one.
    started: usize,
}

/// How a restore ended.
#[derive(Clone, Copy, Debug, PartialEq, Eq, Serialize, Deserialize)]
#[serde(rename_all = "SCREAMING_SNAKE_CASE")]
enum RestoreOutcome {
    /// The restored data are in place. What they replaced lies in `.restore-previous`.
    Applied,

    /// The restore did not start, since the plan or the folders did not allow it. Nothing changed.
    Refused,

    /// The restore started, but could not finish. Everything was moved back.
    RolledBack,

    /// Moving back failed as well. The journal stays, and the next start tries again.
    RollbackFailed,
}

/// What the app reads at its next start, in `.restore-result.json`.
#[derive(Debug, Serialize, Deserialize)]
struct RestoreResult {
    outcome: RestoreOutcome,

    /// What happened, in English, for the log. The app tells the user in its own words.
    detail: String,

    prepared_at_utc: String,
    backup_created_at_utc: String,
    backup_app_version: String,
    finished_at_utc: String,
}

/// How a restore ended, and why.
struct RestoreEnd {
    outcome: RestoreOutcome,
    detail: String,
}

impl RestoreEnd {
    fn new(outcome: RestoreOutcome, detail: impl Into<String>) -> Self {
        RestoreEnd { outcome, detail: detail.into() }
    }
}

/// The two folders a restore works in.
struct Folders<'a> {
    data: &'a Path,
    config: &'a Path,
}

impl Folders<'_> {
    fn root(&self, location: Location) -> &Path {
        match location {
            Location::DataDirectory => self.data,
            Location::ConfigDirectory => self.config,
        }
    }

    /// The pending directory of the data folder, which holds the plan, the journal, and `READY`.
    fn pending(&self) -> PathBuf {
        self.data.join(PENDING_DIRECTORY)
    }
}

/// Applies a restore which the app prepared, or moves back one which an earlier start left unfinished.
///
/// Call it before anything opens a file in the data folder. In production, the runtime starts the
/// .NET server only afterward. In the development environment, the IDE starts the .NET server on its
/// own, so start it after the runtime when you try a restore.
pub fn apply_pending_restore(data_directory: &Path, config_directory: &Path) {
    let folders = Folders { data: data_directory, config: config_directory };
    apply_pending_restore_in(&folders, &MOVE_RETRY_DELAYS);
}

/// Applies a pending restore, and returns how it ended, or `None` when there was nothing to do.
fn apply_pending_restore_in(folders: &Folders, retry_delays: &[Duration]) -> Option<RestoreOutcome> {
    let pending = folders.pending();
    let is_ready = exists(&pending.join(READY_FILE_NAME));
    let journal_path = pending.join(JOURNAL_FILE_NAME);

    if exists(&journal_path) {
        if !is_ready {
            info!(Source = "Restore"; "Finishing the clean-up of the restore which an earlier start applied.");
            clean_up_after_success(folders);
            return None;
        }

        warn!(Source = "Restore"; "An earlier start stopped while it applied a restore. Moving the replaced data back.");
        let plan = read_plan(&pending).ok();
        let end = match read_journal(&journal_path) {
            Ok(mut journal) => roll_back(folders, &mut journal, retry_delays, "AI Studio stopped while it applied the restore."),
            Err(error) => RestoreEnd::new(RestoreOutcome::RollbackFailed, format!("The journal of the restore could not be read: {error}")),
        };

        return Some(finish(folders, plan.as_ref(), end, retry_delays));
    }

    if !is_ready {
        return None;
    }

    info!(Source = "Restore"; "Applying the prepared restore of a backup.");
    let (plan, end) = match read_plan(&pending) {
        Ok(plan) => {
            let end = apply(folders, &plan, retry_delays);
            (Some(plan), end)
        },

        Err(error) => (None, RestoreEnd::new(RestoreOutcome::Refused, error)),
    };

    Some(finish(folders, plan.as_ref(), end, retry_delays))
}

/// Moves the restored elements into place, and back again when a move fails.
fn apply(folders: &Folders, plan: &RestorePlan, retry_delays: &[Duration]) -> RestoreEnd {
    let mut journal = match begin(folders, plan, retry_delays) {
        Ok(journal) => journal,
        Err(detail) => return RestoreEnd::new(RestoreOutcome::Refused, detail),
    };

    while journal.started < journal.moves.len() {
        if let Err(detail) = execute_next(folders, &mut journal, retry_delays) {
            error!(Source = "Restore"; "{detail} Moving the replaced data back.");
            return roll_back(folders, &mut journal, retry_delays, &detail);
        }
    }

    RestoreEnd::new(RestoreOutcome::Applied, format!("Replaced {} elements with {} moves.", plan.elements.len(), journal.moves.len()))
}

/// Determines the moves of a restore and writes them into the journal, before any of them happens.
fn begin(folders: &Folders, plan: &RestorePlan, retry_delays: &[Duration]) -> Result<Journal, String> {
    let journal = Journal { moves: plan_moves(folders, plan)?, started: 0 };
    write_journal(&folders.pending(), &journal, retry_delays)?;
    Ok(journal)
}

/// Executes the next move, once the journal counts it.
fn execute_next(folders: &Folders, journal: &mut Journal, retry_delays: &[Duration]) -> Result<(), String> {
    let step = journal.moves[journal.started].clone();
    journal.started += 1;
    write_journal(&folders.pending(), journal, retry_delays)?;

    execute(folders, &step, retry_delays)
        .map_err(|error| format!("Could not move '{}' to '{}': {error}", step.from, step.to))
}

/// Determines every move of a restore, in the order they happen. For each element: move the current
/// one aside, move the restored one in, and move back what stays.
fn plan_moves(folders: &Folders, plan: &RestorePlan) -> Result<Vec<Move>, String> {
    if plan.format_version != PLAN_FORMAT_VERSION {
        return Err(format!("The plan has the format version {}, but this runtime reads version {PLAN_FORMAT_VERSION}.", plan.format_version));
    }

    // Empty folders are what moving back a restore may leave behind; files are the data of the user:
    for root in [folders.data, folders.config] {
        let previous = root.join(PREVIOUS_DIRECTORY);
        if !remove_empty_directories(&previous) {
            return Err(format!("'{}' still keeps the data which an earlier restore replaced.", previous.display()));
        }
    }

    let mut planned_elements = HashSet::new();
    let mut moves = Vec::new();
    for element in &plan.elements {
        let is_replaceable = REPLACEABLE_ELEMENTS.iter().any(|(location, path)| *location == element.location && *path == element.relative_path);
        if !is_replaceable {
            return Err(format!("A restore may not replace '{}'.", element.relative_path));
        }

        if !planned_elements.insert((element.location, element.relative_path.as_str())) {
            return Err(format!("The plan names '{}' twice.", element.relative_path));
        }

        let root = folders.root(element.location);
        let target = element.relative_path.trim_end_matches('/');
        let staged = format!("{PENDING_DIRECTORY}/{target}");
        let previous = format!("{PREVIOUS_DIRECTORY}/{target}");

        if exists(&resolve(root, target)) {
            moves.push(Move::new(element.location, target, &previous));
        }

        if element.location == Location::ConfigDirectory && target == SETTINGS_FILE_NAME {
            for name in settings_of_single_versions(root)? {
                moves.push(Move::new(element.location, &name, &format!("{PREVIOUS_DIRECTORY}/{name}")));
            }
        }

        if exists(&resolve(root, &staged)) {
            moves.push(Move::new(element.location, &staged, target));
        }

        for kept in kept_paths(element)? {
            if !exists(&resolve(root, &kept)) {
                continue;
            }

            let below = &kept[target.len()..];
            if exists(&resolve(root, &format!("{staged}{below}"))) {
                return Err(format!("The backup brings '{kept}', which the restore keeps."));
            }

            moves.push(Move::new(element.location, &format!("{previous}{below}"), &kept));
        }
    }

    Ok(moves)
}

/// The paths below an element which stay as they are, without a trailing slash. A path below another
/// one moves along with that one, so only the outermost ones remain.
fn kept_paths(element: &RestoreElement) -> Result<Vec<String>, String> {
    let element_path = element.relative_path.as_str();
    let always_kept = ALWAYS_KEPT.iter().copied().filter(|path| path.starts_with(element_path));

    let mut kept = Vec::new();
    for path in element.keep.iter().map(String::as_str).chain(always_kept) {
        let lies_below = element_path.ends_with('/') && path.len() > element_path.len() && path.starts_with(element_path);
        if !lies_below || !is_safe_relative_path(path) {
            return Err(format!("A restore cannot keep '{path}' of '{element_path}'."));
        }

        kept.push(path.trim_end_matches('/').to_string());
    }

    kept.sort();
    kept.dedup();

    let outermost = kept.iter()
        .filter(|path| !kept.iter().any(|other| path.starts_with(&format!("{other}/"))))
        .cloned()
        .collect();

    Ok(outermost)
}

/// Whether a path from the plan stays within its folder: relative, with forward slashes, and without
/// `.` or `..`.
fn is_safe_relative_path(path: &str) -> bool {
    !path.is_empty()
        && !path.starts_with('/')
        && !path.contains('\\')
        && !path.contains(':')
        && path.trim_end_matches('/').split('/').all(|segment| !segment.is_empty() && segment != "." && segment != "..")
}

/// The settings files of single versions in the configuration folder, cf. SETTINGS_OF_ONE_VERSION_PREFIX.
fn settings_of_single_versions(config_directory: &Path) -> Result<Vec<String>, String> {
    let entries = match fs::read_dir(config_directory) {
        Ok(entries) => entries,
        Err(error) if error.kind() == io::ErrorKind::NotFound => return Ok(Vec::new()),
        Err(error) => return Err(format!("Could not read '{}': {error}", config_directory.display())),
    };

    let mut names = Vec::new();
    for entry in entries {
        let entry = entry.map_err(|error| format!("Could not read '{}': {error}", config_directory.display()))?;
        let is_file = entry.file_type().map(|file_type| file_type.is_file()).unwrap_or(false);
        let file_name = entry.file_name();
        if let Some(name) = file_name.to_str() && is_file && is_settings_of_single_version(name) {
            names.push(name.to_string());
        }
    }

    names.sort();
    Ok(names)
}

fn is_settings_of_single_version(file_name: &str) -> bool {
    file_name.len() > SETTINGS_OF_ONE_VERSION_PREFIX.len() + SETTINGS_OF_ONE_VERSION_SUFFIX.len()
        && file_name.starts_with(SETTINGS_OF_ONE_VERSION_PREFIX)
        && file_name.ends_with(SETTINGS_OF_ONE_VERSION_SUFFIX)
}

/// Executes one move.
fn execute(folders: &Folders, step: &Move, retry_delays: &[Duration]) -> io::Result<()> {
    let root = folders.root(step.location);
    move_path(&resolve(root, &step.from), &resolve(root, &step.to), retry_delays)
}

/// Undoes the last move the journal counts. That one may not have happened: then its source is still in
/// place, and its destination is free.
fn undo(folders: &Folders, step: &Move, retry_delays: &[Duration]) -> io::Result<()> {
    let root = folders.root(step.location);
    let from = resolve(root, &step.from);
    let to = resolve(root, &step.to);
    if exists(&from) || !exists(&to) {
        return Ok(());
    }

    move_path(&to, &from, retry_delays)
}

/// Undoes the moves the journal counts, in the reverse order. It stops at the first move which cannot
/// be undone: every further one could otherwise carry along data which that move left in the wrong
/// place. The journal then names that move as the last one, and the next start tries again from there.
fn roll_back(folders: &Folders, journal: &mut Journal, retry_delays: &[Duration], cause: &str) -> RestoreEnd {
    while journal.started > 0 {
        let step = &journal.moves[journal.started - 1];
        if let Err(error) = undo(folders, step, retry_delays) {
            return RestoreEnd::new(RestoreOutcome::RollbackFailed, format!("{cause} Could not move '{}' back to '{}': {error}", step.to, step.from));
        }

        journal.started -= 1;
        if let Err(error) = write_journal(&folders.pending(), journal, retry_delays) {
            return RestoreEnd::new(RestoreOutcome::RollbackFailed, format!("{cause} {error}"));
        }
    }

    RestoreEnd::new(RestoreOutcome::RolledBack, cause)
}

/// Writes the result and leaves the folders in the state the outcome calls for.
fn finish(folders: &Folders, plan: Option<&RestorePlan>, end: RestoreEnd, retry_delays: &[Duration]) -> RestoreOutcome {
    match end.outcome {
        RestoreOutcome::Applied => info!(Source = "Restore"; "The restore was applied: {}", end.detail),
        RestoreOutcome::Refused => warn!(Source = "Restore"; "The restore was refused, nothing changed: {}", end.detail),
        RestoreOutcome::RolledBack => warn!(Source = "Restore"; "The restore failed and was moved back: {}", end.detail),
        RestoreOutcome::RollbackFailed => error!(Source = "Restore"; "The restore failed, and moving it back failed as well. The next start tries again: {}", end.detail),
    }

    write_result(folders, plan, &end);
    if end.outcome == RestoreOutcome::RollbackFailed {
        return end.outcome;
    }

    //
    // Without READY, the next start does not try again. While the journal remains next to READY, it
    // moves everything back instead, so READY goes first:
    //
    if let Err(error) = remove_file_if_present(&folders.pending().join(READY_FILE_NAME), retry_delays) {
        error!(Source = "Restore"; "Could not remove the mark of the prepared restore: {error}");
        return end.outcome;
    }

    match end.outcome {
        RestoreOutcome::Applied => clean_up_after_success(folders),

        //
        // The restored data went back to the pending directories, and the app removes them. What
        // remains of the directories for the replaced data is empty:
        //
        RestoreOutcome::RolledBack => {
            if let Err(error) = remove_file_if_present(&folders.pending().join(JOURNAL_FILE_NAME), retry_delays) {
                error!(Source = "Restore"; "Could not remove the journal of the restore: {error}");
            }

            for root in [folders.data, folders.config] {
                remove_empty_directories(&root.join(PREVIOUS_DIRECTORY));
            }
        },

        RestoreOutcome::Refused | RestoreOutcome::RollbackFailed => {},
    }

    end.outcome
}

/// Removes what remains of the pending directories once the restored data are in place: the plan, the
/// journal, and empty folders. Anything else stays for the app.
fn clean_up_after_success(folders: &Folders) {
    let pending = folders.pending();
    for file_name in [JOURNAL_FILE_NAME, PLAN_FILE_NAME] {
        if let Err(error) = remove_file_if_present(&pending.join(file_name), &[]) {
            warn!(Source = "Restore"; "Could not remove '{file_name}' of the applied restore: {error}");
        }
    }

    for root in [folders.data, folders.config] {
        let pending = root.join(PENDING_DIRECTORY);
        if !remove_empty_directories(&pending) {
            warn!(Source = "Restore"; "'{}' still holds files after the restore. The app removes them.", pending.display());
        }
    }
}

fn write_result(folders: &Folders, plan: Option<&RestorePlan>, end: &RestoreEnd) {
    let result = RestoreResult {
        outcome: end.outcome,
        detail: end.detail.clone(),
        prepared_at_utc: plan.map(|plan| plan.prepared_at_utc.clone()).unwrap_or_default(),
        backup_created_at_utc: plan.map(|plan| plan.backup_created_at_utc.clone()).unwrap_or_default(),
        backup_app_version: plan.map(|plan| plan.backup_app_version.clone()).unwrap_or_default(),
        finished_at_utc: chrono::Utc::now().to_rfc3339(),
    };

    let path = folders.data.join(RESULT_FILE_NAME);
    let written = serde_json::to_vec_pretty(&result)
        .map_err(io::Error::other)
        .and_then(|json| fs::write(&path, json));

    if let Err(error) = written {
        error!(Source = "Restore"; "Could not write the result of the restore to '{}': {error}", path.display());
    }
}

fn read_plan(pending: &Path) -> Result<RestorePlan, String> {
    let path = pending.join(PLAN_FILE_NAME);
    let json = fs::read(&path).map_err(|error| format!("Could not read the plan '{}': {error}", path.display()))?;
    serde_json::from_slice(&json).map_err(|error| format!("Could not read the plan '{}': {error}", path.display()))
}

fn read_journal(path: &Path) -> Result<Journal, String> {
    let json = fs::read(path).map_err(|error| error.to_string())?;
    serde_json::from_slice(&json).map_err(|error| error.to_string())
}

/// Writes the journal under another name first, so that it is either complete or the one before.
fn write_journal(pending: &Path, journal: &Journal, retry_delays: &[Duration]) -> Result<(), String> {
    let temporary_path = pending.join(JOURNAL_TEMPORARY_FILE_NAME);
    let written = serde_json::to_vec_pretty(journal)
        .map_err(io::Error::other)
        .and_then(|json| {
            let mut file = fs::File::create(&temporary_path)?;
            file.write_all(&json)?;
            file.sync_all()
        })
        .and_then(|_| with_retries(retry_delays, || fs::rename(&temporary_path, pending.join(JOURNAL_FILE_NAME))));

    written.map_err(|error| format!("Could not write the journal of the restore: {error}"))
}

/// Renames a file or folder, and never replaces anything at the destination.
fn move_path(from: &Path, to: &Path, retry_delays: &[Duration]) -> io::Result<()> {
    if exists(to) {
        return Err(io::Error::new(io::ErrorKind::AlreadyExists, format!("'{}' exists already", to.display())));
    }

    if let Some(parent) = to.parent() {
        fs::create_dir_all(parent)?;
    }

    with_retries(retry_delays, || fs::rename(from, to))
}

fn remove_file_if_present(path: &Path, retry_delays: &[Duration]) -> io::Result<()> {
    match with_retries(retry_delays, || fs::remove_file(path)) {
        Err(error) if error.kind() != io::ErrorKind::NotFound => Err(error),
        _ => Ok(()),
    }
}

/// Runs an operation again after each of the given delays, as long as it fails for another reason
/// than a missing file.
fn with_retries(retry_delays: &[Duration], mut operation: impl FnMut() -> io::Result<()>) -> io::Result<()> {
    let mut delays = retry_delays.iter();
    loop {
        let error = match operation() {
            Ok(()) => return Ok(()),
            Err(error) => error,
        };

        match delays.next() {
            Some(delay) if error.kind() != io::ErrorKind::NotFound => {
                warn!(Source = "Restore"; "{error}. Trying again in {delay:?}.");
                thread::sleep(*delay);
            },

            _ => return Err(error),
        }
    }
}

/// Removes a folder with all folders below it, as long as none of them holds a file. Returns whether
/// the folder is gone.
fn remove_empty_directories(path: &Path) -> bool {
    let Ok(metadata) = fs::symlink_metadata(path) else {
        return true;
    };

    if !metadata.is_dir() {
        return false;
    }

    let Ok(entries) = fs::read_dir(path) else {
        return false;
    };

    let mut is_empty = true;
    for entry in entries.flatten() {
        if !remove_empty_directories(&entry.path()) {
            is_empty = false;
        }
    }

    is_empty && fs::remove_dir(path).is_ok()
}

/// Whether a file or folder exists. A link counts as itself, wherever it points to.
fn exists(path: &Path) -> bool {
    fs::symlink_metadata(path).is_ok()
}

/// The path of a relative path with forward slashes, below the given folder.
fn resolve(root: &Path, relative_path: &str) -> PathBuf {
    relative_path
        .split('/')
        .filter(|segment| !segment.is_empty())
        .fold(root.to_path_buf(), |path, segment| path.join(segment))
}

#[cfg(test)]
mod tests {
    use super::*;
    use tempfile::TempDir;

    /// The plugins of the organization which lie among the plugins of the user, as the app names them.
    const ORGANIZATION_PLUGIN: &str = "plugins/company-assistant/";

    /// A data folder and a configuration folder in a temporary directory of their own.
    struct TestFolders {
        _directory: TempDir,
        data: PathBuf,
        config: PathBuf,
    }

    impl TestFolders {
        fn new() -> Self {
            let directory = tempfile::tempdir().unwrap();
            let data = directory.path().join("data");
            let config = directory.path().join("config");
            fs::create_dir_all(&data).unwrap();
            fs::create_dir_all(&config).unwrap();
            TestFolders { _directory: directory, data, config }
        }

        fn folders(&self) -> Folders<'_> {
            Folders { data: &self.data, config: &self.config }
        }

        fn write(&self, location: Location, relative_path: &str, content: &str) {
            let path = resolve(self.folders().root(location), relative_path);
            fs::create_dir_all(path.parent().unwrap()).unwrap();
            fs::write(path, content).unwrap();
        }

        fn read(&self, location: Location, relative_path: &str) -> Option<String> {
            fs::read_to_string(resolve(self.folders().root(location), relative_path)).ok()
        }

        fn exists(&self, location: Location, relative_path: &str) -> bool {
            exists(&resolve(self.folders().root(location), relative_path))
        }

        fn result(&self) -> RestoreResult {
            serde_json::from_slice(&fs::read(self.data.join(RESULT_FILE_NAME)).unwrap()).unwrap()
        }

        /// The data of a user who has used AI Studio for a while.
        fn write_current_data(&self) {
            self.write(Location::ConfigDirectory, "settings.json", "current settings");
            self.write(Location::ConfigDirectory, "settings.v6.json", "current settings of v6");
            self.write(Location::DataDirectory, "workspaces/current/chat/thread.json", "current chat");
            self.write(Location::DataDirectory, "plugins/my-old-assistant/plugin.lua", "current plugin");
            self.write(Location::DataDirectory, "plugins/.internal/languages/plugin.lua", "built-in plugin");
            self.write(Location::DataDirectory, "plugins/.config/organization/plugin.lua", "organization plugin");
            self.write(Location::DataDirectory, "plugins/company-assistant/plugin.lua", "managed plugin");
            self.write(Location::DataDirectory, "events.log", "log");
        }

        /// A restore of a backup with other settings, another chat, and another plugin, as the app
        /// prepares it.
        fn prepare_restore(&self, elements: &str) {
            self.write(Location::ConfigDirectory, ".restore-pending/settings.json", "restored settings");
            self.write(Location::DataDirectory, ".restore-pending/workspaces/restored/chat/thread.json", "restored chat");
            self.write(Location::DataDirectory, ".restore-pending/plugins/my-assistant/plugin.lua", "restored plugin");
            self.write(Location::DataDirectory, ".restore-pending/restore.json", &plan_json(elements));
            self.write(Location::DataDirectory, ".restore-pending/READY", "2026-10-15T08:00:00.0000000+00:00");
        }
    }

    /// A plan as the app writes it.
    fn plan_json(elements: &str) -> String {
        format!(r#"{{
  "format_version": 1,
  "prepared_at_utc": "2026-10-15T08:00:00.0000000+00:00",
  "backup_created_at_utc": "2026-10-01T17:30:00.0000000+00:00",
  "backup_app_version": "26.10.1",
  "elements": [{elements}]
}}"#)
    }

    /// The elements of a full restore, as the app names them.
    fn all_elements() -> String {
        format!(r#"
    {{ "location": "CONFIG_DIRECTORY", "relative_path": "settings.json", "keep": [] }},
    {{ "location": "DATA_DIRECTORY", "relative_path": "workspaces/", "keep": [] }},
    {{ "location": "DATA_DIRECTORY", "relative_path": "plugins/", "keep": ["plugins/.internal/", "plugins/.config/", "plugins/.config-tests/", "plugins/.lock", "{ORGANIZATION_PLUGIN}"] }}"#)
    }

    /// Checks that the data are exactly as before the restore.
    fn assert_current_data_untouched(test: &TestFolders) {
        assert_eq!(test.read(Location::ConfigDirectory, "settings.json").as_deref(), Some("current settings"));
        assert_eq!(test.read(Location::ConfigDirectory, "settings.v6.json").as_deref(), Some("current settings of v6"));
        assert_eq!(test.read(Location::DataDirectory, "workspaces/current/chat/thread.json").as_deref(), Some("current chat"));
        assert_eq!(test.read(Location::DataDirectory, "plugins/my-old-assistant/plugin.lua").as_deref(), Some("current plugin"));
        assert_eq!(test.read(Location::DataDirectory, "plugins/.internal/languages/plugin.lua").as_deref(), Some("built-in plugin"));
        assert_eq!(test.read(Location::DataDirectory, "plugins/company-assistant/plugin.lua").as_deref(), Some("managed plugin"));
        assert!(!test.exists(Location::DataDirectory, "workspaces/restored"));
        assert!(!test.exists(Location::DataDirectory, "plugins/my-assistant"));
    }

    #[test]
    fn a_restore_replaces_the_elements_and_keeps_what_belongs_to_this_computer() {
        let test = TestFolders::new();
        test.write_current_data();
        test.prepare_restore(&all_elements());

        let outcome = apply_pending_restore_in(&test.folders(), &[]);

        assert_eq!(outcome, Some(RestoreOutcome::Applied));
        assert_eq!(test.read(Location::ConfigDirectory, "settings.json").as_deref(), Some("restored settings"));
        assert!(!test.exists(Location::ConfigDirectory, "settings.v6.json"), "The app would prefer it over older restored settings.");
        assert_eq!(test.read(Location::DataDirectory, "workspaces/restored/chat/thread.json").as_deref(), Some("restored chat"));
        assert!(!test.exists(Location::DataDirectory, "workspaces/current"));
        assert_eq!(test.read(Location::DataDirectory, "plugins/my-assistant/plugin.lua").as_deref(), Some("restored plugin"));
        assert!(!test.exists(Location::DataDirectory, "plugins/my-old-assistant"));

        assert_eq!(test.read(Location::DataDirectory, "plugins/.internal/languages/plugin.lua").as_deref(), Some("built-in plugin"));
        assert_eq!(test.read(Location::DataDirectory, "plugins/.config/organization/plugin.lua").as_deref(), Some("organization plugin"));
        assert_eq!(test.read(Location::DataDirectory, "plugins/company-assistant/plugin.lua").as_deref(), Some("managed plugin"));
        assert_eq!(test.read(Location::DataDirectory, "events.log").as_deref(), Some("log"), "The logs are not part of a restore.");

        assert_eq!(test.read(Location::DataDirectory, ".restore-previous/workspaces/current/chat/thread.json").as_deref(), Some("current chat"));
        assert_eq!(test.read(Location::ConfigDirectory, ".restore-previous/settings.json").as_deref(), Some("current settings"));
        assert_eq!(test.read(Location::ConfigDirectory, ".restore-previous/settings.v6.json").as_deref(), Some("current settings of v6"));

        assert!(!test.exists(Location::DataDirectory, ".restore-pending"));
        assert!(!test.exists(Location::ConfigDirectory, ".restore-pending"));

        let result = test.result();
        assert_eq!(result.outcome, RestoreOutcome::Applied);
        assert_eq!(result.backup_app_version, "26.10.1");
    }

    #[test]
    fn a_restore_which_is_not_marked_ready_changes_nothing() {
        let test = TestFolders::new();
        test.write_current_data();
        test.prepare_restore(&all_elements());
        fs::remove_file(test.data.join(".restore-pending").join(READY_FILE_NAME)).unwrap();

        let outcome = apply_pending_restore_in(&test.folders(), &[]);

        assert_eq!(outcome, None);
        assert_current_data_untouched(&test);
    }

    #[test]
    fn a_restore_replaces_the_media_transcripts_of_the_data_sources() {
        let test = TestFolders::new();
        test.write_current_data();
        test.write(Location::DataDirectory, "mediaTranscripts/current/transcript.md", "current transcript");
        test.prepare_restore(r#"{ "location": "DATA_DIRECTORY", "relative_path": "mediaTranscripts/", "keep": [] }"#);
        test.write(Location::DataDirectory, ".restore-pending/mediaTranscripts/restored/transcript.md", "restored transcript");

        let outcome = apply_pending_restore_in(&test.folders(), &[]);

        assert_eq!(outcome, Some(RestoreOutcome::Applied));
        assert_eq!(test.read(Location::DataDirectory, "mediaTranscripts/restored/transcript.md").as_deref(), Some("restored transcript"));
        assert!(!test.exists(Location::DataDirectory, "mediaTranscripts/current"));
    }

    #[test]
    fn a_plan_which_names_another_element_is_refused() {
        for relative_path in ["", "events.log", "databases/", "../data/", "pandoc/"] {
            let test = TestFolders::new();
            test.write_current_data();
            test.prepare_restore(&format!(r#"{{ "location": "DATA_DIRECTORY", "relative_path": "{relative_path}", "keep": [] }}"#));

            let outcome = apply_pending_restore_in(&test.folders(), &[]);

            assert_eq!(outcome, Some(RestoreOutcome::Refused), "'{relative_path}' must not be replaced.");
            assert_current_data_untouched(&test);
            assert!(!test.exists(Location::DataDirectory, ".restore-pending/READY"), "A refused restore must not be tried again at every start.");
        }
    }

    #[test]
    fn a_kept_path_outside_its_element_is_refused() {
        for kept in ["../events.log", "workspaces/", "plugins/../events.log", "plugins/", "/plugins/x/", "plugins\\\\x"] {
            let test = TestFolders::new();
            test.write_current_data();
            test.prepare_restore(&format!(r#"{{ "location": "DATA_DIRECTORY", "relative_path": "plugins/", "keep": ["{kept}"] }}"#));

            let outcome = apply_pending_restore_in(&test.folders(), &[]);

            assert_eq!(outcome, Some(RestoreOutcome::Refused), "'{kept}' must not be kept.");
            assert_current_data_untouched(&test);
        }
    }

    #[test]
    fn a_backup_which_brings_a_kept_path_is_refused() {
        let test = TestFolders::new();
        test.write_current_data();
        test.prepare_restore(&all_elements());
        test.write(Location::DataDirectory, ".restore-pending/plugins/.internal/languages/plugin.lua", "foreign built-in plugin");

        let outcome = apply_pending_restore_in(&test.folders(), &[]);

        assert_eq!(outcome, Some(RestoreOutcome::Refused));
        assert_current_data_untouched(&test);
    }

    #[test]
    fn a_restore_is_refused_while_the_data_of_an_earlier_one_are_kept() {
        let test = TestFolders::new();
        test.write_current_data();
        test.prepare_restore(&all_elements());
        test.write(Location::DataDirectory, ".restore-previous/workspaces/older/chat/thread.json", "older chat");

        let outcome = apply_pending_restore_in(&test.folders(), &[]);

        assert_eq!(outcome, Some(RestoreOutcome::Refused));
        assert_current_data_untouched(&test);
        assert_eq!(test.read(Location::DataDirectory, ".restore-previous/workspaces/older/chat/thread.json").as_deref(), Some("older chat"));
    }

    #[test]
    fn empty_folders_which_an_earlier_restore_left_do_not_block_a_restore() {
        let test = TestFolders::new();
        test.write_current_data();
        test.prepare_restore(&all_elements());
        fs::create_dir_all(test.data.join(".restore-previous").join("plugins")).unwrap();

        let outcome = apply_pending_restore_in(&test.folders(), &[]);

        assert_eq!(outcome, Some(RestoreOutcome::Applied));
    }

    #[test]
    fn a_move_which_fails_moves_everything_back() {
        let test = TestFolders::new();
        test.write_current_data();

        // A file in the way of the folder of the vector stores lets the last move fail:
        test.write(Location::DataDirectory, "databases", "not a folder");
        test.write(Location::DataDirectory, ".restore-pending/databases/vector_database/stores/store_1/segment", "restored store");
        test.prepare_restore(&format!(r#"{}, {{ "location": "DATA_DIRECTORY", "relative_path": "databases/vector_database/stores/", "keep": [] }}"#, all_elements()));

        let outcome = apply_pending_restore_in(&test.folders(), &[]);

        assert_eq!(outcome, Some(RestoreOutcome::RolledBack));
        assert_eq!(test.result().outcome, RestoreOutcome::RolledBack);
        assert_current_data_untouched(&test);
        assert!(!test.exists(Location::DataDirectory, ".restore-previous"));
        assert!(!test.exists(Location::ConfigDirectory, ".restore-previous"));
        assert!(!test.exists(Location::DataDirectory, ".restore-pending/READY"));
        assert!(!test.exists(Location::DataDirectory, ".restore-pending/journal.json"));
        assert_eq!(test.read(Location::DataDirectory, ".restore-pending/workspaces/restored/chat/thread.json").as_deref(), Some("restored chat"), "The app removes the restored data.");
    }

    #[test]
    fn a_restore_which_stopped_in_the_middle_is_moved_back_at_the_next_start() {
        let move_count = {
            let test = TestFolders::new();
            test.write_current_data();
            test.prepare_restore(&all_elements());
            plan_moves(&test.folders(), &read_plan(&test.folders().pending()).unwrap()).unwrap().len()
        };

        for completed_moves in 0..=move_count {
            let test = TestFolders::new();
            test.write_current_data();
            test.prepare_restore(&all_elements());

            let mut journal = begin(&test.folders(), &read_plan(&test.folders().pending()).unwrap(), &[]).unwrap();
            for _ in 0..completed_moves {
                execute_next(&test.folders(), &mut journal, &[]).unwrap();
            }

            let outcome = apply_pending_restore_in(&test.folders(), &[]);

            assert_eq!(outcome, Some(RestoreOutcome::RolledBack), "after {completed_moves} of {move_count} moves");
            assert_current_data_untouched(&test);
            assert!(!test.exists(Location::DataDirectory, ".restore-previous"), "after {completed_moves} of {move_count} moves");
            assert!(!test.exists(Location::DataDirectory, ".restore-pending/READY"));
            assert_eq!(apply_pending_restore_in(&test.folders(), &[]), None, "The next start finds nothing to do.");
        }
    }

    #[test]
    fn a_clean_up_which_stopped_is_finished_at_the_next_start() {
        let test = TestFolders::new();
        test.write(Location::DataDirectory, ".restore-pending/journal.json", r#"{ "moves": [], "started": 0 }"#);
        test.write(Location::DataDirectory, ".restore-pending/restore.json", &plan_json(""));
        fs::create_dir_all(test.data.join(".restore-pending").join("databases").join("sqlite")).unwrap();

        let outcome = apply_pending_restore_in(&test.folders(), &[]);

        assert_eq!(outcome, None);
        assert!(!test.exists(Location::DataDirectory, ".restore-pending"));
    }

    #[test]
    fn a_kept_path_below_another_one_moves_along_with_it() {
        let element = RestoreElement {
            location: Location::DataDirectory,
            relative_path: "plugins/".to_string(),
            keep: vec!["plugins/.config/".to_string(), "plugins/.config/organization/".to_string(), "plugins/.lock".to_string()],
        };

        assert_eq!(kept_paths(&element).unwrap(), vec!["plugins/.config", "plugins/.config-tests", "plugins/.internal", "plugins/.lock"]);
    }

    #[test]
    fn the_settings_of_single_versions_are_recognized_by_their_name() {
        assert!(is_settings_of_single_version("settings.v6.json"));
        assert!(is_settings_of_single_version("settings.v12.json"));
        assert!(!is_settings_of_single_version("settings.json"));
        assert!(!is_settings_of_single_version("settings.v.json"));
        assert!(!is_settings_of_single_version("settings.v6.json.tmp"));
    }
}