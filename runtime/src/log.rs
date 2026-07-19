use std::collections::BTreeMap;
use std::env::{current_dir, temp_dir};
use std::error::Error;
use std::fmt::Debug;
use std::fs::{create_dir_all, OpenOptions};
use std::path::{absolute, Path, PathBuf};
use std::sync::OnceLock;
use flexi_logger::{DeferredNow, Duplicate, FileSpec, Logger, LoggerHandle};
use flexi_logger::writers::FileLogWriter;
use log::{kv, Level};
use log::kv::{Key, Value, VisitSource};
use axum::Json;
use serde::{Deserialize, Serialize};
use crate::api_token::APIToken;
use crate::environment::{is_dev, is_flatpak};

const FLATPAK_PERSISTENT_DATA_DIRECTORY: &str = "/var/data";

static LOGGER: OnceLock<RuntimeLoggerHandle> = OnceLock::new();

static LOG_STARTUP_PATH: OnceLock<String> = OnceLock::new();

static LOG_APP_PATH: OnceLock<String> = OnceLock::new();

/// Initialize the logging system.
pub fn init_logging(bundle_identifier: &str) {

    //
    // Configure the LOGGER:
    //
    let mut log_config = String::new();

    // Set the log level depending on the environment:
    match is_dev() {
        true => log_config.push_str("debug, "),
        false => log_config.push_str("info, "),
    };

    // Keep noisy HTTP/TLS internals at info level even in development builds:
    log_config.push_str("h2=info, ");
    log_config.push_str("hyper=info, ");
    log_config.push_str("hyper_util=info, ");
    log_config.push_str("axum=info, ");
    log_config.push_str("axum_server=info, ");
    log_config.push_str("tower=info, ");
    log_config.push_str("tower_http=info, ");
    log_config.push_str("rustls=info, ");
    log_config.push_str("tokio_rustls=info, ");
    log_config.push_str("symphonia_format_mkv=info, ");
    log_config.push_str("reqwest=info");

    // Configure the initial filename. On Unix systems, the file should start
    // with a dot to be hidden.
    let log_basename = match cfg!(unix)
    {
        true => ".AI Studio Events",
        false => "AI Studio Events",
    };

    let (startup_log_directory, fallback_warning) = get_startup_log_path(bundle_identifier);
    let log_path = FileSpec::default()
        .directory(startup_log_directory)
        .basename(log_basename)
        .suppress_timestamp()
        .suffix("log");

    // Store the startup log path:
    store_startup_log_path(&LOG_STARTUP_PATH, &log_path);

    let runtime_logger = Logger::try_with_str(log_config).expect("Cannot create logging")
        .log_to_file(log_path)
        .duplicate_to_stdout(Duplicate::All)
        .use_utc()
        .format_for_files(file_logger_format)
        .set_palette("196;208;34;7;8".to_string()) // error, warn, info, debug, trace
        .format_for_stderr(terminal_colored_logger_format)
        .format_for_stdout(terminal_colored_logger_format)
        .start().expect("Cannot start logging");

    let runtime_logger = RuntimeLoggerHandle{
        handle: runtime_logger
    };

    LOGGER.set(runtime_logger).expect("Cannot set LOGGER");

    if let Some(fallback_warning) = fallback_warning {
        log::warn!("{fallback_warning}");
    }
}

fn store_startup_log_path(storage: &OnceLock<String>, log_path: &FileSpec) {
    let _ = storage.set(convert_log_path_to_string(log_path));
}

fn convert_log_path_to_string(log_path: &FileSpec) -> String {
    let log_path = log_path.as_pathbuf(None);
    
    // Case: The path is already absolute:
    if log_path.is_absolute() {
        return log_path.to_str().unwrap().to_string();
    }
    
    // Case: The path is relative. Let's try to convert it to an absolute path:
    match log_path.canonicalize() {
        // Case: The path exists:
        Ok(log_path) => log_path.to_str().unwrap().to_string(),

        // Case: The path does not exist. Let's try to build the
        // absolute path without touching the file system:
        Err(_) => match absolute(log_path.clone()) {

            // Case: We could build the absolute path:
            Ok(log_path) => log_path.to_str().unwrap().to_string(),

            // Case: We could not reconstruct the path using the working directory.
            Err(_) => log_path.to_str().unwrap().to_string(),
        }
    }
}

