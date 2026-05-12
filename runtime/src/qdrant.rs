use std::collections::HashMap;
use std::{fs};
use std::error::Error;
use std::fs::File;
use std::io::Write;
use std::path::Path;
use std::sync::{Arc, Mutex, OnceLock};
use log::{debug, error, info, warn};
use once_cell::sync::Lazy;
use axum::Json;
use serde::Serialize;
use crate::api_token::{APIToken};
use crate::environment::{is_dev, DATA_DIRECTORY};
use crate::certificate_factory::generate_certificate;
use std::path::PathBuf;
use tauri::Manager;
use tauri::path::BaseDirectory;
use tempfile::{TempDir, Builder};
use crate::stale_process_cleanup::{kill_stale_process, log_potential_stale_process};
use crate::sidecar_types::SidecarType;
use tauri_plugin_shell::process::{CommandChild, CommandEvent};
use tauri_plugin_shell::ShellExt;

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
static QDRANT_STATUS: Lazy<Mutex<QdrantStatus>> = Lazy::new(|| Mutex::new(QdrantStatus::default()));

const PID_FILE_NAME: &str = "qdrant.pid";
const SIDECAR_TYPE:SidecarType = SidecarType::Qdrant;

#[derive(Default)]
struct QdrantStatus {
    is_available: bool,
    unavailable_reason: Option<String>,
}

fn qdrant_base_path() -> PathBuf {
    let qdrant_directory = if is_dev() { "qdrant_test" } else { "qdrant" };
    Path::new(DATA_DIRECTORY.get().unwrap())
        .join("databases")
        .join(qdrant_directory)
}

#[derive(Serialize)]
pub struct ProvideQdrantInfo {
    path: String,
    port_http: u16,
    port_grpc: u16,
    fingerprint: String,
    api_token: String,
    is_available: bool,
    unavailable_reason: Option<String>,
}

pub async fn qdrant_port(_token: APIToken) -> Json<ProvideQdrantInfo> {
    let status = QDRANT_STATUS.lock().unwrap();
    let is_available = status.is_available;
    let unavailable_reason = status.unavailable_reason.clone();

    Json(ProvideQdrantInfo {
        path: if is_available {
            qdrant_base_path().to_string_lossy().to_string()
        } else {
            String::new()
        },
        port_http: if is_available { *QDRANT_SERVER_PORT_HTTP } else { 0 },
        port_grpc: if is_available { *QDRANT_SERVER_PORT_GRPC } else { 0 },
        fingerprint: if is_available {
            CERTIFICATE_FINGERPRINT.get().cloned().unwrap_or_default()
        } else {
            String::new()
        },
        api_token: if is_available {
            API_TOKEN.to_hex_text().to_string()
        } else {
            String::new()
        },
        is_available,
        unavailable_reason,
    })
}

/// Starts the Qdrant server in a separate process.
pub fn start_qdrant_server<R: tauri::Runtime>(app_handle: tauri::AppHandle<R>){
    let path = qdrant_base_path();
    if !path.exists() {
        if let Err(e) = fs::create_dir_all(&path){
            error!(Source="Qdrant"; "The required directory to host the Qdrant database could not be created: {}", e);
            set_qdrant_unavailable(format!("The Qdrant data directory could not be created: {e}"));
            return;
        };
    }

    let (cert_path, key_path) = match create_temp_tls_files(&path) {
        Ok(paths) => paths,
        Err(e) => {
            error!(Source="Qdrant"; "TLS files for Qdrant could not be created: {e}");
            set_qdrant_unavailable(format!("TLS files for Qdrant could not be created: {e}"));
            return;
        }
    };

    let storage_path = path.join("storage").to_string_lossy().to_string();
    let snapshot_path = path.join("snapshots").to_string_lossy().to_string();
    let init_path = path.join(".qdrant-initialized").to_string_lossy().to_string();
    
    let qdrant_server_environment: HashMap<String, String> = HashMap::from_iter([
        (String::from("QDRANT__SERVICE__HTTP_PORT"), QDRANT_SERVER_PORT_HTTP.to_string()),
        (String::from("QDRANT__SERVICE__GRPC_PORT"), QDRANT_SERVER_PORT_GRPC.to_string()),
        (String::from("QDRANT_INIT_FILE_PATH"), init_path),
        (String::from("QDRANT__STORAGE__STORAGE_PATH"), storage_path),
        (String::from("QDRANT__STORAGE__SNAPSHOTS_PATH"), snapshot_path),
        (String::from("QDRANT__TLS__CERT"), cert_path.to_string_lossy().to_string()),
        (String::from("QDRANT__TLS__KEY"), key_path.to_string_lossy().to_string()),
        (String::from("QDRANT__SERVICE__ENABLE_TLS"), "true".to_string()),
        (String::from("QDRANT__SERVICE__API_KEY"), API_TOKEN.to_hex_text().to_string()),
    ]);
    
    let server_spawn_clone = QDRANT_SERVER.clone();
    let qdrant_relative_source_path = "resources/databases/qdrant/config.yaml";
    let qdrant_source_path = match app_handle.path().resolve(qdrant_relative_source_path, BaseDirectory::Resource) {
        Ok(path) => path,
        Err(_) => {
            let reason = format!("The Qdrant config resource '{qdrant_relative_source_path}' could not be resolved.");
            error!(Source = "Qdrant"; "{reason} Starting the app without Qdrant.");
            set_qdrant_unavailable(reason);
            return;
        }
    };

    let qdrant_source_path_display = qdrant_source_path.to_string_lossy().to_string();
    tauri::async_runtime::spawn(async move {
        let shell = app_handle.shell();

        let sidecar = match shell.sidecar("qdrant") {
            Ok(sidecar) => sidecar,
            Err(e) => {
                let reason = format!("Failed to create sidecar for Qdrant: {e}");
                error!(Source = "Qdrant"; "{reason}");
                set_qdrant_unavailable(reason);
                return;
            }
        };

        let (mut rx, child) = match sidecar
            .args(["--config-path", qdrant_source_path_display.as_str()])
            .envs(qdrant_server_environment)
            .spawn()
        {
            Ok(process) => process,
            Err(e) => {
                let reason = format!("Failed to spawn Qdrant server process with config path '{}': {e}", qdrant_source_path_display);
                error!(Source = "Qdrant"; "{reason}");
                set_qdrant_unavailable(reason);
                return;
            }
        };

        let server_pid = child.pid();
        set_qdrant_available();
        info!(Source = "Bootloader Qdrant"; "Qdrant server process started with PID={server_pid}.");
        log_potential_stale_process(path.join(PID_FILE_NAME), server_pid, SIDECAR_TYPE);

        // Save the server process to stop it later:
        *server_spawn_clone.lock().unwrap() = Some(child);

        // Log the output of the Qdrant server:
        while let Some(event) = rx.recv().await {
            match event {
                CommandEvent::Stdout(line) => {
                    let line_utf8 = String::from_utf8_lossy(&line).to_string();
                    let line = line_utf8.trim_end();
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
                    let line_utf8 = String::from_utf8_lossy(&line).to_string();
                    error!(Source = "Qdrant Server (stderr)"; "{line_utf8}");
                },
            
                _ => {}
            }
        }
    });
}

