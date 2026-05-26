use log::{error, info};
use axum::extract::Query;
use axum::Json;
use serde::{Deserialize, Serialize};
use tauri_plugin_dialog::{DialogExt, FileDialogBuilder};
use crate::api_token::APIToken;
use crate::app_window::MAIN_WINDOW;

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