fn get_startup_log_path(bundle_identifier: &str) -> (PathBuf, Option<String>) {
    if is_flatpak() {
        return select_flatpak_startup_log_path(
            bundle_identifier,
            dirs::data_local_dir(),
            PathBuf::from(FLATPAK_PERSISTENT_DATA_DIRECTORY),
            temp_dir(),
            ensure_log_directory_is_writable,
        ).unwrap_or_else(|error| panic!("Cannot prepare a Flatpak startup log directory: {error}"));
    }

    (get_non_flatpak_startup_log_path(
        home_directory(),
        current_dir().ok(),
        temp_dir(),
    ), None)
}

// Note: Rust plans to remove the deprecation flag for std::env::home_dir() in Rust 1.86.0.
#[allow(deprecated)]
fn home_directory() -> Option<PathBuf> {
    std::env::home_dir()
}

fn get_non_flatpak_startup_log_path(
    home_directory: Option<PathBuf>,
    working_directory: Option<PathBuf>,
    temporary_directory: PathBuf,
) -> PathBuf {
    match home_directory {
        // Case: We could determine the home directory:
        Some(home_directory) => home_directory,
        
        // Case: We could not determine the home directory. Let's try to use the working directory:
        None => match working_directory {

            // Case: We could determine the working directory:
            Some(working_directory) => working_directory,

            // Case: We could not determine the working directory. Let's use the temporary directory:
            None => temporary_directory,
        },
    }
}

fn select_flatpak_startup_log_path<F>(
    bundle_identifier: &str,
    data_local_directory: Option<PathBuf>,
    persistent_data_directory: PathBuf,
    temporary_directory: PathBuf,
    mut ensure_writable: F,
) -> Result<(PathBuf, Option<String>), String>
where
    F: FnMut(&Path) -> Result<(), String>,
{
    let standard_directory = data_local_directory.map(|directory| directory.join(bundle_identifier).join("data"));
    let persistent_fallback = persistent_data_directory.join(bundle_identifier).join("data");
    let temporary_fallback = temporary_directory.join(bundle_identifier).join("data");
    let mut failures = Vec::new();

    if let Some(standard_directory) = standard_directory {
        match ensure_writable(&standard_directory) {
            Ok(()) => return Ok((standard_directory, None)),
            Err(error) => failures.push(format!("standard path failed: {error}")),
        }
    } else {
        failures.push(String::from("standard path failed: dirs::data_local_dir() returned no path"));
    }

    match ensure_writable(&persistent_fallback) {
        Ok(()) => {
            let warning = format!(
                "The standard Flatpak startup log directory was unavailable; using persistent fallback '{}'. {}",
                persistent_fallback.display(),
                failures.join("; "),
            );
            
            return Ok((persistent_fallback, Some(warning)));
        },
        
        Err(error) => failures.push(format!("persistent fallback failed: {error}")),
    }

    match ensure_writable(&temporary_fallback) {
        Ok(()) => {
            let warning = format!(
                "The standard and persistent Flatpak startup log directories were unavailable; using temporary fallback '{}'. {}",
                temporary_fallback.display(),
                failures.join("; "),
            );
            
            Ok((temporary_fallback, Some(warning)))
        },
        
        Err(error) => {
            failures.push(format!("temporary fallback failed: {error}"));
            Err(failures.join("; "))
        },
    }
}