/// Stops the Qdrant server process.
pub fn stop_qdrant_server() {
    if let Some(server_process) = QDRANT_SERVER.lock().unwrap().take() {
        let server_kill_result = server_process.kill();
        match server_kill_result {
            Ok(_) => {
                set_qdrant_unavailable("Qdrant server was stopped.".to_string());
                warn!(Source = "Qdrant"; "Qdrant server process was stopped.")
            },
            Err(e) => error!(Source = "Qdrant"; "Failed to stop Qdrant server process: {e}."),
        }
    } else {
        warn!(Source = "Qdrant"; "Qdrant server process was not started or is already stopped.");
    }

    drop_tmpdir();
    cleanup_qdrant();
}

/// Create a temporary directory with TLS relevant files
pub fn create_temp_tls_files(path: &PathBuf) -> Result<(PathBuf, PathBuf), Box<dyn Error>> {
    let cert = generate_certificate();
    
    let temp_dir = init_tmpdir_in(path);
    let cert_path = temp_dir.join("cert.pem");
    let key_path = temp_dir.join("key.pem");

    let mut cert_file = File::create(&cert_path)?;
    cert_file.write_all(&cert.certificate)?;

    let mut key_file = File::create(&key_path)?;
    key_file.write_all(&cert.private_key)?;
    
    CERTIFICATE_FINGERPRINT.set(cert.fingerprint).expect("Could not set the certificate fingerprint.");

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

/// Remove old Pid files and kill the corresponding processes
pub fn cleanup_qdrant() {
    let path = qdrant_base_path();
    let pid_path = path.join(PID_FILE_NAME);
    if let Err(e) = kill_stale_process(pid_path, SIDECAR_TYPE) {
        warn!(Source = "Qdrant"; "Error during the cleanup of Qdrant: {}", e);
    }
    if let Err(e) = delete_old_certificates(path) {
        warn!(Source = "Qdrant"; "Error during the cleanup of Qdrant: {}", e);
    }
    
}

fn set_qdrant_available() {
    let mut status = QDRANT_STATUS.lock().unwrap();
    status.is_available = true;
    status.unavailable_reason = None;
}

fn set_qdrant_unavailable(reason: String) {
    let mut status = QDRANT_STATUS.lock().unwrap();
    status.is_available = false;
    status.unavailable_reason = Some(reason);
}

pub fn delete_old_certificates(path: PathBuf) -> Result<(), Box<dyn Error>> {
    if !path.exists() {
        return Ok(());
    }

    for entry in fs::read_dir(path)? {
        let entry = entry?;
        let path = entry.path();

        if path.is_dir() {
            let file_name = entry.file_name();
            let folder_name = file_name.to_string_lossy();

            if folder_name.starts_with("cert-") {
                fs::remove_dir_all(&path)?;
                warn!(Source="Qdrant"; "Removed old certificates in: {}", path.display());
            }
        }
    }
    Ok(())
}