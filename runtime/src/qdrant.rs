use std::collections::HashMap;
use std::path::Path;
use std::sync::{Arc, Mutex};
use log::{debug, error, info, warn};
use once_cell::sync::Lazy;
use rocket::get;
use tauri::api::process::{Command, CommandChild, CommandEvent};
use tauri::Url;
use crate::api_token::{APIToken};
use crate::environment::DATA_DIRECTORY;

// Qdrant server process started in a separate process and can communicate
// via HTTP or gRPC with the .NET server and the runtime process
static QDRANT_SERVER: Lazy<Arc<Mutex<Option<CommandChild>>>> = Lazy::new(|| Arc::new(Mutex::new(None)));

// Qdrant server port (default is 6333 for HTTP and 6334 for gRPC)
static QDRANT_SERVER_PORT: Lazy<u16> = Lazy::new(|| {
    crate::network::get_available_port().unwrap_or(6333)
}); 

static QDRANT_INITIALIZED: Lazy<Mutex<bool>> = Lazy::new(|| Mutex::new(false));

#[get("/system/qdrant/port")]
pub fn qdrant_port(_token: APIToken) -> String {
    let qdrant_port = *QDRANT_SERVER_PORT;
    format!("{qdrant_port}")
}

/// Starts the Qdrant server in a separate process.
pub fn start_qdrant_server() {
    
    let base_path = DATA_DIRECTORY.get().unwrap(); 
    
    let storage_path = Path::new(base_path).join("databases").join("qdrant").join("storage").to_str().unwrap().to_string();
    let snapshot_path = Path::new(base_path).join("databases").join("qdrant").join("snapshots").to_str().unwrap().to_string();
    let init_path = Path::new(base_path).join("databases").join("qdrant").join(".qdrant-initalized").to_str().unwrap().to_string();
    
    println!("{}", storage_path);
    println!("{}", snapshot_path);
    println!("{}", init_path);
    
    let qdrant_server_environment = HashMap::from_iter([
        (String::from("QDRANT__SERVICE__HTTP_PORT"), QDRANT_SERVER_PORT.to_string()),
        (String::from("QDRANT_INIT_FILE_PATH"), init_path),
        (String::from("QDRANT__STORAGE__STORAGE_PATH"), storage_path),
        (String::from("QDRANT__STORAGE__SNAPSHOTS_PATH"), snapshot_path),
    ]);
    let server_spawn_clone = QDRANT_SERVER.clone();
    tauri::async_runtime::spawn(async move {
        let (mut rx, child) = Command::new_sidecar("qdrant")
            .expect("Failed to create sidecar for Qdrant")
            .args(["--config-path", "resources/databases/qdrant/config.yaml"])
            .envs(qdrant_server_environment)
            .spawn()
            .expect("Failed to spawn Qdrant server process.");

        let server_pid = child.pid();
        info!(Source = "Bootloader Qdrant"; "Qdrant server process started with PID={server_pid}.");

        // Save the server process to stop it later:
        *server_spawn_clone.lock().unwrap() = Some(child);

        // Log the output of the Qdrant server:
        while let Some(event) = rx.recv().await {
            match event {
                CommandEvent::Stdout(line) => {
                    let line = line.trim_end();
                    if line.contains("INFO") || line.contains("info") {
                        info!(Source = "Qdrant Server"; "{line}");
                    } else if line.contains("WARN") || line.contains("warning") {
                        warn!(Source = "Qdrant Server"; "{line}");
                    } else if line.contains("ERROR") || line.contains("error") {
                        error!(Source = "Qdrant Server"; "{line}");
                    } else {
                        debug!(Source = "Qdrant Server"; "{line}");
                    }
                }
                CommandEvent::Stderr(line) => {
                    error!(Source = "Qdrant Server (stderr)"; "{line}");
                }
                _ => {}
            }
        }
    });
}

/// This endpoint is called by the Qdrant server or frontend to signal that Qdrant is ready.
#[get("/system/qdrant/ready")]
pub async fn qdrant_ready(_token: APIToken) {
    {
        let mut initialized = QDRANT_INITIALIZED.lock().unwrap();
        if *initialized {
            warn!("Qdrant server was already initialized.");
            return;
        }
        info!("Qdrant server is ready.");
        *initialized = true;
    }

    let qdrant_port = *QDRANT_SERVER_PORT;
    let _url = match Url::parse(format!("http://localhost:{qdrant_port}").as_str()) {
        Ok(url) => url,
        Err(msg) => {
            error!("Error while parsing Qdrant URL: {msg}");
            return;
        }
    };

}

/// Stops the Qdrant server process.
pub fn stop_qdrant_server() {
    if let Some(server_process) = QDRANT_SERVER.lock().unwrap().take() {
        let server_kill_result = server_process.kill();
        match server_kill_result {
            Ok(_) => info!("Qdrant server process was stopped."),
            Err(e) => error!("Failed to stop Qdrant server process: {e}."),
        }
    } else {
        warn!("Qdrant server process was not started or is already stopped.");
    }
}