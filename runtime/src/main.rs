// Prevents an additional console window on Windows in release, DO NOT REMOVE!!
#![cfg_attr(not(debug_assertions), windows_subsystem = "windows")]

extern crate rocket;
extern crate core;

use std::collections::{HashMap, HashSet};
use std::net::TcpListener;
use std::sync::{Arc, Mutex, OnceLock};
use std::time::Duration;
use once_cell::sync::Lazy;

use arboard::Clipboard;
use base64::Engine;
use base64::prelude::BASE64_STANDARD;
use keyring::Entry;
use serde::{Deserialize, Serialize};
use tauri::{Manager, Url, Window};
use tauri::api::process::{Command, CommandChild, CommandEvent};
use tokio::time;
use keyring::error::Error::NoEntry;
use log::{debug, error, info, warn};
use rand::{RngCore, SeedableRng};
use rcgen::generate_simple_self_signed;
use rocket::figment::Figment;
use rocket::{get, post, routes, Request};
use rocket::config::{Shutdown};
use rocket::http::Status;
use rocket::request::{FromRequest};
use rocket::serde::json::Json;
use sha2::{Sha256, Digest};
use tauri::updater::UpdateResponse;

use mindwork_ai_studio::encryption::{EncryptedText, ENCRYPTION};
use mindwork_ai_studio::environment::{is_dev, is_prod};
use mindwork_ai_studio::log::{init_logging, switch_to_file_logging};


// The .NET server is started in a separate process and communicates with this
// runtime process via IPC. However, we do net start the .NET server in
// the development environment.
static DOTNET_SERVER: Lazy<Arc<Mutex<Option<CommandChild>>>> = Lazy::new(|| Arc::new(Mutex::new(None)));

// The .NET server port is relevant for the production environment only, sine we
// do not start the server in the development environment.
static DOTNET_SERVER_PORT: Lazy<u16> = Lazy::new(|| get_available_port().unwrap());

// The port used for the runtime API server. In the development environment, we use a fixed
// port, in the production environment we use the next available port. This differentiation
// is necessary because we cannot communicate the port to the .NET server in the development
// environment.
static API_SERVER_PORT: Lazy<u16> = Lazy::new(|| {
    if is_dev() {
        5000
    } else {
        get_available_port().unwrap()
    }
});

// The Tauri main window.
static MAIN_WINDOW: Lazy<Mutex<Option<Window>>> = Lazy::new(|| Mutex::new(None));

// The update response coming from the Tauri updater.
static CHECK_UPDATE_RESPONSE: Lazy<Mutex<Option<UpdateResponse<tauri::Wry>>>> = Lazy::new(|| Mutex::new(None));


static DOTNET_INITIALIZED: Lazy<Mutex<bool>> = Lazy::new(|| Mutex::new(false));

