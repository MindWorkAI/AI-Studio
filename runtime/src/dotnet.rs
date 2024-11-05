use std::collections::HashMap;
use std::sync::{Arc, Mutex};
use base64::Engine;
use base64::prelude::BASE64_STANDARD;
use log::{debug, error, info, warn};
use once_cell::sync::Lazy;
use rocket::get;
use tauri::api::process::{Command, CommandChild, CommandEvent};
use tauri::Url;
use crate::api_token::{APIToken, API_TOKEN};
use crate::app_window::change_location_to;
use crate::certificate::CERTIFICATE_FINGERPRINT;
use crate::encryption::ENCRYPTION;
use crate::environment::is_dev;
use crate::network::get_available_port;

// The .NET server is started in a separate process and communicates with this
// runtime process via IPC. However, we do net start the .NET server in
// the development environment.
static DOTNET_SERVER: Lazy<Arc<Mutex<Option<CommandChild>>>> = Lazy::new(|| Arc::new(Mutex::new(None)));

// The .NET server port is relevant for the production environment only, sine we
// do not start the server in the development environment.
static DOTNET_SERVER_PORT: Lazy<u16> = Lazy::new(|| get_available_port().unwrap());

static DOTNET_INITIALIZED: Lazy<Mutex<bool>> = Lazy::new(|| Mutex::new(false));

#[get("/system/dotnet/port")]
pub fn dotnet_port(_token: APIToken) -> String {
    let dotnet_server_port = *DOTNET_SERVER_PORT;
    format!("{dotnet_server_port}")
}

pub fn start_dotnet_server(api_server_port: u16) {

    // Get the secret password & salt and convert it to a base64 string:
    let secret_password = BASE64_STANDARD.encode(ENCRYPTION.secret_password);
    let secret_key_salt = BASE64_STANDARD.encode(ENCRYPTION.secret_key_salt);

    let dotnet_server_environment = HashMap::from_iter([
        (String::from("AI_STUDIO_SECRET_PASSWORD"), secret_password),
        (String::from("AI_STUDIO_SECRET_KEY_SALT"), secret_key_salt),
        (String::from("AI_STUDIO_CERTIFICATE_FINGERPRINT"), CERTIFICATE_FINGERPRINT.get().unwrap().to_string()),
        (String::from("AI_STUDIO_API_TOKEN"), API_TOKEN.to_hex_text().to_string()),
    ]);

    info!("Try to start the .NET server...");
    let server_spawn_clone = DOTNET_SERVER.clone();
    tauri::async_runtime::spawn(async move {
        let api_port = api_server_port;

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
}

#[get("/system/dotnet/ready")]
pub async fn dotnet_ready(_token: APIToken) {

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
}