fn ensure_log_directory_is_writable(directory: &Path) -> Result<(), String> {
    create_dir_all(directory).map_err(|error| format!("could not create '{}': {error}", directory.display()))?;
    let log_file_path = directory.join(if cfg!(unix) {
        ".AI Studio Events.log"
    } else {
        "AI Studio Events.log"
    });
    
    OpenOptions::new()
        .create(true)
        .append(true)
        .open(&log_file_path)
        .map(|_| ())
        .map_err(|error| format!("could not write '{}': {error}", log_file_path.display()))
}

/// Switch the logging system to a file-based output inside the given directory.
pub fn switch_to_file_logging(logger_path: PathBuf) -> Result<(), Box<dyn Error>>{
    let log_path = FileSpec::default()
        .directory(logger_path)
        .basename("events")
        .suppress_timestamp()
        .suffix("log");
    let _ = LOG_APP_PATH.set(convert_log_path_to_string(&log_path));
    LOGGER.get().expect("No LOGGER was set").handle.reset_flw(&FileLogWriter::builder(log_path))?;

    Ok(())
}

struct RuntimeLoggerHandle {
    handle: LoggerHandle
}

impl Debug for RuntimeLoggerHandle {
    fn fmt(&self, f: &mut std::fmt::Formatter<'_>) -> std::fmt::Result {
        write!(f, "LoggerHandle")
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

fn write_kv_pairs(w: &mut dyn std::io::Write, record: &log::Record) -> Result<(), std::io::Error> {
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

// Custom LOGGER format for the terminal:
fn terminal_colored_logger_format(
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

/// Custom LOGGER format for the log files:
fn file_logger_format(
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
    write!(w, "{}", record.args())
}

pub async fn get_log_paths(_token: APIToken) -> Json<LogPathsResponse> {
    Json(LogPathsResponse {
        log_startup_path: LOG_STARTUP_PATH.get().expect("No startup log path was set").clone(),
        log_app_path: LOG_APP_PATH.get().expect("No app log path was set").clone(),
    })
}

/// Converts a .NET log level string to a Rust log::Level.
fn parse_dotnet_log_level(level: &str) -> Level {
    match level {
        "Trace" | "Debug" => Level::Debug,
        "Information" => Level::Info,
        "Warning" => Level::Warn,
        "Error" | "Critical" => Level::Error,

        _ => Level::Error, // Fallback for unknown levels
    }
}

/// Logs a message with the specified level, including optional exception and stack trace.
fn log_with_level(
    level: Level,
    category: &str,
    message: &str,
    exception: Option<&String>,
    stack_trace: Option<&String>
) {
    // Log the main message:
    log::log!(level, Source = ".NET Server", Comp = category; "{message}");

    // Log exception if present:
    if let Some(ex) = exception {
        log::log!(level, Source = ".NET Server", Comp = category; "  Exception: {ex}");
    }

    // Log stack trace if present:
    if let Some(stack_trace) = stack_trace {
        for line in stack_trace.lines() {
            log::log!(level, Source = ".NET Server", Comp = category; "    {line}");
        }
    }
}

/// Logs an event from the .NET server.
pub async fn log_event(_token: APIToken, Json(event): Json<LogEvent>) -> Json<LogEventResponse> {
    let level = parse_dotnet_log_level(&event.level);
    let message = event.message.as_str();
    let category = event.category.as_str();

    log_with_level(
        level,
        category,
        message,
        event.exception.as_ref(),
        event.stack_trace.as_ref()
    );

    // Log warning for unknown levels:
    if !matches!(event.level.as_str(), "Trace" | "Debug" | "Information" | "Warning" | "Error" | "Critical") {
        log::warn!(Source = ".NET Server", Comp = category; "Unknown log level '{}' received.", event.level);
    }

    Json(LogEventResponse { success: true, issue: String::new() })
}

/// The response the get log paths request.
#[derive(Serialize)]
pub struct LogPathsResponse {
    log_startup_path: String,
    log_app_path: String,
}

/// A log event from the .NET server.
#[derive(Deserialize)]
#[allow(unused)]
pub struct LogEvent {
    timestamp: String,
    level: String,
    category: String,
    message: String,
    exception: Option<String>,
    stack_trace: Option<String>,
}

/// The response to a log event request.
#[derive(Serialize)]
pub struct LogEventResponse {
    success: bool,
    issue: String,
}

#[cfg(test)]
mod tests {
    use super::*;

    const BUNDLE_IDENTIFIER: &str = "org.mindworkai.AIStudio";

    #[test]
    fn flatpak_standard_path_matches_tauri_local_data_path() {
        let base_directory = PathBuf::from("/var/data");
        let expected = base_directory.join(BUNDLE_IDENTIFIER).join("data");

        let (selected, warning) = select_flatpak_startup_log_path(
            BUNDLE_IDENTIFIER,
            Some(base_directory),
            PathBuf::from("/persistent"),
            PathBuf::from("/temporary"),
            |_| Ok(()),
        ).unwrap();

        assert_eq!(selected, expected);
        assert!(warning.is_none());
    }

    #[test]
    fn flatpak_uses_persistent_fallback_when_standard_path_is_unwritable() {
        let standard = PathBuf::from("/standard").join(BUNDLE_IDENTIFIER).join("data");
        let persistent = PathBuf::from("/var/data").join(BUNDLE_IDENTIFIER).join("data");

        let (selected, warning) = select_flatpak_startup_log_path(
            BUNDLE_IDENTIFIER,
            Some(PathBuf::from("/standard")),
            PathBuf::from("/var/data"),
            PathBuf::from("/temporary"),
            |candidate| {
                if candidate == standard {
                    Err(String::from("read-only"))
                } else {
                    Ok(())
                }
            },
        ).unwrap();

        assert_eq!(selected, persistent);
        assert!(warning.unwrap().contains("persistent fallback"));
    }

    #[test]
    fn flatpak_uses_temporary_fallback_when_persistent_path_is_unwritable() {
        let temporary = PathBuf::from("/tmp").join(BUNDLE_IDENTIFIER).join("data");

        let (selected, warning) = select_flatpak_startup_log_path(
            BUNDLE_IDENTIFIER,
            None,
            PathBuf::from("/var/data"),
            PathBuf::from("/tmp"),
            |candidate| {
                if candidate == temporary {
                    Ok(())
                } else {
                    Err(String::from("read-only"))
                }
            },
        ).unwrap();

        assert_eq!(selected, temporary);
        assert!(warning.unwrap().contains("temporary fallback"));
    }

    #[test]
    fn non_flatpak_path_selection_keeps_existing_fallback_order() {
        let home = PathBuf::from("/home/user");
        let working = PathBuf::from("/working");
        let temporary = PathBuf::from("/tmp");

        assert_eq!(
            get_non_flatpak_startup_log_path(Some(home.clone()), Some(working.clone()), temporary.clone()),
            home,
        );
        assert_eq!(
            get_non_flatpak_startup_log_path(None, Some(working.clone()), temporary.clone()),
            working,
        );
        assert_eq!(
            get_non_flatpak_startup_log_path(None, None, temporary.clone()),
            temporary,
        );
    }

    #[test]
    fn startup_log_path_storage_uses_selected_fallback_path() {
        let temporary = PathBuf::from("/tmp").join(BUNDLE_IDENTIFIER).join("data");
        let (selected, _) = select_flatpak_startup_log_path(
            BUNDLE_IDENTIFIER,
            None,
            PathBuf::from("/var/data"),
            PathBuf::from("/tmp"),
            |candidate| {
                if candidate == temporary {
                    Ok(())
                } else {
                    Err(String::from("unavailable"))
                }
            },
        ).unwrap();
        let log_path = FileSpec::default()
            .directory(selected)
            .basename(".AI Studio Events")
            .suppress_timestamp()
            .suffix("log");
        let storage = OnceLock::new();

        store_startup_log_path(&storage, &log_path);

        assert_eq!(
            storage.get().unwrap(),
            "/tmp/org.mindworkai.AIStudio/data/.AI Studio Events.log",
        );
    }
}