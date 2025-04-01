use std::collections::BTreeMap;
use std::env::{current_dir, temp_dir};
use std::error::Error;
use std::fmt::Debug;
use std::path::{absolute, PathBuf};
use std::sync::OnceLock;
use flexi_logger::{DeferredNow, Duplicate, FileSpec, Logger, LoggerHandle};
use flexi_logger::writers::FileLogWriter;
use log::kv;
use log::kv::{Key, Value, VisitSource};
use rocket::get;
use rocket::serde::json::Json;
use rocket::serde::Serialize;
use crate::api_token::APIToken;
use crate::environment::is_dev;

static LOGGER: OnceLock<RuntimeLoggerHandle> = OnceLock::new();

static LOG_STARTUP_PATH: OnceLock<String> = OnceLock::new();

static LOG_APP_PATH: OnceLock<String> = OnceLock::new();

/// Initialize the logging system.
pub fn init_logging() {

    //
    // Configure the LOGGER:
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

    // Configure the initial filename. On Unix systems, the file should start
    // with a dot to be hidden.
    let log_basename = match cfg!(unix)
    {
        true => ".AI Studio Events",
        false => "AI Studio Events",
    };

    let log_path = FileSpec::default()
        .directory(get_startup_log_path())
        .basename(log_basename)
        .suppress_timestamp()
        .suffix("log");

    // Store the startup log path:
    let _ = LOG_STARTUP_PATH.set(convert_log_path_to_string(&log_path));

    let runtime_logger = Logger::try_with_str(log_config).expect("Cannot create logging")
        .log_to_file(log_path)
        .duplicate_to_stdout(Duplicate::All)
        .use_utc()
        .format_for_files(file_logger_format)
        .format_for_stderr(terminal_colored_logger_format)
        .format_for_stdout(terminal_colored_logger_format)
        .start().expect("Cannot start logging");

    let runtime_logger = RuntimeLoggerHandle{
        handle: runtime_logger
    };

    LOGGER.set(runtime_logger).expect("Cannot set LOGGER");
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

// Note: Rust plans to remove the deprecation flag for std::env::home_dir() in Rust 1.86.0.
#[allow(deprecated)]
fn get_startup_log_path() -> String {
    match std::env::home_dir() {
        // Case: We could determine the home directory:
        Some(home_dir) => home_dir.to_str().unwrap().to_string(),
        
        // Case: We could not determine the home directory. Let's try to use the working directory:
        None => match current_dir() {

            // Case: We could determine the working directory:
            Ok(working_directory) => working_directory.to_str().unwrap().to_string(),

            // Case: We could not determine the working directory. Let's use the temporary directory:
            Err(_) => temp_dir().to_str().unwrap().to_string(),
        },
    }
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
    write!(w, "{}", &record.args())
}

#[get("/log/paths")]
pub async fn get_log_paths(_token: APIToken) -> Json<LogPathsResponse> {
    Json(LogPathsResponse {
        log_startup_path: LOG_STARTUP_PATH.get().expect("No startup log path was set").clone(),
        log_app_path: LOG_APP_PATH.get().expect("No app log path was set").clone(),
    })
}

/// The response the get log paths request.
#[derive(Serialize)]
pub struct LogPathsResponse {
    log_startup_path: String,
    log_app_path: String,
}