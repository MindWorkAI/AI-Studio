use std::sync::Mutex;
use std::time::Duration;
use log::{error, info, warn};
use once_cell::sync::Lazy;
use rocket::{get, post};
use rocket::serde::json::Json;
use rocket::serde::Serialize;
use serde::Deserialize;
use tauri::updater::UpdateResponse;
use tauri::{Manager, PathResolver, Window};
use tauri::api::dialog::blocking::FileDialogBuilder;
use tokio::time;
use crate::api_token::APIToken;
use crate::dotnet::stop_dotnet_server;
use crate::environment::{is_prod, CONFIG_DIRECTORY, DATA_DIRECTORY};
use crate::log::switch_to_file_logging;
use crate::pdfium::PDFIUM_LIB_PATH;

/// The Tauri main window.
static MAIN_WINDOW: Lazy<Mutex<Option<Window>>> = Lazy::new(|| Mutex::new(None));

/// The update response coming from the Tauri updater.
static CHECK_UPDATE_RESPONSE: Lazy<Mutex<Option<UpdateResponse<tauri::Wry>>>> = Lazy::new(|| Mutex::new(None));

/// Starts the Tauri app.
pub fn start_tauri() {
    info!("Starting Tauri app...");
    let app = tauri::Builder::default()
        .setup(move |app| {
            let window = app.get_window("main").expect("Failed to get main window.");
            *MAIN_WINDOW.lock().unwrap() = Some(window);

            info!(Source = "Bootloader Tauri"; "Setup is running.");
            let data_path = app.path_resolver().app_local_data_dir().unwrap();
            let data_path = data_path.join("data");

            DATA_DIRECTORY.set(data_path.to_str().unwrap().to_string()).map_err(|_| error!("Was not abe to set the data directory.")).unwrap();
            CONFIG_DIRECTORY.set(app.path_resolver().app_config_dir().unwrap().to_str().unwrap().to_string()).map_err(|_| error!("Was not able to set the config directory.")).unwrap();

            info!(Source = "Bootloader Tauri"; "Reconfigure the file logger to use the app data directory {data_path:?}");
            switch_to_file_logging(data_path).map_err(|e| error!("Failed to switch logging to file: {e}")).unwrap();
            set_pdfium_path(app.path_resolver());
            Ok(())
        })
        .plugin(tauri_plugin_window_state::Builder::default().build())
        .build(tauri::generate_context!())
        .expect("Error while running Tauri application");

    app.run(|app_handle, event| match event {

        tauri::RunEvent::WindowEvent { event, label, .. } => {
            match event {
                tauri::WindowEvent::CloseRequested { .. } => {
                    warn!(Source = "Tauri"; "Window '{label}': close was requested.");
                }

                tauri::WindowEvent::Destroyed => {
                    warn!(Source = "Tauri"; "Window '{label}': was destroyed.");
                }

                tauri::WindowEvent::FileDrop(files) => {
                    info!(Source = "Tauri"; "Window '{label}': files were dropped: {files:?}");
                }

                _ => (),
            }
        }

        tauri::RunEvent::Updater(updater_event) => {
            match updater_event {

                tauri::UpdaterEvent::UpdateAvailable { body, date, version } => {
                    let body_len = body.len();
                    info!(Source = "Tauri"; "Updater: update available: body size={body_len} time={date:?} version={version}");
                }

                tauri::UpdaterEvent::Pending => {
                    info!(Source = "Tauri"; "Updater: update is pending!");
                }

                tauri::UpdaterEvent::DownloadProgress { chunk_length, content_length } => {
                    info!(Source = "Tauri"; "Updater: downloaded {} of {:?}", chunk_length, content_length);
                }

                tauri::UpdaterEvent::Downloaded => {
                    info!(Source = "Tauri"; "Updater: update has been downloaded!");
                    warn!(Source = "Tauri"; "Try to stop the .NET server now...");
                    stop_dotnet_server();
                }

                tauri::UpdaterEvent::Updated => {
                    info!(Source = "Tauri"; "Updater: app has been updated");
                    warn!(Source = "Tauri"; "Try to restart the app now...");
                    app_handle.restart();
                }

                tauri::UpdaterEvent::AlreadyUpToDate => {
                    info!(Source = "Tauri"; "Updater: app is already up to date");
                }

                tauri::UpdaterEvent::Error(error) => {
                    warn!(Source = "Tauri"; "Updater: failed to update: {error}");
                }
            }
        }

        tauri::RunEvent::ExitRequested { .. } => {
            warn!(Source = "Tauri"; "Run event: exit was requested.");
        }

        tauri::RunEvent::Ready => {
            info!(Source = "Tauri"; "Run event: Tauri app is ready.");
        }

        _ => {}
    });

    warn!(Source = "Tauri"; "Tauri app was stopped.");
    if is_prod() {
        warn!("Try to stop the .NET server as well...");
        stop_dotnet_server();
    }
}

