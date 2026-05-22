use log::info;
use once_cell::sync::Lazy;
use axum::routing::{get, post};
use axum::Router;
use axum_server::tls_rustls::RustlsConfig;
use std::net::SocketAddr;
use std::sync::Once;
use crate::runtime_certificate::{CERTIFICATE, CERTIFICATE_PRIVATE_KEY};
use crate::environment::is_dev;
use crate::network::get_available_port;

static RUSTLS_CRYPTO_PROVIDER_INIT: Once = Once::new();

/// The port used for the runtime API server. In the development environment, we use a fixed
/// port, in the production environment we use the next available port. This differentiation
/// is necessary because we cannot communicate the port to the .NET server in the development
/// environment.
pub static API_SERVER_PORT: Lazy<u16> = Lazy::new(|| {
    if is_dev() {
        5000
    } else {
        get_available_port().unwrap()
    }
});

/// Starts the runtime API server. The server is used to communicate with the .NET server and
/// to provide additional functionality to the Tauri app.
pub fn start_runtime_api() {
    let api_port = *API_SERVER_PORT;
    info!("Try to start the API server on 'http://localhost:{api_port}'...");

    let app = Router::new()
        .route("/system/dotnet/port", get(crate::dotnet::dotnet_port))
        .route("/system/dotnet/ready", get(crate::dotnet::dotnet_ready))
        .route("/system/qdrant/info", get(crate::qdrant::qdrant_port))
        .route("/clipboard/set", post(crate::clipboard::set_clipboard))
        .route("/events", get(crate::app_window::get_event_stream))
        .route("/updates/check", get(crate::app_window::check_for_update))
        .route("/updates/install", get(crate::app_window::install_update))
        .route("/app/exit", post(crate::app_window::exit_app))
        .route("/select/directory", post(crate::file_actions::select_directory))
        .route("/select/file", post(crate::file_actions::select_file))
        .route("/select/files", post(crate::file_actions::select_files))
        .route("/save/file", post(crate::file_actions::save_file))
        .route("/secrets/get", post(crate::secret::get_secret))
        .route("/secrets/store", post(crate::secret::store_secret))
        .route("/secrets/delete", post(crate::secret::delete_secret))
        .route("/system/directories/config", get(crate::environment::get_config_directory))
        .route("/system/directories/data", get(crate::environment::get_data_directory))
        .route("/system/language", get(crate::environment::read_user_language))
        .route("/system/username", get(crate::environment::read_user_name))
        .route("/system/enterprise/config/id", get(crate::environment::read_enterprise_env_config_id))
        .route("/system/enterprise/config/server", get(crate::environment::read_enterprise_env_config_server_url))
        .route("/system/enterprise/config/encryption_secret", get(crate::environment::read_enterprise_env_config_encryption_secret))
        .route("/system/enterprise/configs", get(crate::environment::read_enterprise_configs))
        .route("/retrieval/fs/extract", get(crate::file_data::extract_data))
        .route("/log/paths", get(crate::log::get_log_paths))
        .route("/log/event", post(crate::log::log_event))
        .route("/shortcuts/register", post(crate::app_window::register_shortcut))
        .route("/shortcuts/validate", post(crate::app_window::validate_shortcut))
        .route("/shortcuts/suspend", post(crate::app_window::suspend_shortcuts))
        .route("/shortcuts/resume", post(crate::app_window::resume_shortcuts));

    tauri::async_runtime::spawn(async move {
        install_rustls_crypto_provider();

        let cert = CERTIFICATE.get().unwrap().clone();
        let key = CERTIFICATE_PRIVATE_KEY.get().unwrap().clone();
        let tls_config = RustlsConfig::from_pem(cert, key).await.unwrap();
        let addr = SocketAddr::from(([127, 0, 0, 1], api_port));

        axum_server::bind_rustls(addr, tls_config)
            .serve(app.into_make_service())
            .await
            .unwrap();
    });
}

fn install_rustls_crypto_provider() {
    RUSTLS_CRYPTO_PROVIDER_INIT.call_once(|| {
        let _ = rustls::crypto::aws_lc_rs::default_provider().install_default();
    });
}