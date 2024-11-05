// Prevents an additional console window on Windows in release, DO NOT REMOVE!!
#![cfg_attr(not(debug_assertions), windows_subsystem = "windows")]

extern crate rocket;
extern crate core;

use std::collections::HashSet;
use once_cell::sync::Lazy;

use arboard::Clipboard;
use serde::Serialize;
use log::{debug, error, info, warn};
use rcgen::generate_simple_self_signed;
use rocket::figment::Figment;
use rocket::{post, routes};
use rocket::config::{Shutdown};
use rocket::serde::json::Json;
use sha2::{Sha256, Digest};
use mindwork_ai_studio::api_token::APIToken;
use mindwork_ai_studio::app_window::start_tauri;
use mindwork_ai_studio::dotnet::start_dotnet_server;
use mindwork_ai_studio::encryption::{EncryptedText, ENCRYPTION};
use mindwork_ai_studio::environment::is_dev;
use mindwork_ai_studio::log::init_logging;
use mindwork_ai_studio::network::get_available_port;

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
                mindwork_ai_studio::dotnet::dotnet_port,
                mindwork_ai_studio::dotnet::dotnet_ready,
                set_clipboard,
                mindwork_ai_studio::app_window::check_for_update,
                mindwork_ai_studio::app_window::install_update,
                mindwork_ai_studio::secret::get_secret,
                mindwork_ai_studio::secret::store_secret,
                mindwork_ai_studio::secret::delete_secret,
                mindwork_ai_studio::environment::get_data_directory,
                mindwork_ai_studio::environment::get_config_directory,
            ])
            .ignite().await.unwrap()
            .launch().await.unwrap();
    });

    info!("Secret password for the IPC channel was generated successfully.");
    start_dotnet_server(*API_SERVER_PORT, certificate_fingerprint);
    start_tauri();
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