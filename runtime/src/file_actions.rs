use log::{error, info};
use axum::extract::Query;
use axum::Json;
use serde::{Deserialize, Serialize};
use std::path::{Path, PathBuf};
use tauri_plugin_dialog::{DialogExt, FileDialogBuilder};
use crate::api_token::APIToken;
use crate::app_window::MAIN_WINDOW;

#[cfg(any(windows, target_os = "macos"))]
use std::process::Command;

#[cfg(target_os = "linux")]
use ashpd::desktop::open_uri::{OpenDirectoryRequest, OpenFileRequest};

#[cfg(windows)]
use std::os::windows::process::CommandExt;

/// Microsoft documents CREATE_NO_WINDOW as a process creation flag with value 0x08000000.
#[cfg(windows)]
const CREATE_NO_WINDOW: u32 = 0x08000000;

#[derive(Clone, Deserialize)]
pub struct PreviousDirectory {
    path: String,
}

#[derive(Deserialize)]
pub struct SelectDirectoryQuery {
    title: String,
}

#[derive(Clone, Deserialize)]
pub struct FileTypeFilter {
    filter_name: String,
    filter_extensions: Vec<String>,
}

#[derive(Clone, Deserialize)]
pub struct SelectFileOptions {
    title: String,
    previous_file: Option<PreviousFile>,
    filter: Option<FileTypeFilter>,
}

#[derive(Clone, Deserialize)]
pub struct SaveFileOptions {
    title: String,
    name_file: Option<PreviousFile>,
    filter: Option<FileTypeFilter>,
}

#[derive(Clone, Deserialize)]
pub struct OpenPathOptions {
    path: String,
}

#[derive(Serialize)]
pub struct DirectorySelectionResponse {
    user_cancelled: bool,
    selected_directory: String,
}

#[derive(Serialize)]
pub struct FileSelectionResponse {
    user_cancelled: bool,
    selected_file_path: String,
}

#[derive(Serialize)]
pub struct FilesSelectionResponse {
    user_cancelled: bool,
    selected_file_paths: Vec<String>,
}

#[derive(Serialize)]
pub struct FileSaveResponse {
    user_cancelled: bool,
    save_file_path: String,
}

#[derive(Serialize)]
pub struct OpenPathResponse {
    success: bool,
    issue: String,
}

#[derive(Clone, Deserialize)]
pub struct PreviousFile {
    file_path: String,
}

/// Let the user select a directory.
pub async fn select_directory(
    _token: APIToken,
    Query(query): Query<SelectDirectoryQuery>,
    previous_directory: Option<Json<PreviousDirectory>>,
) -> Json<DirectorySelectionResponse> {
    let main_window_lock = MAIN_WINDOW.lock().unwrap();
    let main_window = match main_window_lock.as_ref() {
        Some(window) => window,
        None => {
            error!(Source = "Tauri"; "Cannot open directory dialog: main window not available.");
            return Json(DirectorySelectionResponse {
                user_cancelled: true,
                selected_directory: String::from(""),
            });
        }
    };

    let mut dialog = main_window.dialog().file().set_parent(main_window).set_title(&query.title);
    if let Some(previous) = previous_directory {
        dialog = dialog.set_directory(previous.path.clone());
    }

    drop(main_window_lock);

    let folder_path = dialog.blocking_pick_folder();
    match folder_path {
        Some(path) => {
            match path.into_path() {
                Ok(pb) => {
                    info!("User selected directory: {pb:?}");
                    Json(DirectorySelectionResponse {
                        user_cancelled: false,
                        selected_directory: pb.to_string_lossy().to_string(),
                    })
                }
                Err(e) => {
                    error!(Source = "Tauri"; "Failed to convert directory path: {e}");
                    Json(DirectorySelectionResponse {
                        user_cancelled: true,
                        selected_directory: String::new(),
                    })
                }
            }
        },

        None => {
            info!("User cancelled directory selection.");
            Json(DirectorySelectionResponse {
                user_cancelled: true,
                selected_directory: String::from(""),
            })
        },
    }
}

