// Prevents an additional console window on Windows in release, DO NOT REMOVE!!
#![cfg_attr(not(debug_assertions), windows_subsystem = "windows")]

extern crate rocket;
extern crate core;

use std::collections::HashSet;
use once_cell::sync::Lazy;

use log::{info, warn};
use rcgen::generate_simple_self_signed;
use rocket::figment::Figment;
use rocket::routes;
use rocket::config::{Shutdown};
use sha2::{Sha256, Digest};
use mindwork_ai_studio::app_window::start_tauri;
use mindwork_ai_studio::certificate::{generate_certificate, CERTIFICATE, CERTIFICATE_PRIVATE_KEY};
use mindwork_ai_studio::dotnet::start_dotnet_server;
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

    generate_certificate();

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
        .merge(("tls.certs", CERTIFICATE.get().unwrap()))
        .merge(("tls.key", CERTIFICATE_PRIVATE_KEY.get().unwrap()))

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
                mindwork_ai_studio::clipboard::set_clipboard,
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
    
    start_dotnet_server(*API_SERVER_PORT);
    start_tauri();
}