#[tokio::main]
async fn main() {

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

    init_logging();

    info!("Starting MindWork AI Studio:");
    let working_directory = std::env::current_dir().unwrap();
    info!(".. The working directory is: '{working_directory:?}'");
    
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

    info!("Try to generate a TLS certificate for the runtime API server...");

    let subject_alt_names = vec!["localhost".to_string()];
    let certificate_data = generate_simple_self_signed(subject_alt_names).unwrap();
    let certificate_binary_data = certificate_data.cert.der().to_vec();
    let certificate_fingerprint = Sha256::digest(certificate_binary_data).to_vec();
    let certificate_fingerprint = certificate_fingerprint.iter().fold(String::new(), |mut result, byte| {
        result.push_str(&format!("{:02x}", byte));
        result
    });
    let certificate_fingerprint = certificate_fingerprint.to_uppercase();
    info!("Certificate fingerprint: '{certificate_fingerprint}'.");
    info!("Done generating certificate for the runtime API server.");

    let api_port = *API_SERVER_PORT;
    info!("Try to start the API server on 'http://localhost:{api_port}'...");

    // The shutdown configuration for the runtime API server:
    let mut shutdown = Shutdown {
        // We do not want to use the Ctrl+C signal to stop the server:
        ctrlc: false,

        // Everything else is set to default for now:
        ..Shutdown::default()
    };

    #[cfg(unix)]
    {
        // We do not want to use the termination signal to stop the server.
        // This option, however, is only available on Unix systems:
        shutdown.signals = HashSet::new();
    }

    // Configure the runtime API server:
    let figment = Figment::from(rocket::Config::release_default())

        // We use the next available port which was determined before:
        .merge(("port", api_port))

        // The runtime API server should be accessible only from the local machine:
        .merge(("address", "127.0.0.1"))

        // We do not want to use the Ctrl+C signal to stop the server:
        .merge(("ctrlc", false))

        // Set a name for the server:
        .merge(("ident", "AI Studio Runtime API"))

        // Set the maximum number of workers and blocking threads:
        .merge(("workers", 3))
        .merge(("max_blocking", 12))

        // No colors and emojis in the log output:
        .merge(("cli_colors", false))

        // Read the TLS certificate and key from the generated certificate data in-memory:
        .merge(("tls.certs", certificate_data.cert.pem().as_bytes()))
        .merge(("tls.key", certificate_data.key_pair.serialize_pem().as_bytes()))

        // Set the shutdown configuration:
        .merge(("shutdown", shutdown));

    //
    // Start the runtime API server in a separate thread. This is necessary
    // because the server is blocking, and we need to run the Tauri app in
    // parallel:
    //
    tauri::async_runtime::spawn(async move {
        rocket::custom(figment)
            .mount("/", routes![
                dotnet_port, dotnet_ready, set_clipboard, check_for_update, install_update,
                get_secret, store_secret, delete_secret, get_data_directory, get_config_directory,
            ])
            .ignite().await.unwrap()
            .launch().await.unwrap();
    });

    // Get the secret password & salt and convert it to a base64 string:
    let secret_password = BASE64_STANDARD.encode(ENCRYPTION.secret_password);
    let secret_key_salt = BASE64_STANDARD.encode(ENCRYPTION.secret_key_salt);

    let dotnet_server_environment = HashMap::from_iter([
        (String::from("AI_STUDIO_SECRET_PASSWORD"), secret_password),
        (String::from("AI_STUDIO_SECRET_KEY_SALT"), secret_key_salt),
        (String::from("AI_STUDIO_CERTIFICATE_FINGERPRINT"), certificate_fingerprint),
        (String::from("AI_STUDIO_API_TOKEN"), API_TOKEN.to_hex_text().to_string()),
    ]);

    info!("Secret password for the IPC channel was generated successfully.");
    info!("Try to start the .NET server...");
    let server_spawn_clone = DOTNET_SERVER.clone();
    tauri::async_runtime::spawn(async move {
        let api_port = *API_SERVER_PORT;

        let (mut rx, child) = match is_dev() {
            true => {
                // We are in the development environment, so we try to start a process
                // with `dotnet run` in the `../app/MindWork AI Studio` directory. But
                // we cannot issue a sidecar because we cannot use any command for the
                // sidecar (see Tauri configuration). Thus, we use a standard Rust process:
                warn!(Source = "Bootloader .NET"; "Development environment detected; start .NET server using 'dotnet run'.");
                Command::new("dotnet")

                    // Start the .NET server in the `../app/MindWork AI Studio` directory.
                    // We provide the runtime API server port to the .NET server:
                    .args(["run", "--project", "../app/MindWork AI Studio", "--", format!("{api_port}").as_str()])

                    .envs(dotnet_server_environment)
                    .spawn()
                    .expect("Failed to spawn .NET server process.")
            }

            false => {
                Command::new_sidecar("mindworkAIStudioServer")
                    .expect("Failed to create sidecar")

                    // Provide the runtime API server port to the .NET server:
                    .args([format!("{api_port}").as_str()])

                    .envs(dotnet_server_environment)
                    .spawn()
                    .expect("Failed to spawn .NET server process.")
            }
        };

        let server_pid = child.pid();
        info!(Source = "Bootloader .NET"; "The .NET server process started with PID={server_pid}.");

        // Save the server process to stop it later:
        *server_spawn_clone.lock().unwrap() = Some(child);

        // Log the output of the .NET server:
        while let Some(CommandEvent::Stdout(line)) = rx.recv().await {

            // Remove newline characters from the end:
            let line = line.trim_end();

            // Starts the line with '=>'?
            if line.starts_with("=>") {
                // Yes. This means that the line is a log message from the .NET server.
                // The format is: '<YYYY-MM-dd HH:mm:ss.fff> [<log level>] <source>: <message>'.
                // We try to parse this line and log it with the correct log level:
                let line = line.trim_start_matches("=>").trim();
                let parts = line.split_once(": ").unwrap();
                let left_part = parts.0.trim();
                let message = parts.1.trim();
                let parts = left_part.split_once("] ").unwrap();
                let level = parts.0.split_once("[").unwrap().1.trim();
                let source = parts.1.trim();
                match level {
                    "Trace" => debug!(Source = ".NET Server", Comp = source; "{message}"),
                    "Debug" => debug!(Source = ".NET Server", Comp = source; "{message}"),
                    "Information" => info!(Source = ".NET Server", Comp = source; "{message}"),
                    "Warning" => warn!(Source = ".NET Server", Comp = source; "{message}"),
                    "Error" => error!(Source = ".NET Server", Comp = source; "{message}"),
                    "Critical" => error!(Source = ".NET Server", Comp = source; "{message}"),

                    _ => error!(Source = ".NET Server", Comp = source; "{message} (unknown log level '{level}')"),
                }
            } else {
                let lower_line = line.to_lowercase();
                if lower_line.contains("error") {
                    error!(Source = ".NET Server"; "{line}");
                } else if lower_line.contains("warning") {
                    warn!(Source = ".NET Server"; "{line}");
                } else {
                    info!(Source = ".NET Server"; "{line}");
                }
            }
        }
    });

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
                    stop_servers();
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
        stop_servers();
    }
}

    }