/// Let the user select a file.
pub async fn select_file(
    _token: APIToken,
    payload: Json<SelectFileOptions>,
) -> Json<FileSelectionResponse> {
    // Create a new file dialog builder:
    let file_dialog = MAIN_WINDOW
        .lock()
        .unwrap()
        .as_ref()
        .map(|w| w.dialog().file().set_parent(w).set_title(&payload.title));

    let Some(mut file_dialog) = file_dialog else {
        error!(Source = "Tauri"; "Cannot open file dialog: main window not available.");
        return Json(FileSelectionResponse {
            user_cancelled: true,
            selected_file_path: String::from(""),
        });
    };

    // Set the file type filter if provided:
    file_dialog = apply_filter(file_dialog, &payload.filter);

    // Set the previous file path if provided:
    if let Some(previous) = &payload.previous_file {
        let previous_path = previous.file_path.as_str();
        file_dialog = file_dialog.set_directory(previous_path);
    }

    // Show the file dialog and get the selected file path:
    let file_path = file_dialog.blocking_pick_file();
    match file_path {
        Some(path) => match path.into_path() {
            Ok(pb) => {
                info!("User selected file: {pb:?}");
                Json(FileSelectionResponse {
                    user_cancelled: false,
                    selected_file_path: pb.to_string_lossy().to_string(),
                })
            }
            Err(e) => {
                error!(Source = "Tauri"; "Failed to convert file path: {e}");
                Json(FileSelectionResponse {
                    user_cancelled: true,
                    selected_file_path: String::new(),
                })
            }
        },

        None => {
            info!("User cancelled file selection.");
            Json(FileSelectionResponse {
                user_cancelled: true,
                selected_file_path: String::from(""),
            })
        },
    }
}

/// Let the user select some files.
pub async fn select_files(
    _token: APIToken,
    payload: Json<SelectFileOptions>,
) -> Json<FilesSelectionResponse> {
    // Create a new file dialog builder:
    let file_dialog = MAIN_WINDOW
        .lock()
        .unwrap()
        .as_ref()
        .map(|w| w.dialog().file().set_parent(w).set_title(&payload.title));

    let Some(mut file_dialog) = file_dialog else {
        error!(Source = "Tauri"; "Cannot open file dialog: main window not available.");
        return Json(FilesSelectionResponse {
            user_cancelled: true,
            selected_file_paths: Vec::new(),
        });
    };

    // Set the file type filter if provided:
    file_dialog = apply_filter(file_dialog, &payload.filter);

    // Set the previous file path if provided:
    if let Some(previous) = &payload.previous_file {
        let previous_path = previous.file_path.as_str();
        file_dialog = file_dialog.set_directory(previous_path);
    }

    // Show the file dialog and get the selected file path:
    let file_paths = file_dialog.blocking_pick_files();
    match file_paths {
        Some(paths) => {
            let converted: Vec<String> = paths.into_iter().filter_map(|p| p.into_path().ok()).map(|pb| pb.to_string_lossy().to_string()).collect();
            info!("User selected {} files.", converted.len());
            Json(FilesSelectionResponse {
                user_cancelled: false,
                selected_file_paths: converted,
            })
        }

        None => {
            info!("User cancelled file selection.");
            Json(FilesSelectionResponse {
                user_cancelled: true,
                selected_file_paths: Vec::new(),
            })
        },
    }
}

pub async fn save_file(_token: APIToken, payload: Json<SaveFileOptions>) -> Json<FileSaveResponse> {
    // Create a new file dialog builder:
    let file_dialog = MAIN_WINDOW
        .lock()
        .unwrap()
        .as_ref()
        .map(|w| w.dialog().file().set_parent(w).set_title(&payload.title));

    let Some(mut file_dialog) = file_dialog else {
        error!(Source = "Tauri"; "Cannot open save dialog: main window not available.");
        return Json(FileSaveResponse {
            user_cancelled: true,
            save_file_path: String::from(""),
        });
    };

    // Set the file type filter if provided:
    file_dialog = apply_filter(file_dialog, &payload.filter);

    // Set the previous file path if provided:
    if let Some(previous) = &payload.name_file {
        let previous_path = previous.file_path.as_str();
        file_dialog = file_dialog.set_directory(previous_path);
    }

    // Displays the file dialogue box and select the file:
    let file_path = file_dialog.blocking_save_file();
    match file_path {
        Some(path) => match path.into_path() {
            Ok(pb) => {
                info!("User selected file for writing operation: {pb:?}");
                Json(FileSaveResponse {
                    user_cancelled: false,
                    save_file_path: pb.to_string_lossy().to_string(),
                })
            }
            Err(e) => {
                error!(Source = "Tauri"; "Failed to convert save file path: {e}");
                Json(FileSaveResponse {
                    user_cancelled: true,
                    save_file_path: String::new(),
                })
            }
        },

        None => {
            info!("User cancelled file selection.");
            Json(FileSaveResponse {
                user_cancelled: true,
                save_file_path: String::from(""),
            })
        },
    }
}

