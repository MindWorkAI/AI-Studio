// Prevents an additional console window on Windows in release, DO NOT REMOVE!!
#![cfg_attr(not(debug_assertions), windows_subsystem = "windows")]

extern crate core;

use std::net::TcpListener;
use std::sync::{Arc, Mutex};
use once_cell::sync::Lazy;

use arboard::Clipboard;
use keyring::Entry;
use serde::Serialize;
use tauri::{Manager, Url, Window, WindowUrl};
use tauri::api::process::{Command, CommandChild, CommandEvent};
use tauri::utils::config::AppUrl;
use tokio::time;
use flexi_logger::{AdaptiveFormat, Logger};
use log::{debug, error, info, warn};
use tauri::updater::UpdateResponse;

static SERVER: Lazy<Arc<Mutex<Option<CommandChild>>>> = Lazy::new(|| Arc::new(Mutex::new(None)));
static MAIN_WINDOW: Lazy<Mutex<Option<Window>>> = Lazy::new(|| Mutex::new(None));
static CHECK_UPDATE_RESPONSE: Lazy<Mutex<Option<UpdateResponse<tauri::Wry>>>> = Lazy::new(|| Mutex::new(None));

fn main() {

    let metadata = include_str!("../../metadata.txt");
    let mut metadata_lines = metadata.lines();
    let app_version = metadata_lines.next().unwrap();
    let build_time = metadata_lines.next().unwrap();
    let build_number = metadata_lines.next().unwrap();
    let dotnet_sdk_version = metadata_lines.next().unwrap();
    let dotnet_version = metadata_lines.next().unwrap();
    let rust_version = metadata_lines.next().unwrap();
    let mud_blazor_version = metadata_lines.next().unwrap();
    let tauri_version = metadata_lines.next().unwrap();
    let app_commit_hash = metadata_lines.next().unwrap();

    // Set the log level according to the environment:
    // In debug mode, the log level is set to debug, in release mode to info.
    let log_level = match is_dev() {
        true => "debug",
        false => "info",
    };

    Logger::try_with_str(log_level).expect("Cannot create logging")
        .log_to_stdout()
        .adaptive_format_for_stdout(AdaptiveFormat::Detailed)
        .start().expect("Cannot start logging");

    info!("Starting MindWork AI Studio:");
    info!(".. Version: v{app_version} (commit {app_commit_hash}, build {build_number})");
    info!(".. Build time: {build_time}");
    info!(".. .NET SDK: v{dotnet_sdk_version}");
    info!(".. .NET: v{dotnet_version}");
    info!(".. Rust: v{rust_version}");
    info!(".. MudBlazor: v{mud_blazor_version}");
    info!(".. Tauri: v{tauri_version}");

    if is_dev() {
        warn!("Running in development mode.");
    } else {
        info!("Running in production mode.");
    }

    let port = match is_dev() {
        true => 5000,
        false => get_available_port().unwrap(),
    };

    let url = match Url::parse(format!("http://localhost:{port}").as_str())
    {
        Ok(url) => url,
        Err(msg) => {
            error!("Error while parsing URL: {msg}");
            return;
        }
    };

    let app_url = AppUrl::Url(WindowUrl::External(url.clone()));
    let app_url_log = app_url.clone();
    info!("Try to start the .NET server on {app_url_log}...");

    // Arc for the server process to stop it later:
    let server_spawn_clone = SERVER.clone();

    // Channel to communicate with the server process:
    let (sender, mut receiver) = tauri::async_runtime::channel(100);

    if is_prod() {
        tauri::async_runtime::spawn(async move {
            let (mut rx, child) = Command::new_sidecar("mindworkAIStudioServer")
                .expect("Failed to create sidecar")
                .args([format!("{port}").as_str()])
                .spawn()
                .expect("Failed to spawn .NET server process.");

            let server_pid = child.pid();
            debug!(".NET server process started with PID={server_pid}.");

            // Save the server process to stop it later:
            *server_spawn_clone.lock().unwrap() = Some(child);
            
            info!("Waiting for .NET server to boot...");
            while let Some(CommandEvent::Stdout(line)) = rx.recv().await {
                let line_lower = line.to_lowercase();
                let line_cleared = line_lower.trim();
                match line_cleared
                {
                    "rust/tauri server started" => _ = sender.send(ServerEvent::Started).await,

                    _ if line_cleared.contains("fail") || line_cleared.contains("error") || line_cleared.contains("exception") => _ = sender.send(ServerEvent::Error(line)).await,
                    _ if line_cleared.contains("warn") => _ = sender.send(ServerEvent::Warning(line)).await,
                    _ if line_cleared.contains("404") => _ = sender.send(ServerEvent::NotFound(line)).await,
                    _ => (),
                }
            }

            let sending_stop_result = sender.send(ServerEvent::Stopped).await;
            match sending_stop_result {
                Ok(_) => (),
                Err(e) => error!("Was not able to send the server stop message: {e}."),
            }
        });
    } else {
        warn!("Running in development mode, no .NET server will be started.");
    }

    let main_window_spawn_clone = &MAIN_WINDOW;
    let server_receive_clone = SERVER.clone();

    // Create a thread to handle server events:
    tauri::async_runtime::spawn(async move {
        info!("Start listening for server events...");
        loop {
            match receiver.recv().await {
                Some(ServerEvent::Started) => {
                    info!("The .NET server was booted successfully.");

                    // Try to get the main window. If it is not available yet, wait for it:
                    let mut main_window_ready = false;
                    let mut main_window_status_reported = false;
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

                            time::sleep(time::Duration::from_millis(100)).await;
                        }
                    }

                    let main_window = main_window_spawn_clone.lock().unwrap();
                    let js_location_change = format!("window.location = '{url}';");
                    let location_change_result = main_window.as_ref().unwrap().eval(js_location_change.as_str());
                    match location_change_result {
                        Ok(_) => info!("Location was changed to {url}."),
                        Err(e) => error!("Failed to change location to {url}: {e}."),
                    }
                },

                Some(ServerEvent::NotFound(line)) => {
                    warn!("The .NET server issued a 404 error: {line}.");
                },

                Some(ServerEvent::Warning(line)) => {
                    warn!("The .NET server issued a warning: {line}.");
                },

                Some(ServerEvent::Error(line)) => {
                    error!("The .NET server issued an error: {line}.");
                },

                Some(ServerEvent::Stopped) => {
                    warn!("The .NET server was stopped.");
                    *server_receive_clone.lock().unwrap() = None;
                },

                None => {
                    debug!("Server event channel was closed.");
                    break;
                },
            }
        }
    });

    info!("Starting Tauri app...");
    let app = tauri::Builder::default()
        .setup(move |app| {
            let window = app.get_window("main").expect("Failed to get main window.");
            *MAIN_WINDOW.lock().unwrap() = Some(window);
            Ok(())
        })
        .plugin(tauri_plugin_window_state::Builder::default().build())
        .invoke_handler(tauri::generate_handler![
            store_secret, get_secret, delete_secret, set_clipboard,
            check_for_update, install_update
        ])
        .build(tauri::generate_context!())
        .expect("Error while running Tauri application");
    
    app.run(|app_handle, event| match event {

        tauri::RunEvent::WindowEvent { event, label, .. } => {
            match event {
                tauri::WindowEvent::CloseRequested { .. } => {
                    warn!("Window '{label}': close was requested.");
                }

                tauri::WindowEvent::Destroyed => {
                    warn!("Window '{label}': was destroyed.");
                }

                tauri::WindowEvent::FileDrop(files) => {
                    info!("Window '{label}': files were dropped: {files:?}");
                }

                _ => (),
            }
        }

        tauri::RunEvent::Updater(updater_event) => {
            match updater_event {

                tauri::UpdaterEvent::UpdateAvailable { body, date, version } => {
                    let body_len = body.len();
                    info!("Updater: update available: body size={body_len} time={date:?} version={version}");
                }

                tauri::UpdaterEvent::Pending => {
                    info!("Updater: update is pending!");
                }

                tauri::UpdaterEvent::DownloadProgress { chunk_length, content_length } => {
                    info!("Updater: downloaded {} of {:?}", chunk_length, content_length);
                }

                tauri::UpdaterEvent::Downloaded => {
                    info!("Updater: update has been downloaded!");
                    warn!("Try to stop the .NET server now...");
                    stop_server();
                }

                tauri::UpdaterEvent::Updated => {
                    info!("Updater: app has been updated");
                    warn!("Try to restart the app now...");
                    app_handle.restart();
                }

                tauri::UpdaterEvent::AlreadyUpToDate => {
                    info!("Updater: app is already up to date");
                }

                tauri::UpdaterEvent::Error(error) => {
                    warn!("Updater: failed to update: {error}");
                }
            }
        }

        tauri::RunEvent::ExitRequested { .. } => {
            warn!("Run event: exit was requested.");
        }
        
        tauri::RunEvent::Ready => {
            info!("Run event: Tauri app is ready.");
        }

        _ => {}
    });

    info!("Tauri app was stopped.");
    if is_prod() {
        info!("Try to stop the .NET server as well...");
        stop_server();
    }
}

