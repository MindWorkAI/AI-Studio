use std::collections::HashMap;
use std::fs::File;
use std::io::Write;
use std::path::Path;
use std::sync::{Arc, Mutex, OnceLock};
use log::{debug, error, info, warn};
use once_cell::sync::Lazy;
use rocket::get;
use rocket::serde::json::Json;
use rocket::serde::Serialize;
use tauri::api::process::{Command, CommandChild, CommandEvent};
use crate::api_token::{APIToken};
use crate::environment::DATA_DIRECTORY;
use crate::certificate_factory::generate_certificate;
use std::path::PathBuf;
use tempfile::{TempDir, Builder};

// Qdrant server process started in a separate process and can communicate
// via HTTP or gRPC with the .NET server and the runtime process
static QDRANT_SERVER: Lazy<Arc<Mutex<Option<CommandChild>>>> = Lazy::new(|| Arc::new(Mutex::new(None)));

// Qdrant server port (default is 6333 for HTTP and 6334 for gRPC)
static QDRANT_SERVER_PORT_HTTP: Lazy<u16> = Lazy::new(|| {
    crate::network::get_available_port().unwrap_or(6333)
});

static QDRANT_SERVER_PORT_GRPC: Lazy<u16> = Lazy::new(|| {
    crate::network::get_available_port().unwrap_or(6334)
});

pub static CERTIFICATE_FINGERPRINT: OnceLock<String> = OnceLock::new();
static API_TOKEN: Lazy<APIToken> = Lazy::new(|| {
    crate::api_token::generate_api_token()
});

static TMPDIR: Lazy<Mutex<Option<TempDir>>> = Lazy::new(|| Mutex::new(None));

#[derive(Serialize)]
pub struct ProvideQdrantInfo {
    path: String,
    port_http: u16,
    port_grpc: u16,
    fingerprint: String,
    api_token: String,
}

#[get("/system/qdrant/info")]
pub fn qdrant_port(_token: APIToken) -> Json<ProvideQdrantInfo> {
    return Json(ProvideQdrantInfo {
        path: Path::new(DATA_DIRECTORY.get().unwrap()).join("databases").join("qdrant").to_str().unwrap().to_string(),
        port_http: *QDRANT_SERVER_PORT_HTTP,
        port_grpc: *QDRANT_SERVER_PORT_GRPC,
        fingerprint: CERTIFICATE_FINGERPRINT.get().expect("Certificate fingerprint not available").to_string(),
        api_token: API_TOKEN.to_hex_text().to_string(),
    });
}

/// Starts the Qdrant server in a separate process.
pub fn start_qdrant_server() {
    
    let base_path = DATA_DIRECTORY.get().unwrap();
    let (cert_path, key_path) =create_temp_tls_files(Path::new(base_path).join("databases").join("qdrant")).unwrap();
    
    let storage_path = Path::new(base_path).join("databases").join("qdrant").join("storage").to_str().unwrap().to_string();
    let snapshot_path = Path::new(base_path).join("databases").join("qdrant").join("snapshots").to_str().unwrap().to_string();
    let init_path = Path::new(base_path).join("databases").join("qdrant").join(".qdrant-initalized").to_str().unwrap().to_string();
    
    let qdrant_server_environment = HashMap::from_iter([
        (String::from("QDRANT__SERVICE__HTTP_PORT"), QDRANT_SERVER_PORT_HTTP.to_string()),
        (String::from("QDRANT__SERVICE__GRPC_PORT"), QDRANT_SERVER_PORT_GRPC.to_string()),
        (String::from("QDRANT_INIT_FILE_PATH"), init_path),
        (String::from("QDRANT__STORAGE__STORAGE_PATH"), storage_path),
        (String::from("QDRANT__STORAGE__SNAPSHOTS_PATH"), snapshot_path),
        (String::from("QDRANT__TLS__CERT"), cert_path.to_str().unwrap().to_string()),
        (String::from("QDRANT__TLS__KEY"), key_path.to_str().unwrap().to_string()),
        (String::from("QDRANT__SERVICE__ENABLE_TLS"), "true".to_string()),
        (String::from("QDRANT__SERVICE__API_KEY"), API_TOKEN.to_hex_text().to_string()),
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
                },
            
                CommandEvent::Stderr(line) => {
                    error!(Source = "Qdrant Server (stderr)"; "{line}");
                },
            
                _ => {}
            }
        }
    });
}

/// Stops the Qdrant server process.
pub fn stop_qdrant_server() {
    drop_tmpdir();
    if let Some(server_process) = QDRANT_SERVER.lock().unwrap().take() {
        let server_kill_result = server_process.kill();
        match server_kill_result {
            Ok(_) => warn!(Source = "Qdrant"; "Qdrant server process was stopped."),
            Err(e) => error!(Source = "Qdrant"; "Failed to stop Qdrant server process: {e}."),
        }
    } else {
        warn!(Source = "Qdrant"; "Qdrant server process was not started or is already stopped.");
    }
}

pub fn create_temp_tls_files(path: PathBuf) -> Result<(PathBuf, PathBuf), Box<dyn std::error::Error>> {
    let (certificate, cert_private_key, cert_fingerprint) = generate_certificate();
    let temp_dir = init_tmpdir_in(path);
    let cert_path = temp_dir.join("cert.pem");
    let key_path = temp_dir.join("key.pem");

    let mut cert_file = File::create(&cert_path)?;
    cert_file.write_all(&*certificate)?;

    let mut key_file = File::create(&key_path)?;
    key_file.write_all(&*cert_private_key)?;
    
    CERTIFICATE_FINGERPRINT.set(cert_fingerprint).expect("Could not set the certificate fingerprint.");

    Ok((cert_path, key_path))
}

pub fn init_tmpdir_in<P: AsRef<Path>>(path: P) -> PathBuf {
    let mut guard = TMPDIR.lock().unwrap();
    let dir = guard.get_or_insert_with(|| {
        Builder::new()
            .prefix("cert-")
            .tempdir_in(path)
            .expect("failed to create tempdir")
    });

    dir.path().to_path_buf()
}

pub fn drop_tmpdir() {
    let mut guard = TMPDIR.lock().unwrap();
    *guard = None;
    warn!(Source = "Qdrant"; "Temporary directory for TLS was dropped.");
}