pub async fn open_path_in_file_manager(
    _token: APIToken,
    payload: Json<OpenPathOptions>,
) -> Json<OpenPathResponse> {
    let requested_path = PathBuf::from(payload.path.trim());
    if requested_path.as_os_str().is_empty() {
        return Json(OpenPathResponse {
            success: false,
            issue: String::from("The path is empty."),
        });
    }

    let Some(target) = resolve_file_manager_target(&requested_path) else {
        let issue = format!(
            "The path does not exist and its parent folder could not be found: {}",
            requested_path.to_string_lossy(),
        );
        error!(Source = "Tauri"; "{issue}");
        return Json(OpenPathResponse {
            success: false,
            issue,
        });
    };

    #[cfg(target_os = "linux")]
    {
        return match open_path_in_linux_file_manager(&target).await {
            Ok(()) => {
                info!("Opened file manager for path: {:?}", target.path);
                Json(OpenPathResponse {
                    success: true,
                    issue: String::new(),
                })
            }

            Err(issue) => {
                error!(Source = "Tauri"; "{issue}");
                Json(OpenPathResponse {
                    success: false,
                    issue,
                })
            }
        };
    }

    #[cfg(any(windows, target_os = "macos"))]
    {
        let mut command = create_file_manager_command(&target);

        #[cfg(windows)]
        command.creation_flags(CREATE_NO_WINDOW);

        match command.spawn() {
            Ok(_) => {
                info!("Opened file manager for path: {:?}", target.path);
                Json(OpenPathResponse {
                    success: true,
                    issue: String::new(),
                })
            }

            Err(error) => {
                let issue = format!("Failed to open the file manager: {error}");
                error!(Source = "Tauri"; "{issue}");
                Json(OpenPathResponse {
                    success: false,
                    issue,
                })
            }
        }
    }
}

/// Applies an optional file type filter to a FileDialogBuilder.
fn apply_filter<R: tauri::Runtime>(file_dialog: FileDialogBuilder<R>, filter: &Option<FileTypeFilter>) -> FileDialogBuilder<R> {
    match filter {
        Some(f) => file_dialog.add_filter(
            &f.filter_name,
            &f.filter_extensions.iter().map(|s| s.as_str()).collect::<Vec<&str>>(),
        ),

        None => file_dialog,
    }
}

#[derive(Debug, PartialEq, Eq)]
struct FileManagerTarget {
    path: PathBuf,
    reveal_file: bool,
}

#[cfg(any(target_os = "linux", test))]
#[derive(Debug, PartialEq, Eq)]
enum LinuxPortalOperation {
    RevealFile,
    OpenDirectory,
}

fn resolve_file_manager_target(requested_path: &Path) -> Option<FileManagerTarget> {
    if requested_path.is_file() {
        return Some(FileManagerTarget {
            path: requested_path.to_path_buf(),
            reveal_file: true,
        });
    }

    if requested_path.is_dir() {
        return Some(FileManagerTarget {
            path: requested_path.to_path_buf(),
            reveal_file: false,
        });
    }

    requested_path.parent()
        .filter(|parent| parent.is_dir())
        .map(|parent| FileManagerTarget {
            path: parent.to_path_buf(),
            reveal_file: false,
        })
}

#[cfg(any(target_os = "linux", test))]
fn linux_portal_operation(target: &FileManagerTarget) -> LinuxPortalOperation {
    if target.reveal_file {
        LinuxPortalOperation::RevealFile
    } else {
        LinuxPortalOperation::OpenDirectory
    }
}

#[cfg(any(target_os = "linux", test))]
fn xdg_open_fallback_path(target: &FileManagerTarget) -> &Path {
    if target.reveal_file {
        target.path.parent().unwrap_or(&target.path)
    } else {
        &target.path
    }
}

#[cfg(target_os = "linux")]
enum LinuxPortalError {
    Unavailable(String),
    RequestFailed(String),
}