#[get("/system/dotnet/port")]
fn dotnet_port(_token: APIToken) -> String {
    let dotnet_server_port = *DOTNET_SERVER_PORT;
    format!("{dotnet_server_port}")
}


#[get("/system/dotnet/ready")]
async fn dotnet_ready(_token: APIToken) {
    
    // We create a manual scope for the lock to be released as soon as possible.
    // This is necessary because we cannot await any function while the lock is
    // held.
    {
        let mut initialized = DOTNET_INITIALIZED.lock().unwrap();
        if *initialized {
            error!("Anyone tried to initialize the runtime twice. This is not intended.");
            return;
        }

        info!("The .NET server was booted successfully.");
        *initialized = true;
    }

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
    
    let main_window = main_window_spawn_clone.lock().unwrap();
    let dotnet_server_port = *DOTNET_SERVER_PORT;
    let url = match Url::parse(format!("http://localhost:{dotnet_server_port}").as_str())
    {
        Ok(url) => url,
        Err(msg) => {
            error!("Error while parsing URL for navigating to the app: {msg}");
            return;
        }
    };
    
    let js_location_change = format!("window.location = '{url}';");
    let location_change_result = main_window.as_ref().unwrap().eval(js_location_change.as_str());
    match location_change_result {
        Ok(_) => info!("The app location was changed to {url}."),
        Err(e) => error!("Failed to change the app location to {url}: {e}."),
    }
}


fn stop_servers() {
    if let Some(server_process) = DOTNET_SERVER.lock().unwrap().take() {
        let server_kill_result = server_process.kill();
        match server_kill_result {
            Ok(_) => info!("The .NET server process was stopped."),
            Err(e) => error!("Failed to stop the .NET server process: {e}."),
        }
    } else {
        warn!("The .NET server process was not started or is already stopped.");
    }
}

