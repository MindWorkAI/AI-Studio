use std::fs;
use std::io;
use std::path::{Path, PathBuf};
use std::time::Duration;
use log::{info, warn};
use tauri::{AppHandle, WebviewWindow};
use crate::environment::DATA_DIRECTORY;

/// The file which AI Studio leaves in its data directory right before it restarts itself. The list
/// of backup contents in the app, `DataBackupContents.ENTRIES`, names it as a temporary file.
const MARKER_FILE_NAME: &str = "restart_to_front";

/// How old the marker may be and still bring the window to the front.
///
/// The new instance starts within seconds after the old one left the marker. An older marker stems
/// from a restart which never happened, e.g., because the app crashed on its way out. The next start
/// is then one the user triggers, and that start brings the app to the front by itself.
const MARKER_MAX_AGE: Duration = Duration::from_secs(120);

/// Restarts the app at once, see `AppHandle::restart`, and brings its window to the front again.
///
/// On macOS, Tauri starts the new instance in the background, so that a restart in the background
/// takes no focus away from the app the user works in. AI Studio restarts itself only on behalf of
/// the user, e.g., to finish an update, who then expects to see the app again. Therefore, the next
/// start finds the marker and brings the window to the front, see `bring_to_front_after_own_restart`.
pub fn restart(app_handle: &AppHandle) -> ! {
    leave_marker_in_data_directory();
    app_handle.restart()
}

/// Requests a restart of the app, see `AppHandle::request_restart`, and brings its window to the
/// front again, see `restart`.
pub fn request_restart(app_handle: &AppHandle) {
    leave_marker_in_data_directory();
    app_handle.request_restart();
}

/// Brings the window to the front when AI Studio restarted itself just before.
///
/// Call it once at startup, as soon as the data directory is known. It removes the marker in any
/// case, so that it takes effect at most once.
pub fn bring_to_front_after_own_restart(window: &WebviewWindow) {
    let Some(directory) = data_directory() else {
        warn!(Source = "Tauri"; "Cannot check whether AI Studio restarted itself: the data directory is unknown.");
        return;
    };

    match take_fresh_marker(&directory, MARKER_MAX_AGE) {
        Ok(true) => {
            info!(Source = "Tauri"; "AI Studio restarted itself; bringing the window to the front.");
            if let Err(error) = window.set_focus() {
                warn!(Source = "Tauri"; "Failed to bring the window to the front after the restart: {error}");
            }
        },

        Ok(false) => {},
        Err(error) => warn!(Source = "Tauri"; "Failed to check whether AI Studio restarted itself: {error}"),
    }
}

fn data_directory() -> Option<PathBuf> {
    DATA_DIRECTORY.get().map(PathBuf::from)
}

/// Leaves the marker for the next start. A failure costs only the window coming to the front, so
/// the restart goes on anyway.
fn leave_marker_in_data_directory() {
    let Some(directory) = data_directory() else {
        warn!(Source = "Tauri"; "The data directory is unknown, so the window may stay in the background after the restart.");
        return;
    };

    if let Err(error) = leave_marker(&directory) {
        warn!(Source = "Tauri"; "Failed to mark the restart, so the window may stay in the background afterward: {error}");
    }
}

fn leave_marker(directory: &Path) -> io::Result<()> {
    fs::write(directory.join(MARKER_FILE_NAME), b"")
}

/// Removes the marker and tells whether it was there and at most `max_age` old.
///
/// A marker which seems to come from the future, because the clock went back, counts as fresh.
fn take_fresh_marker(directory: &Path, max_age: Duration) -> io::Result<bool> {
    let marker = directory.join(MARKER_FILE_NAME);
    let modified = match fs::metadata(&marker) {
        Ok(metadata) => metadata.modified(),
        Err(error) if error.kind() == io::ErrorKind::NotFound => return Ok(false),
        Err(error) => return Err(error),
    };

    fs::remove_file(&marker)?;
    let age = modified?.elapsed().unwrap_or(Duration::ZERO);
    Ok(age <= max_age)
}

#[cfg(test)]
mod tests {
    use std::fs::File;
    use std::time::SystemTime;
    use super::*;

    #[test]
    fn a_fresh_marker_counts_once() {
        let directory = tempfile::tempdir().unwrap();
        leave_marker(directory.path()).unwrap();

        assert!(take_fresh_marker(directory.path(), MARKER_MAX_AGE).unwrap());
        assert!(!directory.path().join(MARKER_FILE_NAME).exists());
        assert!(!take_fresh_marker(directory.path(), MARKER_MAX_AGE).unwrap());
    }

    #[test]
    fn an_old_marker_does_not_count_but_is_removed() {
        let directory = tempfile::tempdir().unwrap();
        leave_marker(directory.path()).unwrap();

        let marker = directory.path().join(MARKER_FILE_NAME);
        let long_ago = SystemTime::now() - MARKER_MAX_AGE - Duration::from_secs(60);
        File::options().write(true).open(&marker).unwrap().set_modified(long_ago).unwrap();

        assert!(!take_fresh_marker(directory.path(), MARKER_MAX_AGE).unwrap());
        assert!(!marker.exists());
    }

    #[test]
    fn no_marker_does_not_count() {
        let directory = tempfile::tempdir().unwrap();

        assert!(!take_fresh_marker(directory.path(), MARKER_MAX_AGE).unwrap());
    }
}