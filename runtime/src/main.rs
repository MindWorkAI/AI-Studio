// Prevents an additional console window on Windows in release, DO NOT REMOVE!!
#![cfg_attr(not(debug_assertions), windows_subsystem = "windows")]

extern crate rocket;
extern crate core;

use log::{info, warn};
use mindwork_ai_studio::app_window::start_tauri;
use mindwork_ai_studio::certificate::{generate_certificate};
use mindwork_ai_studio::dotnet::start_dotnet_server;
use mindwork_ai_studio::environment::is_dev;
use mindwork_ai_studio::log::init_logging;
use mindwork_ai_studio::runtime_api::start_runtime_api;

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
    let architecture = metadata_lines.next().unwrap();
    let pdfium_version = metadata_lines.next().unwrap();

    init_logging();
    info!("Starting MindWork AI Studio:");
    
    let working_directory = std::env::current_dir().unwrap();
    info!(".. The working directory is: '{working_directory:?}'");
    
    info!(".. Version: v{app_version} (commit {app_commit_hash}, build {build_number}, {architecture})");
    info!(".. Build time: {build_time}");
    info!(".. .NET SDK: v{dotnet_sdk_version}");
    info!(".. .NET: v{dotnet_version}");
    info!(".. Rust: v{rust_version}");
    info!(".. MudBlazor: v{mud_blazor_version}");
    info!(".. Tauri: v{tauri_version}");
    info!(".. PDFium: v{pdfium_version}");

    if is_dev() {
        warn!("Running in development mode.");
    } else {
        info!("Running in production mode.");
    }

    generate_certificate();
    start_runtime_api();
    start_dotnet_server();
    start_tauri();
}