// Enum for server events:
enum ServerEvent {
    Started,
    NotFound(String),
    Warning(String),
    Error(String),
    Stopped,
}

pub fn is_dev() -> bool {
    cfg!(debug_assertions)
}

pub fn is_prod() -> bool {
    !is_dev()
}

fn get_available_port() -> Option<u16> {
    TcpListener::bind(("127.0.0.1", 0))
        .map(|listener| listener.local_addr().unwrap().port())
        .ok()
}

fn stop_server() {
    if let Some(server_process) = SERVER.lock().unwrap().take() {
        let server_kill_result = server_process.kill();
        match server_kill_result {
            Ok(_) => info!("The .NET server process was stopped."),
            Err(e) => error!("Failed to stop the .NET server process: {e}."),
        }
    } else {
        warn!("The .NET server process was not started or already stopped.");
    }
}

#[tauri::command]
async fn check_for_update() -> CheckUpdateResponse {
    let app_handle = MAIN_WINDOW.lock().unwrap().as_ref().unwrap().app_handle();
    tauri::async_runtime::spawn(async move {
        let response = app_handle.updater().check().await;
        match response {
            Ok(update_response) => match update_response.is_update_available() {
                true => {
                    *CHECK_UPDATE_RESPONSE.lock().unwrap() = Some(update_response.clone());
                    let new_version = update_response.latest_version();
                    info!("Updater: update to version '{new_version}' is available.");
                    let changelog = update_response.body();
                    CheckUpdateResponse {
                        update_is_available: true,
                        error: false,
                        new_version: new_version.to_string(),
                        changelog: match changelog {
                            Some(c) => c.to_string(),
                            None => String::from(""),
                        },
                    }
                },

                false => {
                    info!("Updater: no updates available.");
                    CheckUpdateResponse {
                        update_is_available: false,
                        error: false,
                        new_version: String::from(""),
                        changelog: String::from(""),
                    }
                },
            },

            Err(e) => {
                warn!("Failed to check updater: {e}.");
                CheckUpdateResponse {
                    update_is_available: false,
                    error: true,
                    new_version: String::from(""),
                    changelog: String::from(""),
                }
            },
        }
    }).await.unwrap()
}

