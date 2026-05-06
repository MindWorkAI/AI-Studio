use log::info;
use rocket::post;
use rocket::serde::{Deserialize, Serialize};
use rocket::serde::json::Json;
use tauri::api::dialog::blocking::FileDialogBuilder;
use crate::api_token::APIToken;

#[derive(Clone, Deserialize)]
pub struct PreviousDirectory {
    path: String,
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
#[post("/select/directory?<title>", data = "<previous_directory>")]
pub fn select_directory(_token: APIToken, title: &str, previous_directory: Option<Json<PreviousDirectory>>) -> Json<DirectorySelectionResponse> {
    let folder_path = match previous_directory {
        Some(previous) => {
            let previous_path = previous.path.as_str();
            FileDialogBuilder::new()
                .set_title(title)
                .set_directory(previous_path)
                .pick_folder()
        },

        None => {
            FileDialogBuilder::new()
                .set_title(title)
                .pick_folder()
        },
    };

    match folder_path {
        Some(path) => {
            info!("User selected directory: {path:?}");
            Json(DirectorySelectionResponse {
                user_cancelled: false,
                selected_directory: path.to_str().unwrap().to_string(),
            })
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
#[post("/select/file", data = "<payload>")]
pub fn select_file(_token: APIToken, payload: Json<SelectFileOptions>) -> Json<FileSelectionResponse> {

    // Create a new file dialog builder:
    let file_dialog = FileDialogBuilder::new();

    // Set the title of the file dialog:
    let file_dialog = file_dialog.set_title(&payload.title);

    // Set the file type filter if provided:
    let file_dialog = apply_filter(file_dialog, &payload.filter);

    // Set the previous file path if provided:
    let file_dialog = match &payload.previous_file {
        Some(previous) => {
            let previous_path = previous.file_path.as_str();
            file_dialog.set_directory(previous_path)
        },

        None => file_dialog,
    };

    // Show the file dialog and get the selected file path:
    let file_path = file_dialog.pick_file();
    match file_path {
        Some(path) => {
            info!("User selected file: {path:?}");
            Json(FileSelectionResponse {
                user_cancelled: false,
                selected_file_path: path.to_str().unwrap().to_string(),
            })
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
#[post("/select/files", data = "<payload>")]
pub fn select_files(_token: APIToken, payload: Json<SelectFileOptions>) -> Json<FilesSelectionResponse> {

    // Create a new file dialog builder:
    let file_dialog = FileDialogBuilder::new();

    // Set the title of the file dialog:
    let file_dialog = file_dialog.set_title(&payload.title);

    // Set the file type filter if provided:
    let file_dialog = apply_filter(file_dialog, &payload.filter);

    // Set the previous file path if provided:
    let file_dialog = match &payload.previous_file {
        Some(previous) => {
            let previous_path = previous.file_path.as_str();
            file_dialog.set_directory(previous_path)
        },

        None => file_dialog,
    };

    // Show the file dialog and get the selected file path:
    let file_paths = file_dialog.pick_files();
    match file_paths {
        Some(paths) => {
            info!("User selected {} files.", paths.len());
            Json(FilesSelectionResponse {
                user_cancelled: false,
                selected_file_paths: paths.iter().map(|p| p.to_str().unwrap().to_string()).collect(),
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

#[post("/save/file", data = "<payload>")]
pub fn save_file(_token: APIToken, payload: Json<SaveFileOptions>) -> Json<FileSaveResponse> {

    // Create a new file dialog builder:
    let file_dialog = FileDialogBuilder::new();

    // Set the title of the file dialog:
    let file_dialog = file_dialog.set_title(&payload.title);

    // Set the file type filter if provided:
    let file_dialog = apply_filter(file_dialog, &payload.filter);

    // Set the previous file path if provided:
    let file_dialog = match &payload.name_file {
        Some(previous) => {
            let previous_path = previous.file_path.as_str();
            file_dialog.set_directory(previous_path)
        },

        None => file_dialog,
    };

    // Displays the file dialogue box and select the file:
    let file_path = file_dialog.save_file();
    match file_path {
        Some(path) => {
            info!("User selected file for writing operation: {path:?}");
            Json(FileSaveResponse {
                user_cancelled: false,
                save_file_path: path.to_str().unwrap().to_string(),
            })
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
fn apply_filter(file_dialog: FileDialogBuilder, filter: &Option<FileTypeFilter>) -> FileDialogBuilder {
    match filter {
        Some(f) => file_dialog.add_filter(
            &f.filter_name,
            &f.filter_extensions.iter().map(|s| s.as_str()).collect::<Vec<&str>>(),
        ),

        None => file_dialog,
    }
}