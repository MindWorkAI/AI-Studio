use std::collections::HashMap;
use std::path::Path;
use std::sync::{Arc, Mutex};
use base64::Engine;
use base64::prelude::BASE64_STANDARD;
use log::{error, info, warn};
use once_cell::sync::Lazy;
use rocket::get;
use tauri::api::process::{Command, CommandChild, CommandEvent};
use tauri::Url;
use crate::api_token::APIToken;
use crate::runtime_api_token::API_TOKEN;
use crate::app_window::change_location_to;
use crate::runtime_certificate::CERTIFICATE_FINGERPRINT;
use crate::encryption::ENCRYPTION;
use crate::environment::{is_dev, DATA_DIRECTORY};
use crate::network::get_available_port;
use crate::runtime_api::API_SERVER_PORT;
use crate::stale_process_cleanup::{kill_stale_process, log_potential_stale_process};

// The .NET server is started in a separate process and communicates with this
// runtime process via IPC. However, we do net start the .NET server in
// the development environment.
static DOTNET_SERVER: Lazy<Arc<Mutex<Option<CommandChild>>>> = Lazy::new(|| Arc::new(Mutex::new(None)));

// The .NET server port is relevant for the production environment only, sine we
// do not start the server in the development environment.
static DOTNET_SERVER_PORT: Lazy<u16> = Lazy::new(|| get_available_port().unwrap());

static DOTNET_INITIALIZED: Lazy<Mutex<bool>> = Lazy::new(|| Mutex::new(false));

pub const PID_FILE_NAME: &str = "mindwork_ai_studio.pid";

/// Returns the desired port of the .NET server. Our .NET app calls this endpoint to get
/// the port where the .NET server should listen to.
#[get("/system/dotnet/port")]
pub fn dotnet_port(_token: APIToken) -> String {
    let dotnet_server_port = *DOTNET_SERVER_PORT;
    format!("{dotnet_server_port}")
}

/// Creates the startup environment file for the .NET server in the development
/// environment. The file is created in the root directory of the repository.
/// Creating that env file on a production environment would be a security
/// issue, since it contains the secret password and salt in plain text.
/// Anyone could read that file and decrypt the secret communication
/// between the .NET server and the Tauri app.
///
/// Therefore, we not only create the file in the development environment
/// but also remove that code from any production build.
#[cfg(debug_assertions)]
pub fn create_startup_env_file() {

    // Get the secret password & salt and convert it to a base64 string:
    let secret_password = BASE64_STANDARD.encode(ENCRYPTION.secret_password);
    let secret_key_salt = BASE64_STANDARD.encode(ENCRYPTION.secret_key_salt);
    let api_port = *API_SERVER_PORT;

    warn!(Source = "Bootloader .NET"; "Development environment detected; create the startup env file at '../startup.env'.");
    let env_file_path = std::path::PathBuf::from("..").join("startup.env");
    let mut env_file = std::fs::File::create(env_file_path).unwrap();
    let env_file_content = format!(
        "AI_STUDIO_SECRET_PASSWORD={secret_password}\n\
        AI_STUDIO_SECRET_KEY_SALT={secret_key_salt}\n\
        AI_STUDIO_CERTIFICATE_FINGERPRINT={cert_fingerprint}\n\
        AI_STUDIO_API_PORT={api_port}\n\
        AI_STUDIO_API_TOKEN={api_token}",

        cert_fingerprint = CERTIFICATE_FINGERPRINT.get().unwrap(),
        api_token = API_TOKEN.to_hex_text()
    );

    std::io::Write::write_all(&mut env_file, env_file_content.as_bytes()).unwrap();
    info!(Source = "Bootloader .NET"; "The startup env file was created successfully.");
}

/// Starts the .NET server in a separate process.
pub fn start_dotnet_server() {

    // Get the secret password & salt and convert it to a base64 string:
    let secret_password = BASE64_STANDARD.encode(ENCRYPTION.secret_password);
    let secret_key_salt = BASE64_STANDARD.encode(ENCRYPTION.secret_key_salt);
    let api_port = *API_SERVER_PORT;

    let dotnet_server_environment = HashMap::from_iter([
        (String::from("AI_STUDIO_SECRET_PASSWORD"), secret_password),
        (String::from("AI_STUDIO_SECRET_KEY_SALT"), secret_key_salt),
        (String::from("AI_STUDIO_CERTIFICATE_FINGERPRINT"), CERTIFICATE_FINGERPRINT.get().unwrap().to_string()),
        (String::from("AI_STUDIO_API_PORT"), format!("{api_port}")),
        (String::from("AI_STUDIO_API_TOKEN"), API_TOKEN.to_hex_text().to_string()),
    ]);

    info!("Try to start the .NET server...");
    let server_spawn_clone = DOTNET_SERVER.clone();
    tauri::async_runtime::spawn(async move {
        let (mut rx, child) = Command::new_sidecar("mindworkAIStudioServer")
                .expect("Failed to create sidecar")
                .envs(dotnet_server_environment)
                .spawn()
                .expect("Failed to spawn .NET server process.");
        let server_pid = child.pid();
        info!(Source = "Bootloader .NET"; "The .NET server process started with PID={server_pid}.");
        log_potential_stale_process(Path::new(DATA_DIRECTORY.get().unwrap()).join(PID_FILE_NAME), server_pid);

        // Save the server process to stop it later:
        *server_spawn_clone.lock().unwrap() = Some(child);

        // Log the output of the .NET server:
        // NOTE: Log events are sent via structured HTTP API calls.
        // This loop serves for fundamental output (e.g., startup errors).
        while let Some(CommandEvent::Stdout(line)) = rx.recv().await {
            let line = line.trim_end();
            info!(Source = ".NET Server (stdout)"; "{line}");
        }
    });

}

/// This endpoint is called by the .NET server to signal that the server is ready.
#[get("/system/dotnet/ready")]
pub async fn dotnet_ready(_token: APIToken) {

    // We create a manual scope for the lock to be released as soon as possible.
    // This is necessary because we cannot await any function while the lock is
    // held.
    {
        let mut initialized = DOTNET_INITIALIZED.lock().unwrap();
        if !is_dev() && *initialized {
            error!("Anyone tried to initialize the runtime twice. This is not intended.");
            return;
        }

        info!("The .NET server was booted successfully.");
        *initialized = true;
    }

    let dotnet_server_port = *DOTNET_SERVER_PORT;
    let url = match Url::parse(format!("http://localhost:{dotnet_server_port}").as_str())
    {
        Ok(url) => url,
        Err(msg) => {
            error!("Error while parsing URL for navigating to the app: {msg}");
            return;
        }
    };

    change_location_to(url.as_str()).await;
}

/// Stops the .NET server process.
pub fn stop_dotnet_server() {
    if let Some(server_process) = DOTNET_SERVER.lock().unwrap().take() {
        let server_kill_result = server_process.kill();
        match server_kill_result {
            Ok(_) => info!("The .NET server process was stopped."),
            Err(e) => error!("Failed to stop the .NET server process: {e}."),
        }
    } else {
        warn!("The .NET server process was not started or is already stopped.");
    }
    info!("Start dotnet server cleanup");
    cleanup_dotnet_server();
}

/// Remove old Pid files and kill the corresponding processes
pub fn cleanup_dotnet_server() {
    let pid_path = Path::new(DATA_DIRECTORY.get().unwrap()).join(PID_FILE_NAME);
    if let Err(e) = kill_stale_process(pid_path) {
        warn!(Source = ".NET"; "Error during the cleanup of .NET: {}", e);
    }
}