// Prevents an additional console window on Windows in release, DO NOT REMOVE!!
#![cfg_attr(not(debug_assertions), windows_subsystem = "windows")]

extern crate rocket;
extern crate core;

use std::collections::{BTreeMap, HashMap, HashSet};
use std::fmt;
use std::net::TcpListener;
use std::sync::{Arc, Mutex};
use std::time::{Duration, Instant};
use once_cell::sync::Lazy;

use arboard::Clipboard;
use base64::Engine;
use base64::prelude::BASE64_STANDARD;
use aes::cipher::{block_padding::Pkcs7, BlockDecryptMut, BlockEncryptMut, KeyIvInit};
use keyring::Entry;
use serde::{Deserialize, Serialize};
use tauri::{Manager, Url, Window};
use tauri::api::process::{Command, CommandChild, CommandEvent};
use tokio::time;
use flexi_logger::{DeferredNow, Duplicate, FileSpec, Logger};
use flexi_logger::writers::FileLogWriter;
use hmac::Hmac;
use keyring::error::Error::NoEntry;
use log::{debug, error, info, kv, warn};
use log::kv::{Key, Value, VisitSource};
use pbkdf2::pbkdf2;
use rand::{RngCore, SeedableRng};
use rocket::figment::Figment;
use rocket::{data, get, post, routes, Data, Request};
use rocket::config::Shutdown;
use rocket::data::{Outcome, ToByteUnit};
use rocket::http::Status;
use rocket::serde::json::Json;
use sha2::Sha512;
use tauri::updater::UpdateResponse;
use tokio::io::AsyncReadExt;

type Aes256CbcEnc = cbc::Encryptor<aes::Aes256>;
type Aes256CbcDec = cbc::Decryptor<aes::Aes256>;

// The .NET server is started in a separate process and communicates with this
// runtime process via IPC. However, we do net start the .NET server in
// the development environment.
static DOTNET_SERVER: Lazy<Arc<Mutex<Option<CommandChild>>>> = Lazy::new(|| Arc::new(Mutex::new(None)));

// The .NET server port is relevant for the production environment only, sine we
// do not start the server in the development environment.
static DOTNET_SERVER_PORT: Lazy<u16> = Lazy::new(|| get_available_port().unwrap());

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

// The Tauri main window.
static MAIN_WINDOW: Lazy<Mutex<Option<Window>>> = Lazy::new(|| Mutex::new(None));

// The update response coming from the Tauri updater.
static CHECK_UPDATE_RESPONSE: Lazy<Mutex<Option<UpdateResponse<tauri::Wry>>>> = Lazy::new(|| Mutex::new(None));

