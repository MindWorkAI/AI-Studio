use std::sync::Mutex;
use std::time::Duration;
use log::{error, info, warn};
use once_cell::sync::Lazy;
use rocket::get;
use rocket::serde::json::Json;
use rocket::serde::Serialize;
use tauri::updater::UpdateResponse;
use tauri::{Manager, Window};
use tokio::time;
use crate::api_token::APIToken;
use crate::dotnet::stop_dotnet_server;
use crate::environment::{is_prod, CONFIG_DIRECTORY, DATA_DIRECTORY};
use crate::log::switch_to_file_logging;

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
            let logger_path = app.path_resolver().app_local_data_dir().unwrap();
            let logger_path = logger_path.join("data");

            DATA_DIRECTORY.set(logger_path.to_str().unwrap().to_string()).map_err(|_| error!("Was not abe to set the data directory.")).unwrap();
            CONFIG_DIRECTORY.set(app.path_resolver().app_config_dir().unwrap().to_str().unwrap().to_string()).map_err(|_| error!("Was not able to set the config directory.")).unwrap();

            info!(Source = "Bootloader Tauri"; "Reconfigure the file logger to use the app data directory {logger_path:?}");
            switch_to_file_logging(logger_path).map_err(|e| error!("Failed to switch logging to file: {e}")).unwrap();

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