/// Changes the location of the main window to the given URL.
pub async fn change_location_to(url: &str) {
    // Try to get the main window. If it is not available yet, wait for it:
    let mut main_window_ready = false;
    let mut main_window_status_reported = false;
    let main_window_spawn_clone = &MAIN_WINDOW;
    while !main_window_ready
    {
        main_window_ready = {
            let main_window = main_window_spawn_clone.lock().unwrap();
            main_window.is_some()
        };

        if !main_window_ready {
            if !main_window_status_reported {
                info!("Waiting for main window to be ready, because .NET was faster than Tauri.");
                main_window_status_reported = true;
            }

            time::sleep(Duration::from_millis(100)).await;
        }
    }

    let js_location_change = format!("window.location = '{url}';");
    let main_window = main_window_spawn_clone.lock().unwrap();
    let location_change_result = main_window.as_ref().unwrap().eval(js_location_change.as_str());
    match location_change_result {
        Ok(_) => info!("The app location was changed to {url}."),
        Err(e) => error!("Failed to change the app location to {url}: {e}."),
    }
}

/// Checks for updates.
#[get("/updates/check")]
pub async fn check_for_update(_token: APIToken) -> Json<CheckUpdateResponse> {
    let app_handle = MAIN_WINDOW.lock().unwrap().as_ref().unwrap().app_handle();
    let response = app_handle.updater().check().await;
    match response {
        Ok(update_response) => match update_response.is_update_available() {
            true => {
                *CHECK_UPDATE_RESPONSE.lock().unwrap() = Some(update_response.clone());
                let new_version = update_response.latest_version();
                info!(Source = "Updater"; "An update to version '{new_version}' is available.");
                let changelog = update_response.body();
                Json(CheckUpdateResponse {
                    update_is_available: true,
                    error: false,
                    new_version: new_version.to_string(),
                    changelog: match changelog {
                        Some(c) => c.to_string(),
                        None => String::from(""),
                    },
                })
            },

            false => {
                info!(Source = "Updater"; "No updates are available.");
                Json(CheckUpdateResponse {
                    update_is_available: false,
                    error: false,
                    new_version: String::from(""),
                    changelog: String::from(""),
                })
            },
        },

        Err(e) => {
            warn!(Source = "Updater"; "Failed to check for updates: {e}.");
            Json(CheckUpdateResponse {
                update_is_available: false,
                error: true,
                new_version: String::from(""),
                changelog: String::from(""),
            })
        },
    }
}

/// The response to the check for update request.
#[derive(Serialize)]
pub struct CheckUpdateResponse {
    update_is_available: bool,
    error: bool,
    new_version: String,
    changelog: String,
}

/// Installs the update.
#[get("/updates/install")]
pub async fn install_update(_token: APIToken) {
    let cloned_response_option = CHECK_UPDATE_RESPONSE.lock().unwrap().clone();
    match cloned_response_option {
        Some(update_response) => {
            update_response.download_and_install().await.unwrap();
        },

        None => {
            error!(Source = "Updater"; "No update available to install. Did you check for updates first?");
        },
    }
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

#[derive(Serialize)]
pub struct DirectorySelectionResponse {
    user_cancelled: bool,
    selected_directory: String,
}

/// Let the user select a file.
#[post("/select/file", data = "<payload>")]
pub fn select_file(_token: APIToken, payload: Json<SelectFileOptions>) -> Json<FileSelectionResponse> {

    // Create a new file dialog builder:
    let file_dialog = FileDialogBuilder::new();

    // Set the title of the file dialog:
    let file_dialog = file_dialog.set_title(&payload.title);

    // Set the file type filter if provided:
    let file_dialog = match &payload.filter {
        Some(filter) => {
            file_dialog.add_filter(&filter.filter_name, &filter.filter_extensions.iter().map(|s| s.as_str()).collect::<Vec<&str>>())
        },

        None => file_dialog,
    };

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

#[derive(Clone, Deserialize)]
pub struct PreviousFile {
    file_path: String,
}

#[derive(Serialize)]
pub struct FileSelectionResponse {
    user_cancelled: bool,
    selected_file_path: String,
}

fn set_pdfium_path(path_resolver: PathResolver) {
    let pdfium_relative_source_path = String::from("resources/libraries/");
    let pdfium_source_path = path_resolver.resolve_resource(pdfium_relative_source_path);
    if pdfium_source_path.is_none() {
        error!(Source = "Bootloader Tauri"; "Failed to set the PDFium library path.");
        return;
    }

    let pdfium_source_path = pdfium_source_path.unwrap();
    let pdfium_source_path = pdfium_source_path.to_str().unwrap().to_string();
    *PDFIUM_LIB_PATH.lock().unwrap() = Some(pdfium_source_path.clone());
}