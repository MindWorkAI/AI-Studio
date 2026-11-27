use std::sync::atomic::{AtomicBool, Ordering};
use tauri::AppHandle;

#[cfg(target_os = "macos")]
use std::path::Path;

#[cfg(target_os = "macos")]
use log::{info, warn};

/// Whether AI Studio restarts itself, see `restart` and `request_restart`.
static RESTART_REQUESTED: AtomicBool = AtomicBool::new(false);

/// Restarts the app at once, see `AppHandle::restart`, and shows its window in the foreground again.
///
/// On macOS, Tauri starts the new instance in the background, so that a restart in the background
/// takes no focus away from the app the user works in. AI Studio restarts itself only on behalf of
/// the user, e.g., to finish an update, who then expects to see the app again. Therefore, AI Studio
/// starts the new instance itself, see `start_new_instance_in_front`.
///
/// Call it outside the main thread only: there, Tauri restarts the app at once, without the event
/// `RunEvent::Exit`, and therefore in the background.
pub fn restart(app_handle: &AppHandle) -> ! {
    RESTART_REQUESTED.store(true, Ordering::SeqCst);
    app_handle.restart()
}

/// Requests a restart of the app, see `AppHandle::request_restart`, and shows its window in the
/// foreground again, see `restart`.
pub fn request_restart(app_handle: &AppHandle) {
    RESTART_REQUESTED.store(true, Ordering::SeqCst);
    app_handle.request_restart();
}

/// Starts the new instance in the foreground and ends this one, when AI Studio restarts itself.
///
/// Call it on `RunEvent::Exit`, which reaches the app right before Tauri starts the new instance in
/// the background. The new instance cannot bring its window to the front by itself: macOS lets an app
/// become active only when the active app allows it. The old instance, however, is still the active
/// app, so the new instance it starts comes to the front.
///
/// Outside an app bundle, as in a development build, and when the start fails, Tauri restarts the
/// app as usual.
#[cfg(target_os = "macos")]
pub fn start_new_instance_in_front() {
    use std::process::{exit, Command, Stdio};

    if !RESTART_REQUESTED.load(Ordering::SeqCst) {
        return;
    }

    let Ok(binary) = std::env::current_exe() else {
        return;
    };

    let Some(bundle) = app_bundle(&binary) else {
        return;
    };

    info!(Source = "Tauri"; "Starting the new instance of AI Studio in the foreground.");
    let result = Command::new("/usr/bin/open")
        .arg("-n")
        .arg(bundle)
        .arg("--args")
        .args(std::env::args_os().skip(1))
        .stdin(Stdio::null())
        .output();

    match result {
        Ok(output) if output.status.success() => {
            log::logger().flush();
            exit(0);
        },

        Ok(output) => warn!(Source = "Tauri"; "Failed to start the new instance in the foreground ({}), so Tauri starts it in the background: {}", output.status, String::from_utf8_lossy(&output.stderr).trim()),
        Err(error) => warn!(Source = "Tauri"; "Failed to start the new instance in the foreground, so Tauri starts it in the background: {error}"),
    }
}

/// Returns the app bundle which contains the binary, such as `/Applications/X.app` for
/// `/Applications/X.app/Contents/MacOS/x`.
#[cfg(target_os = "macos")]
fn app_bundle(binary: &Path) -> Option<&Path> {
    let macos_directory = binary.parent()?;
    let contents_directory = macos_directory.parent()?;
    if macos_directory.file_name()? != "MacOS" || contents_directory.file_name()? != "Contents" {
        return None;
    }

    contents_directory.parent()
}

#[cfg(all(test, target_os = "macos"))]
mod tests {
    use std::path::Path;
    use super::app_bundle;

    #[test]
    fn finds_the_bundle_of_a_binary_inside_one() {
        let binary = Path::new("/Applications/MindWork AI Studio.app/Contents/MacOS/mindwork-ai-studio");
        assert_eq!(app_bundle(binary), Some(Path::new("/Applications/MindWork AI Studio.app")));
    }

    #[test]
    fn finds_no_bundle_for_a_binary_outside_one() {
        assert_eq!(app_bundle(Path::new("/Users/placeholder/runtime/target/debug/mindwork-ai-studio")), None);
        assert_eq!(app_bundle(Path::new("/Applications/MindWork AI Studio.app/Contents/Resources/mindwork-ai-studio")), None);
        assert_eq!(app_bundle(Path::new("/mindwork-ai-studio")), None);
    }
}