#[get("/updates/check")]
async fn check_for_update(_token: APIToken) -> Json<CheckUpdateResponse> {
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

#[derive(Serialize)]
struct CheckUpdateResponse {
    update_is_available: bool,
    error: bool,
    new_version: String,
    changelog: String,
}

#[get("/updates/install")]
async fn install_update(_token: APIToken) {
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

#[post("/secrets/store", data = "<request>")]
fn store_secret(_token: APIToken, request: Json<StoreSecret>) -> Json<StoreSecretResponse> {
    let user_name = request.user_name.as_str();
    let decrypted_text = match ENCRYPTION.decrypt(&request.secret) {
        Ok(text) => text,
        Err(e) => {
            error!(Source = "Secret Store"; "Failed to decrypt the text: {e}.");
            return Json(StoreSecretResponse {
                success: false,
                issue: format!("Failed to decrypt the text: {e}"),
            })
        },
    };

    let service = format!("mindwork-ai-studio::{}", request.destination);
    let entry = Entry::new(service.as_str(), user_name).unwrap();
    let result = entry.set_password(decrypted_text.as_str());
    match result {
        Ok(_) => {
            info!(Source = "Secret Store"; "Secret for {service} and user {user_name} was stored successfully.");
            Json(StoreSecretResponse {
                success: true,
                issue: String::from(""),
            })
        },
        
        Err(e) => {
            error!(Source = "Secret Store"; "Failed to store secret for {service} and user {user_name}: {e}.");
            Json(StoreSecretResponse {
                success: false,
                issue: e.to_string(),
            })
        },
    }
}

#[derive(Deserialize)]
struct StoreSecret {
    destination: String,
    user_name: String,
    secret: EncryptedText,
}

#[derive(Serialize)]
struct StoreSecretResponse {
    success: bool,
    issue: String,
}

#[post("/secrets/get", data = "<request>")]
fn get_secret(_token: APIToken, request: Json<RequestSecret>) -> Json<RequestedSecret> {
    let user_name = request.user_name.as_str();
    let service = format!("mindwork-ai-studio::{}", request.destination);
    let entry = Entry::new(service.as_str(), user_name).unwrap();
    let secret = entry.get_password();
    match secret {
        Ok(s) => {
            info!(Source = "Secret Store"; "Secret for '{service}' and user '{user_name}' was retrieved successfully.");

            // Encrypt the secret:
            let encrypted_secret = match ENCRYPTION.encrypt(s.as_str()) {
                Ok(e) => e,
                Err(e) => {
                    error!(Source = "Secret Store"; "Failed to encrypt the secret: {e}.");
                    return Json(RequestedSecret {
                        success: false,
                        secret: EncryptedText::new(String::from("")),
                        issue: format!("Failed to encrypt the secret: {e}"),
                    });
                },
            };

            Json(RequestedSecret {
                success: true,
                secret: encrypted_secret,
                issue: String::from(""),
            })
        },

        Err(e) => {
            if !request.is_trying {
                error!(Source = "Secret Store"; "Failed to retrieve secret for '{service}' and user '{user_name}': {e}.");
            }
            
            Json(RequestedSecret {
                success: false,
                secret: EncryptedText::new(String::from("")),
                issue: format!("Failed to retrieve secret for '{service}' and user '{user_name}': {e}"),
            })
        },
    }
}

#[derive(Deserialize)]
struct RequestSecret {
    destination: String,
    user_name: String,
    is_trying: bool,
}

#[derive(Serialize)]
struct RequestedSecret {
    success: bool,
    secret: EncryptedText,
    issue: String,
}

#[post("/secrets/delete", data = "<request>")]
fn delete_secret(_token: APIToken, request: Json<RequestSecret>) -> Json<DeleteSecretResponse> {
    let user_name = request.user_name.as_str();
    let service = format!("mindwork-ai-studio::{}", request.destination);
    let entry = Entry::new(service.as_str(), user_name).unwrap();
    let result = entry.delete_credential();

    match result {
        Ok(_) => {
            warn!(Source = "Secret Store"; "Secret for {service} and user {user_name} was deleted successfully.");
            Json(DeleteSecretResponse {
                success: true,
                was_entry_found: true,
                issue: String::from(""),
            })
        },

        Err(NoEntry) => {
            warn!(Source = "Secret Store"; "No secret for {service} and user {user_name} was found.");
            Json(DeleteSecretResponse {
                success: true,
                was_entry_found: false,
                issue: String::from(""),
            })
        }
        
        Err(e) => {
            error!(Source = "Secret Store"; "Failed to delete secret for {service} and user {user_name}: {e}.");
            Json(DeleteSecretResponse {
                success: false,
                was_entry_found: false,
                issue: e.to_string(),
            })
        },
    }
}

#[derive(Serialize)]
struct DeleteSecretResponse {
    success: bool,
    was_entry_found: bool,
    issue: String,
}

#[post("/clipboard/set", data = "<encrypted_text>")]
fn set_clipboard(_token: APIToken, encrypted_text: EncryptedText) -> Json<SetClipboardResponse> {

    // Decrypt this text first:
    let decrypted_text = match ENCRYPTION.decrypt(&encrypted_text) {
        Ok(text) => text,
        Err(e) => {
            error!(Source = "Clipboard"; "Failed to decrypt the text: {e}.");
            return Json(SetClipboardResponse {
                success: false,
                issue: e,
            })
        },
    };

    let clipboard_result = Clipboard::new();
    let mut clipboard = match clipboard_result {
        Ok(clipboard) => clipboard,
        Err(e) => {
            error!(Source = "Clipboard"; "Failed to get the clipboard instance: {e}.");
            return Json(SetClipboardResponse {
                success: false,
                issue: e.to_string(),
            })
        },
    };
    
    let set_text_result = clipboard.set_text(decrypted_text);
    match set_text_result {
        Ok(_) => {
            debug!(Source = "Clipboard"; "Text was set to the clipboard successfully.");
            Json(SetClipboardResponse {
                success: true,
                issue: String::from(""),
            })
        },
        
        Err(e) => {
            error!(Source = "Clipboard"; "Failed to set text to the clipboard: {e}.");
            Json(SetClipboardResponse {
                success: false,
                issue: e.to_string(),
            })
        },
    }
}

#[derive(Serialize)]
struct SetClipboardResponse {
    success: bool,
    issue: String,
}