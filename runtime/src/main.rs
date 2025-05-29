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
use mindwork_ai_studio::metadata::MetaData;
use mindwork_ai_studio::runtime_api::start_runtime_api;

#[cfg(debug_assertions)]
use mindwork_ai_studio::dotnet::create_startup_env_file;

#[tokio::main]
async fn main() {
    let metadata = MetaData::init_from_string(include_str!("../../metadata.txt"));

    init_logging();
    info!("Starting MindWork AI Studio:");
    
    let working_directory = std::env::current_dir().unwrap();
    info!(".. The working directory is: '{working_directory:?}'");
    
    info!(".. Version: v{app_version} (commit {hash}, build {build_num}, {architecture})",
        app_version = metadata.app_version,
        hash = metadata.app_commit_hash,
        build_num = metadata.build_number,
        architecture = metadata.architecture);
    info!(".. Build time: {build_time}", build_time = metadata.build_time);
    info!(".. .NET SDK: v{sdk_version}", sdk_version = metadata.dotnet_sdk_version);
    info!(".. .NET: v{dotnet_version}", dotnet_version = metadata.dotnet_version);
    info!(".. Rust: v{rust_version}", rust_version = metadata.rust_version);
    info!(".. MudBlazor: v{mud_blazor_version}", mud_blazor_version = metadata.mud_blazor_version);
    info!(".. Tauri: v{tauri_version}", tauri_version = metadata.tauri_version);
    info!(".. PDFium: v{pdfium_version}", pdfium_version = metadata.pdfium_version);

    if is_dev() {
        warn!("Running in development mode.");
    } else {
        info!("Running in production mode.");
    }

    generate_certificate();
    start_runtime_api();
    
    if is_dev() {
        #[cfg(debug_assertions)]
        create_startup_env_file();
    } else {
        start_dotnet_server();
    }
    
    start_tauri();
}