#[cfg(target_os = "linux")]
async fn open_path_with_linux_portal(target: &FileManagerTarget) -> Result<(), LinuxPortalError> {
    let file = std::fs::File::open(&target.path)
        .map_err(|error| LinuxPortalError::Unavailable(format!("Failed to open the path for the desktop portal: {error}")))?;

    let request = match linux_portal_operation(target) {
        LinuxPortalOperation::RevealFile => OpenDirectoryRequest::default().send(&file).await,
        LinuxPortalOperation::OpenDirectory => OpenFileRequest::default().send_file(&file).await,
    }
    .map_err(|error| LinuxPortalError::Unavailable(format!("Desktop portal invocation failed: {error}")))?;

    request.response()
        .map_err(|error| LinuxPortalError::RequestFailed(format!("Desktop portal request failed: {error}")))
}

#[cfg(target_os = "linux")]
async fn open_path_with_xdg_open(target: &FileManagerTarget) -> Result<(), String> {
    let fallback_path = xdg_open_fallback_path(target);
    let status = tokio::process::Command::new("xdg-open")
        .arg(fallback_path)
        .status()
        .await
        .map_err(|error| format!("xdg-open failed to start for '{}': {error}", fallback_path.to_string_lossy()))?;

    if status.success() {
        Ok(())
    } else {
        Err(format!("xdg-open failed for '{}' with exit status {status}", fallback_path.to_string_lossy()))
    }
}

#[cfg(target_os = "linux")]
async fn open_path_in_linux_file_manager(target: &FileManagerTarget) -> Result<(), String> {
    match open_path_with_linux_portal(target).await {
        Ok(()) => Ok(()),
        Err(LinuxPortalError::RequestFailed(error)) => Err(error),
        Err(LinuxPortalError::Unavailable(portal_error)) => {
            match open_path_with_xdg_open(target).await {
                Ok(()) => Ok(()),
                Err(fallback_error) => Err(format!("{portal_error} Fallback failed: {fallback_error}")),
            }
        }
    }
}

#[cfg(target_os = "windows")]
fn create_file_manager_command(target: &FileManagerTarget) -> Command {
    let mut command = Command::new("explorer.exe");
    if target.reveal_file {
        command.arg(format!("/select,{}", target.path.to_string_lossy()));
    } else {
        command.arg(&target.path);
    }

    command
}

#[cfg(target_os = "macos")]
fn create_file_manager_command(target: &FileManagerTarget) -> Command {
    let mut command = Command::new("open");
    if target.reveal_file {
        command.arg("-R");
    }

    command.arg(&target.path);
    command
}

#[cfg(test)]
mod tests {
    use super::*;
    use std::fs;

    #[test]
    fn existing_file_is_revealed_and_falls_back_to_its_parent() {
        let temp_dir = tempfile::tempdir().unwrap();
        let file_path = temp_dir.path().join("application.log");
        fs::write(&file_path, "log").unwrap();

        let target = resolve_file_manager_target(&file_path).unwrap();

        assert_eq!(target.path, file_path);
        assert!(target.reveal_file);
        assert_eq!(linux_portal_operation(&target), LinuxPortalOperation::RevealFile);
        assert_eq!(xdg_open_fallback_path(&target), temp_dir.path());
    }

    #[test]
    fn existing_directory_is_opened_directly() {
        let temp_dir = tempfile::tempdir().unwrap();

        let target = resolve_file_manager_target(temp_dir.path()).unwrap();

        assert_eq!(target.path, temp_dir.path());
        assert!(!target.reveal_file);
        assert_eq!(linux_portal_operation(&target), LinuxPortalOperation::OpenDirectory);
        assert_eq!(xdg_open_fallback_path(&target), temp_dir.path());
    }

    #[test]
    fn missing_file_uses_its_existing_parent_directory() {
        let temp_dir = tempfile::tempdir().unwrap();
        let missing_file = temp_dir.path().join("missing.log");

        let target = resolve_file_manager_target(&missing_file).unwrap();

        assert_eq!(target.path, temp_dir.path());
        assert!(!target.reveal_file);
        assert_eq!(linux_portal_operation(&target), LinuxPortalOperation::OpenDirectory);
        assert_eq!(xdg_open_fallback_path(&target), temp_dir.path());
    }

    #[test]
    fn invalid_path_without_existing_parent_is_rejected() {
        let temp_dir = tempfile::tempdir().unwrap();
        let invalid_path = temp_dir.path().join("missing-directory").join("missing.log");

        assert!(resolve_file_manager_target(&invalid_path).is_none());
    }
}