#[derive(Serialize)]
struct CheckUpdateResponse {
    update_is_available: bool,
    error: bool,
    new_version: String,
    changelog: String,
}

#[tauri::command]
async fn install_update() {
    let cloned_response_option = CHECK_UPDATE_RESPONSE.lock().unwrap().clone();
    match cloned_response_option {
        Some(update_response) => {
            update_response.download_and_install().await.unwrap();
        },

        None => {
            error!("Update installer: no update available to install. Did you check for updates first?");
        },
    }
}

#[tauri::command]
fn store_secret(destination: String, user_name: String, secret: String) -> StoreSecretResponse {
    let service = format!("mindwork-ai-studio::{}", destination);
    let entry = Entry::new(service.as_str(), user_name.as_str()).unwrap();
    let result = entry.set_password(secret.as_str());
    match result {
        Ok(_) => {
            info!("Secret for {service} and user {user_name} was stored successfully.");
            StoreSecretResponse {
                success: true,
                issue: String::from(""),
            }
        },
        
        Err(e) => {
            error!("Failed to store secret for {service} and user {user_name}: {e}.");
            StoreSecretResponse {
                success: false,
                issue: e.to_string(),
            }
        },
    }
}

#[derive(Serialize)]
struct StoreSecretResponse {
    success: bool,
    issue: String,
}

#[tauri::command]
fn get_secret(destination: String, user_name: String) -> RequestedSecret {
    let service = format!("mindwork-ai-studio::{}", destination);
    let entry = Entry::new(service.as_str(), user_name.as_str()).unwrap();
    let secret = entry.get_password();
    match secret {
        Ok(s) => {
            info!("Secret for {service} and user {user_name} was retrieved successfully.");
            RequestedSecret {
                success: true,
                secret: s,
                issue: String::from(""),
            }
        },

        Err(e) => {
            error!("Failed to retrieve secret for {service} and user {user_name}: {e}.");
            RequestedSecret {
                success: false,
                secret: String::from(""),
                issue: e.to_string(),
            }
        },
    }
}

#[derive(Serialize)]
struct RequestedSecret {
    success: bool,
    secret: String,
    issue: String,
}

#[tauri::command]
fn delete_secret(destination: String, user_name: String) -> DeleteSecretResponse {
    let service = format!("mindwork-ai-studio::{}", destination);
    let entry = Entry::new(service.as_str(), user_name.as_str()).unwrap();
    let result = entry.delete_password();
    match result {
        Ok(_) => {
            warn!("Secret for {service} and user {user_name} was deleted successfully.");
            DeleteSecretResponse {
                success: true,
                issue: String::from(""),
            }
        },
        
        Err(e) => {
            error!("Failed to delete secret for {service} and user {user_name}: {e}.");
            DeleteSecretResponse {
                success: false,
                issue: e.to_string(),
            }
        },
    }
}

#[derive(Serialize)]
struct DeleteSecretResponse {
    success: bool,
    issue: String,
}

#[tauri::command]
fn set_clipboard(text: String) -> SetClipboardResponse {
    let clipboard_result = Clipboard::new();
    let mut clipboard = match clipboard_result {
        Ok(clipboard) => clipboard,
        Err(e) => {
            error!("Failed to get the clipboard instance: {e}.");
            return SetClipboardResponse {
                success: false,
                issue: e.to_string(),
            }
        },
    };
    
    let set_text_result = clipboard.set_text(text);
    match set_text_result {
        Ok(_) => {
            debug!("Text was set to the clipboard successfully.");
            SetClipboardResponse {
                success: true,
                issue: String::from(""),
            }
        },
        
        Err(e) => {
            error!("Failed to set text to the clipboard: {e}.");
            SetClipboardResponse {
                success: false,
                issue: e.to_string(),
            }
        },
    }
}

#[derive(Serialize)]
struct SetClipboardResponse {
    success: bool,
    issue: String,
}