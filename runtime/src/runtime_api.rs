use log::info;
use once_cell::sync::Lazy;
use rocket::config::Shutdown;
use rocket::figment::Figment;
use rocket::routes;
use crate::certificate::{CERTIFICATE, CERTIFICATE_PRIVATE_KEY};
use crate::environment::is_dev;
use crate::network::get_available_port;

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
    
    // Get the shutdown configuration:
    let shutdown = create_shutdown();

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
                crate::dotnet::dotnet_port,
                crate::dotnet::dotnet_ready,
                crate::clipboard::set_clipboard,
                crate::app_window::check_for_update,
                crate::app_window::install_update,
                crate::app_window::select_directory,
                crate::app_window::select_file,
                crate::secret::get_secret,
                crate::secret::store_secret,
                crate::secret::delete_secret,
                crate::environment::get_data_directory,
                crate::environment::get_config_directory,
                crate::environment::read_user_language,
                crate::environment::read_enterprise_env_config_id,
                crate::environment::delete_enterprise_env_config_id,
                crate::environment::read_enterprise_env_config_server_url,
                crate::file_data::extract_data,
                crate::log::get_log_paths,
            ])
            .ignite().await.unwrap()
            .launch().await.unwrap();
    });
}

fn create_shutdown() -> Shutdown {
    //
    // Create a shutdown configuration, depending on the operating system:
    //
    #[cfg(unix)]
    {
        use std::collections::HashSet;
        let mut shutdown = Shutdown {
            // We do not want to use the Ctrl+C signal to stop the server:
            ctrlc: false,

            // Everything else is set to default for now:
            ..Shutdown::default()
        };

        shutdown.signals = HashSet::new();
        shutdown
    }

    #[cfg(windows)]
    {
        Shutdown {
            // We do not want to use the Ctrl+C signal to stop the server:
            ctrlc: false,

            // Everything else is set to default for now:
            ..Shutdown::default()
        }
    }
}