static ENCRYPTION: Lazy<Encryption> = Lazy::new(|| {
    //
    // Generate a secret key & salt for the AES encryption for the IPC channel:
    //
    let mut secret_key = [0u8; 512]; // 512 bytes = 4096 bits
    let mut secret_key_salt = [0u8; 16]; // 16 bytes = 128 bits

    // We use a cryptographically secure pseudo-random number generator
    // to generate the secret password & salt. ChaCha20Rng is the algorithm
    // of our choice:
    let mut rng = rand_chacha::ChaChaRng::from_entropy();

    // Fill the secret key & salt with random bytes:
    rng.fill_bytes(&mut secret_key);
    rng.fill_bytes(&mut secret_key_salt);

    Encryption::new(&secret_key, &secret_key_salt).unwrap()
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

    //
    // Configure the logger:
    //
    let mut log_config = String::new();

    // Set the log level depending on the environment:
    match is_dev() {
        true => log_config.push_str("debug, "),
        false => log_config.push_str("info, "),
    };

    // Set the log level for the Rocket library:
    log_config.push_str("rocket=info, ");

    // Set the log level for the Rocket server:
    log_config.push_str("rocket::server=warn, ");

    // Set the log level for the Reqwest library:
    log_config.push_str("reqwest::async_impl::client=info");

    let logger = Logger::try_with_str(log_config).expect("Cannot create logging")
        .log_to_file(FileSpec::default()
            .basename("AI Studio Events")
            .suppress_timestamp()
            .suffix("log"))
        .duplicate_to_stdout(Duplicate::All)
        .use_utc()
        .format_for_files(file_logger_format)
        .format_for_stderr(terminal_colored_logger_format)
        .format_for_stdout(terminal_colored_logger_format)
        .start().expect("Cannot start logging");

    info!("Starting MindWork AI Studio:");
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

    let api_port = *API_SERVER_PORT;
    info!("Try to start the API server on 'http://localhost:{api_port}'...");

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

        // Set the shutdown configuration:
        .merge(("shutdown", Shutdown {

            // Again, we do not want to use the Ctrl+C signal to stop the server:
            ctrlc: false,

            // We do not want to use the termination signal to stop the server:
            signals: HashSet::new(),

            // Everything else is set to default:
            ..Shutdown::default()
        }));

    //
    // Start the runtime API server in a separate thread. This is necessary
    // because the server is blocking, and we need to run the Tauri app in
    // parallel:
    //
    tauri::async_runtime::spawn(async move {
        _ = rocket::custom(figment)
            .mount("/", routes![dotnet_port, dotnet_ready, set_clipboard, check_for_update, install_update, get_secret, store_secret])
            .ignite().await.unwrap()
            .launch().await.unwrap();
    });

    // Get the secret password & salt and convert it to a base64 string:
    let secret_password = BASE64_STANDARD.encode(ENCRYPTION.secret_password);
    let secret_key_salt = BASE64_STANDARD.encode(ENCRYPTION.secret_key_salt);

    info!("Secret password for the IPC channel was generated successfully.");
    info!("Try to start the .NET server...");
    let server_spawn_clone = DOTNET_SERVER.clone();
    tauri::async_runtime::spawn(async move {
        let api_port = *API_SERVER_PORT;

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

                    // Provide the secret password & salt for the IPC channel to the .NET server by using
                    // an environment variable. We must use a HashMap for this:
                    .envs(HashMap::from_iter([
                        (String::from("AI_STUDIO_SECRET_PASSWORD"), secret_password),
                        (String::from("AI_STUDIO_SECRET_KEY_SALT"), secret_key_salt),
                    ]))
                    .spawn()
                    .expect("Failed to spawn .NET server process.")
            }

            false => {
                Command::new_sidecar("mindworkAIStudioServer")
                    .expect("Failed to create sidecar")

                    // Provide the runtime API server port to the .NET server:
                    .args([format!("{api_port}").as_str()])

                    // Provide the secret password & salt for the IPC channel to the .NET server by using
                    // an environment variable. We must use a HashMap for this:
                    .envs(HashMap::from_iter([
                        (String::from("AI_STUDIO_SECRET_PASSWORD"), secret_password),
                        (String::from("AI_STUDIO_SECRET_KEY_SALT"), secret_key_salt),
                    ]))
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

    info!("Starting Tauri app...");
    let app = tauri::Builder::default()
        .setup(move |app| {
            let window = app.get_window("main").expect("Failed to get main window.");
            *MAIN_WINDOW.lock().unwrap() = Some(window);

            info!(Source = "Bootloader Tauri"; "Setup is running.");
            let logger_path = app.path_resolver().app_local_data_dir().unwrap();
            let logger_path = logger_path.join("data");

            info!(Source = "Bootloader Tauri"; "Reconfigure the file logger to use the app data directory {logger_path:?}");
            logger.reset_flw(&FileLogWriter::builder(
                FileSpec::default()
                .directory(logger_path)
                .basename("events")
                .suppress_timestamp()
                .suffix("log")))?;

            Ok(())
        })
        .plugin(tauri_plugin_window_state::Builder::default().build())
        .invoke_handler(tauri::generate_handler![
            delete_secret
        ])
        .build(tauri::generate_context!())
        .expect("Error while running Tauri application");
    
    app.run(|app_handle, event| match event {

        tauri::RunEvent::WindowEvent { event, label, .. } => {
            match event {
                tauri::WindowEvent::CloseRequested { .. } => {
                    warn!(Source = "Tauri"; "Window '{label}': close was requested.");
                }

                tauri::WindowEvent::Destroyed => {
                    warn!(Source = "Tauri"; "Window '{label}': was destroyed.");
                }

                tauri::WindowEvent::FileDrop(files) => {
                    info!(Source = "Tauri"; "Window '{label}': files were dropped: {files:?}");
                }

                _ => (),
            }
        }

        tauri::RunEvent::Updater(updater_event) => {
            match updater_event {

                tauri::UpdaterEvent::UpdateAvailable { body, date, version } => {
                    let body_len = body.len();
                    info!(Source = "Tauri"; "Updater: update available: body size={body_len} time={date:?} version={version}");
                }

                tauri::UpdaterEvent::Pending => {
                    info!(Source = "Tauri"; "Updater: update is pending!");
                }

                tauri::UpdaterEvent::DownloadProgress { chunk_length, content_length } => {
                    info!(Source = "Tauri"; "Updater: downloaded {} of {:?}", chunk_length, content_length);
                }

                tauri::UpdaterEvent::Downloaded => {
                    info!(Source = "Tauri"; "Updater: update has been downloaded!");
                    warn!(Source = "Tauri"; "Try to stop the .NET server now...");
                    stop_servers();
                }

                tauri::UpdaterEvent::Updated => {
                    info!(Source = "Tauri"; "Updater: app has been updated");
                    warn!(Source = "Tauri"; "Try to restart the app now...");
                    app_handle.restart();
                }

                tauri::UpdaterEvent::AlreadyUpToDate => {
                    info!(Source = "Tauri"; "Updater: app is already up to date");
                }

                tauri::UpdaterEvent::Error(error) => {
                    warn!(Source = "Tauri"; "Updater: failed to update: {error}");
                }
            }
        }

        tauri::RunEvent::ExitRequested { .. } => {
            warn!(Source = "Tauri"; "Run event: exit was requested.");
        }
        
        tauri::RunEvent::Ready => {
            info!(Source = "Tauri"; "Run event: Tauri app is ready.");
        }

        _ => {}
    });

    warn!(Source = "Tauri"; "Tauri app was stopped.");
    if is_prod() {
        warn!("Try to stop the .NET server as well...");
        stop_servers();
    }
}

//
// Data structure for iterating over key-value pairs of log messages.
//
struct LogKVCollect<'kvs>(BTreeMap<Key<'kvs>, Value<'kvs>>);

impl<'kvs> VisitSource<'kvs> for LogKVCollect<'kvs> {
    fn visit_pair(&mut self, key: Key<'kvs>, value: Value<'kvs>) -> Result<(), kv::Error> {
        self.0.insert(key, value);
        Ok(())
    }
}

pub fn write_kv_pairs(w: &mut dyn std::io::Write, record: &log::Record) -> Result<(), std::io::Error> {
    if record.key_values().count() > 0 {
        let mut visitor = LogKVCollect(BTreeMap::new());
        record.key_values().visit(&mut visitor).unwrap();
        write!(w, "[")?;
        let mut index = 0;
        for (key, value) in visitor.0 {
            index += 1;
            if index > 1 {
                write!(w, ", ")?;
            }

            write!(w, "{} = {}", key, value)?;
        }
        write!(w, "] ")?;
    }

    Ok(())
}

// Custom logger format for the terminal:
pub fn terminal_colored_logger_format(
    w: &mut dyn std::io::Write,
    now: &mut DeferredNow,
    record: &log::Record,
) -> Result<(), std::io::Error> {
    let level = record.level();

    // Write the timestamp, log level, and module path:
    write!(
        w,
        "[{}] {} [{}] ",
        flexi_logger::style(level).paint(now.format(flexi_logger::TS_DASHES_BLANK_COLONS_DOT_BLANK).to_string()),
        flexi_logger::style(level).paint(record.level().to_string()),
        record.module_path().unwrap_or("<unnamed>"),
    )?;

    // Write all key-value pairs:
    write_kv_pairs(w, record)?;

    // Write the log message:
    write!(w, "{}", flexi_logger::style(level).paint(record.args().to_string()))
}

// Custom logger format for the log files:
pub fn file_logger_format(
    w: &mut dyn std::io::Write,
    now: &mut DeferredNow,
    record: &log::Record,
) -> Result<(), std::io::Error> {

    // Write the timestamp, log level, and module path:
    write!(
        w,
        "[{}] {} [{}] ",
        now.format(flexi_logger::TS_DASHES_BLANK_COLONS_DOT_BLANK),
        record.level(),
        record.module_path().unwrap_or("<unnamed>"),
    )?;

    // Write all key-value pairs:
    write_kv_pairs(w, record)?;

    // Write the log message:
    write!(w, "{}", &record.args())
}

pub struct Encryption {
    key: [u8; 32],
    iv: [u8; 16],

    secret_password: [u8; 512],
    secret_key_salt: [u8; 16],
}

impl Encryption {
    // The number of iterations to derive the key and IV from the password. For a password
    // manager where the user has to enter their primary password, 100 iterations would be
    // too few and insecure. Here, the use case is different: We generate a 512-byte long
    // and cryptographically secure password at every start. This password already contains
    // enough entropy. In our case, we need key and IV primarily because AES, with the
    // algorithms we chose, requires a fixed key length, and our password is too long.
    const ITERATIONS: u32 = 100;

    pub fn new(secret_password: &[u8], secret_key_salt: &[u8]) -> Result<Self, String> {
        if secret_password.len() != 512 {
            return Err("The secret password must be 512 bytes long.".to_string());
        }

        if secret_key_salt.len() != 16 {
            return Err("The salt must be 16 bytes long.".to_string());
        }

        info!(Source = "Encryption"; "Initializing encryption...");
        let mut encryption = Encryption {
            key: [0u8; 32],
            iv: [0u8; 16],

            secret_password: [0u8; 512],
            secret_key_salt: [0u8; 16],
        };

        encryption.secret_password.copy_from_slice(secret_password);
        encryption.secret_key_salt.copy_from_slice(secret_key_salt);

        let start = Instant::now();
        let mut key_iv = [0u8; 48];
        pbkdf2::<Hmac<Sha512>>(secret_password, secret_key_salt, Self::ITERATIONS, &mut key_iv).map_err(|e| format!("Error while generating key and IV: {e}"))?;
        encryption.key.copy_from_slice(&key_iv[0..32]);
        encryption.iv.copy_from_slice(&key_iv[32..48]);

        let duration = start.elapsed();
        let duration = duration.as_millis();
        info!(Source = "Encryption"; "Encryption initialized in {duration} milliseconds.", );

        Ok(encryption)
    }

    pub fn encrypt(&self, data: &str) -> Result<EncryptedText, String> {
        let cipher = Aes256CbcEnc::new(&self.key.into(), &self.iv.into());
        let encrypted = cipher.encrypt_padded_vec_mut::<Pkcs7>(data.as_bytes());
        let mut result = BASE64_STANDARD.encode(self.secret_key_salt);
        result.push_str(&BASE64_STANDARD.encode(&encrypted));
        Ok(EncryptedText::new(result))
    }

    pub fn decrypt(&self, encrypted_data: &EncryptedText) -> Result<String, String> {
        let decoded = BASE64_STANDARD.decode(encrypted_data.get_encrypted()).map_err(|e| format!("Error decoding base64: {e}"))?;

        if decoded.len() < 16 {
            return Err("Encrypted data is too short.".to_string());
        }

        let (salt, encrypted) = decoded.split_at(16);
        if salt != self.secret_key_salt {
            return Err("The salt bytes do not match. The data is corrupted or tampered.".to_string());
        }

        let cipher = Aes256CbcDec::new(&self.key.into(), &self.iv.into());
        let decrypted = cipher.decrypt_padded_vec_mut::<Pkcs7>(encrypted).map_err(|e| format!("Error decrypting data: {e}"))?;

        String::from_utf8(decrypted).map_err(|e| format!("Error converting decrypted data to string: {}", e))
    }
}

#[derive(Clone, Serialize, Deserialize)]
pub struct EncryptedText(String);

impl EncryptedText {
    pub fn new(encrypted_data: String) -> Self {
        EncryptedText(encrypted_data)
    }

    pub fn get_encrypted(&self) -> &str {
        &self.0
    }
}

impl fmt::Debug for EncryptedText {
    fn fmt(&self, f: &mut fmt::Formatter<'_>) -> fmt::Result {
        write!(f, "EncryptedText(**********)")
    }
}

impl fmt::Display for EncryptedText {
    fn fmt(&self, f: &mut fmt::Formatter<'_>) -> fmt::Result {
        write!(f, "**********")
    }
}

// Use Case: When we receive encrypted text from the client as body (e.g., in a POST request).
// We must interpret the body as EncryptedText.
#[rocket::async_trait]
impl<'r> data::FromData<'r> for EncryptedText {
    type Error = String;
    async fn from_data(req: &'r Request<'_>, data: Data<'r>) -> Outcome<'r, Self> {
        let content_type = req.content_type();
        if content_type.map_or(true, |ct| !ct.is_text()) {
            return Outcome::Forward((data, Status::Ok));
        }

        let mut stream = data.open(2.mebibytes());
        let mut body = String::new();
        if let Err(e) = stream.read_to_string(&mut body).await {
            return Outcome::Error((Status::InternalServerError, format!("Failed to read data: {}", e)));
        }

        Outcome::Success(EncryptedText(body))
    }
}

#[get("/system/dotnet/port")]
fn dotnet_port() -> String {
    let dotnet_server_port = *DOTNET_SERVER_PORT;
    format!("{dotnet_server_port}")
}

#[post("/system/dotnet/ready")]
async fn dotnet_ready() {
    let main_window_spawn_clone = &MAIN_WINDOW;
    let dotnet_server_port = *DOTNET_SERVER_PORT;
    let url = match Url::parse(format!("http://localhost:{dotnet_server_port}").as_str())
    {
        Ok(url) => url,
        Err(msg) => {
            error!("Error while parsing URL for navigating to the app: {msg}");
            return;
        }
    };

    info!("The .NET server was booted successfully.");

    // Try to get the main window. If it is not available yet, wait for it:
    let mut main_window_ready = false;
    let mut main_window_status_reported = false;
    while !main_window_ready
    {
        main_window_ready = {
            let main_window = main_window_spawn_clone.lock().unwrap();
            main_window.is_some()
        };

        if !main_window_ready {
            if !main_window_status_reported {
                info!("Waiting for main window to be ready, because .NET was faster than Tauri.");
                main_window_status_reported = true;
            }

            time::sleep(Duration::from_millis(100)).await;
        }
    }

    let main_window = main_window_spawn_clone.lock().unwrap();
    let js_location_change = format!("window.location = '{url}';");
    let location_change_result = main_window.as_ref().unwrap().eval(js_location_change.as_str());
    match location_change_result {
        Ok(_) => info!("The app location was changed to {url}."),
        Err(e) => error!("Failed to change the app location to {url}: {e}."),
    }
}

pub fn is_dev() -> bool {
    cfg!(debug_assertions)
}

pub fn is_prod() -> bool {
    !is_dev()
}

fn get_available_port() -> Option<u16> {
    TcpListener::bind(("127.0.0.1", 0))
        .map(|listener| listener.local_addr().unwrap().port())
        .ok()
}

fn stop_servers() {
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

#[get("/updates/check")]
async fn check_for_update() -> Json<CheckUpdateResponse> {
    let app_handle = MAIN_WINDOW.lock().unwrap().as_ref().unwrap().app_handle();
    let response = app_handle.updater().check().await;
    match response {
        Ok(update_response) => match update_response.is_update_available() {
            true => {
                *CHECK_UPDATE_RESPONSE.lock().unwrap() = Some(update_response.clone());
                let new_version = update_response.latest_version();
                info!(Source = "Updater"; "An update to version '{new_version}' is available.");
                let changelog = update_response.body();
                Json(CheckUpdateResponse {
                    update_is_available: true,
                    error: false,
                    new_version: new_version.to_string(),
                    changelog: match changelog {
                        Some(c) => c.to_string(),
                        None => String::from(""),
                    },
                })
            },

            false => {
                info!(Source = "Updater"; "No updates are available.");
                Json(CheckUpdateResponse {
                    update_is_available: false,
                    error: false,
                    new_version: String::from(""),
                    changelog: String::from(""),
                })
            },
        },

        Err(e) => {
            warn!(Source = "Updater"; "Failed to check for updates: {e}.");
            Json(CheckUpdateResponse {
                update_is_available: false,
                error: true,
                new_version: String::from(""),
                changelog: String::from(""),
            })
        },
    }
}

#[derive(Serialize)]
struct CheckUpdateResponse {
    update_is_available: bool,
    error: bool,
    new_version: String,
    changelog: String,
}

#[get("/updates/install")]
async fn install_update() {
    let cloned_response_option = CHECK_UPDATE_RESPONSE.lock().unwrap().clone();
    match cloned_response_option {
        Some(update_response) => {
            update_response.download_and_install().await.unwrap();
        },

        None => {
            error!(Source = "Updater"; "No update available to install. Did you check for updates first?");
        },
    }
}

#[post("/secrets/store", data = "<request>")]
fn store_secret(request: Json<StoreSecret>) -> Json<StoreSecretResponse> {
    let user_name = request.user_name.as_str();
    let decrypted_text = match ENCRYPTION.decrypt(&request.secret) {
        Ok(text) => text,
        Err(e) => {
            error!(Source = "Secret Store"; "Failed to decrypt the text: {e}.");
            return Json(StoreSecretResponse {
                success: false,
                issue: format!("Failed to decrypt the text: {e}"),
            })
        },
    };
    
    let service = format!("mindwork-ai-studio::{}", request.destination);
    let entry = Entry::new(service.as_str(), user_name).unwrap();
    let result = entry.set_password(decrypted_text.as_str());
    match result {
        Ok(_) => {
            info!(Source = "Secret Store"; "Secret for {service} and user {user_name} was stored successfully.");
            Json(StoreSecretResponse {
                success: true,
                issue: String::from(""),
            })
        },
        
        Err(e) => {
            error!(Source = "Secret Store"; "Failed to store secret for {service} and user {user_name}: {e}.");
            Json(StoreSecretResponse {
                success: false,
                issue: e.to_string(),
            })
        },
    }
}

#[derive(Deserialize)]
struct StoreSecret {
    destination: String,
    user_name: String,
    secret: EncryptedText,
}

#[derive(Serialize)]
struct StoreSecretResponse {
    success: bool,
    issue: String,
}

#[post("/secrets/get", data = "<request>")]
fn get_secret(request: Json<RequestSecret>) -> Json<RequestedSecret> {
    let user_name = request.user_name.as_str();
    let service = format!("mindwork-ai-studio::{}", request.destination);
    let entry = Entry::new(service.as_str(), user_name).unwrap();
    let secret = entry.get_password();
    match secret {
        Ok(s) => {
            info!(Source = "Secret Store"; "Secret for '{service}' and user '{user_name}' was retrieved successfully.");

            // Encrypt the secret:
            let encrypted_secret = match ENCRYPTION.encrypt(s.as_str()) {
                Ok(e) => e,
                Err(e) => {
                    error!(Source = "Secret Store"; "Failed to encrypt the secret: {e}.");
                    return Json(RequestedSecret {
                        success: false,
                        secret: EncryptedText::new(String::from("")),
                        issue: format!("Failed to encrypt the secret: {e}"),
                    });
                },
            };

            Json(RequestedSecret {
                success: true,
                secret: encrypted_secret,
                issue: String::from(""),
            })
        },

        Err(e) => {
            error!(Source = "Secret Store"; "Failed to retrieve secret for '{service}' and user '{user_name}': {e}.");
            Json(RequestedSecret {
                success: false,
                secret: EncryptedText::new(String::from("")),
                issue: format!("Failed to retrieve secret for '{service}' and user '{user_name}': {e}"),
            })
        },
    }
}

#[derive(Deserialize)]
struct RequestSecret {
    destination: String,
    user_name: String,
}

#[derive(Serialize)]
struct RequestedSecret {
    success: bool,
    secret: EncryptedText,
    issue: String,
}

#[tauri::command]
fn delete_secret(destination: String, user_name: String) -> DeleteSecretResponse {
    let service = format!("mindwork-ai-studio::{}", destination);
    let entry = Entry::new(service.as_str(), user_name.as_str()).unwrap();
    let result = entry.delete_credential();

    match result {
        Ok(_) => {
            warn!(Source = "Secret Store"; "Secret for {service} and user {user_name} was deleted successfully.");
            DeleteSecretResponse {
                success: true,
                was_entry_found: true,
                issue: String::from(""),
            }
        },

        Err(NoEntry) => {
            warn!(Source = "Secret Store"; "No secret for {service} and user {user_name} was found.");
            DeleteSecretResponse {
                success: true,
                was_entry_found: false,
                issue: String::from(""),
            }
        }
        
        Err(e) => {
            error!(Source = "Secret Store"; "Failed to delete secret for {service} and user {user_name}: {e}.");
            DeleteSecretResponse {
                success: false,
                was_entry_found: false,
                issue: e.to_string(),
            }
        },
    }
}

#[derive(Serialize)]
struct DeleteSecretResponse {
    success: bool,
    was_entry_found: bool,
    issue: String,
}

#[post("/clipboard/set", data = "<encrypted_text>")]
fn set_clipboard(encrypted_text: EncryptedText) -> Json<SetClipboardResponse> {

    // Decrypt this text first:
    let decrypted_text = match ENCRYPTION.decrypt(&encrypted_text) {
        Ok(text) => text,
        Err(e) => {
            error!(Source = "Clipboard"; "Failed to decrypt the text: {e}.");
            return Json(SetClipboardResponse {
                success: false,
                issue: e,
            })
        },
    };

    let clipboard_result = Clipboard::new();
    let mut clipboard = match clipboard_result {
        Ok(clipboard) => clipboard,
        Err(e) => {
            error!(Source = "Clipboard"; "Failed to get the clipboard instance: {e}.");
            return Json(SetClipboardResponse {
                success: false,
                issue: e.to_string(),
            })
        },
    };
    
    let set_text_result = clipboard.set_text(decrypted_text);
    match set_text_result {
        Ok(_) => {
            debug!(Source = "Clipboard"; "Text was set to the clipboard successfully.");
            Json(SetClipboardResponse {
                success: true,
                issue: String::from(""),
            })
        },
        
        Err(e) => {
            error!(Source = "Clipboard"; "Failed to set text to the clipboard: {e}.");
            Json(SetClipboardResponse {
                success: false,
                issue: e.to_string(),
            })
        },
    }
}

#[derive(Serialize)]
struct SetClipboardResponse {
    success: bool,
    issue: String,
}