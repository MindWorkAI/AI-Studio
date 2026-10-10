use std::collections::BTreeMap;
use std::env::{current_dir, home_dir, temp_dir};
use std::error::Error;
use std::fmt::Debug;
use std::fs::{create_dir_all, OpenOptions};
use std::path::{absolute, Path, PathBuf};
use std::sync::atomic::{AtomicBool, Ordering};
use std::sync::OnceLock;
use chrono::{DateTime, Utc};
use flexi_logger::{DeferredNow, Duplicate, FileSpec, Logger, LoggerHandle};
use flexi_logger::writers::FileLogWriter;
use log::{kv, Level};
use log::kv::{Key, Value, VisitSource};
use axum::Json;
use serde::{Deserialize, Serialize};
use crate::api_token::APIToken;
use crate::environment::{is_dev, is_flatpak};

const FLATPAK_PERSISTENT_DATA_DIRECTORY: &str = "/var/data";

/// The key under which a log record carries the time its event happened, in microseconds
/// since the Unix epoch. A record without it happened when it reached the logger. The
/// formatters start the line with this time and never write the key itself.
const CREATED_AT_KEY: &str = "created_at_micros";

/// The key under which a line shows how late its record reached the logger.
const DELAY_KEY: &str = "Delay";

/// From this many milliseconds on, a line shows how late its record reached the logger.
/// Below it lies the usual transport time, which would only be noise.
const DELAY_THRESHOLD_MS: i64 = 100;

static LOGGER: OnceLock<RuntimeLoggerHandle> = OnceLock::new();

static LOG_STARTUP_PATH: OnceLock<String> = OnceLock::new();

static LOG_APP_PATH: OnceLock<String> = OnceLock::new();

/// Whether we already warned that the .NET server sent a timestamp we cannot read.
/// Once is enough: when one is unreadable, all of them are.
static WARNED_ABOUT_UNREADABLE_DOTNET_TIMESTAMP: AtomicBool = AtomicBool::new(false);

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
    log_config.push_str("reqwest=info, ");

    // Hide harmless Qdrant Edge messages. Qdrant initializes its feature flags and the
    // multi-mmap check only in its own binaries; the module is private in the crate, so
    // we cannot do it. Qdrant Edge then falls back to its defaults, which are correct
    // for us, but warns on every load and every optimization. On Windows, it logs every
    // ignored madvise call at debug level. Check these modules again for new warnings
    // whenever qdrant-edge gets updated, and drop the first two filters once
    // https://github.com/qdrant/qdrant/issues/11069 is solved:
    log_config.push_str("qdrant_edge::common::flags=error, ");
    log_config.push_str("qdrant_edge::common::mmap::ops=error, ");
    log_config.push_str("qdrant_edge::common::mmap::advice=info");

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
        home_dir(),
        current_dir().ok(),
        temp_dir(),
    ), None)
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
        // The creation time is the timestamp of the line, not one of its pairs:
        if key.as_str() != CREATED_AT_KEY {
            self.0.insert(key, value);
        }

        Ok(())
    }
}

fn write_kv_pairs(w: &mut dyn std::io::Write, record: &log::Record, delay_ms: Option<i64>) -> Result<(), std::io::Error> {
    let delay = delay_ms.map(|delay_ms| format!("{delay_ms} ms"));
    let mut visitor = LogKVCollect(BTreeMap::new());
    record.key_values().visit(&mut visitor).unwrap();
    if let Some(delay) = &delay {
        visitor.0.insert(Key::from_str(DELAY_KEY), Value::from_display(delay));
    }

    if !visitor.0.is_empty() {
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

/// When the event of a log record happened, and how late the record reached the logger.
struct LogTime {
    timestamp: String,
    delay_ms: Option<i64>,
}

fn get_log_time(now: &mut DeferredNow, record: &log::Record) -> LogTime {
    let created_at = record
        .key_values()
        .get(Key::from_str(CREATED_AT_KEY))
        .and_then(|value| value.to_i64())
        .and_then(DateTime::<Utc>::from_timestamp_micros);

    match created_at {
        // Case: The event happened when its record reached the logger:
        None => LogTime {
            timestamp: now.format(flexi_logger::TS_DASHES_BLANK_COLONS_DOT_BLANK).to_string(),
            delay_ms: None,
        },

        // Case: The event happened earlier, e.g., in the .NET server. The logger
        // runs in UTC (see init_logging), so this time is written in UTC as well:
        Some(created_at) => LogTime {
            timestamp: created_at.format(flexi_logger::TS_DASHES_BLANK_COLONS_DOT_BLANK).to_string(),
            delay_ms: get_notable_delay_ms(created_at, now.now_utc_owned()),
        },
    }
}

/// Returns how late a record reached the logger, but only when the delay is notable.
fn get_notable_delay_ms(created_at: DateTime<Utc>, arrived_at: DateTime<Utc>) -> Option<i64> {
    let delay_ms = (arrived_at - created_at).num_milliseconds();
    (delay_ms >= DELAY_THRESHOLD_MS).then_some(delay_ms)
}

// Custom LOGGER format for the terminal:
fn terminal_colored_logger_format(
    w: &mut dyn std::io::Write,
    now: &mut DeferredNow,
    record: &log::Record,
) -> Result<(), std::io::Error> {
    let level = record.level();
    let log_time = get_log_time(now, record);

    // Write the timestamp, log level, and module path:
    write!(
        w,
        "[{}] {} [{}] ",
        flexi_logger::style(level).paint(log_time.timestamp),
        flexi_logger::style(level).paint(record.level().to_string()),
        record.module_path().unwrap_or("<unnamed>"),
    )?;

    // Write all key-value pairs:
    write_kv_pairs(w, record, log_time.delay_ms)?;

    // Write the log message:
    write!(w, "{}", flexi_logger::style(level).paint(record.args().to_string()))
}

/// Custom LOGGER format for the log files:
fn file_logger_format(
    w: &mut dyn std::io::Write,
    now: &mut DeferredNow,
    record: &log::Record,
) -> Result<(), std::io::Error> {
    let log_time = get_log_time(now, record);

    // Write the timestamp, log level, and module path:
    write!(
        w,
        "[{}] {} [{}] ",
        log_time.timestamp,
        record.level(),
        record.module_path().unwrap_or("<unnamed>"),
    )?;

    // Write all key-value pairs:
    write_kv_pairs(w, record, log_time.delay_ms)?;

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

/// Reads the timestamp of a .NET log event, e.g., `2026-10-09T17:53:54.5629130+00:00`,
/// and returns it in microseconds since the Unix epoch.
fn parse_dotnet_timestamp(timestamp: &str) -> Option<i64> {
    DateTime::parse_from_rfc3339(timestamp)
        .ok()
        .map(|timestamp| timestamp.timestamp_micros())
}

/// Logs a message with the specified level, including optional exception and stack trace.
/// When the time the event happened is known, every line carries it.
fn log_with_level(
    logger: &dyn log::Log,
    level: Level,
    category: &str,
    created_at: Option<i64>,
    message: &str,
    exception: Option<&String>,
    stack_trace: Option<&String>
) {
    // Log the main message:
    log::log!(logger: logger, level, Source = ".NET Server", Comp = category, (CREATED_AT_KEY) = created_at; "{message}");

    // Log exception if present:
    if let Some(ex) = exception {
        log::log!(logger: logger, level, Source = ".NET Server", Comp = category, (CREATED_AT_KEY) = created_at; "  Exception: {ex}");
    }

    // Log stack trace if present:
    if let Some(stack_trace) = stack_trace {
        for line in stack_trace.lines() {
            log::log!(logger: logger, level, Source = ".NET Server", Comp = category, (CREATED_AT_KEY) = created_at; "    {line}");
        }
    }
}

/// Logs an event from the .NET server.
pub async fn log_event(_token: APIToken, Json(event): Json<LogEvent>) -> Json<LogEventResponse> {
    let level = parse_dotnet_log_level(&event.level);
    let message = event.message.as_str();
    let category = event.category.as_str();
    let created_at = parse_dotnet_timestamp(&event.timestamp);

    log_with_level(
        log::logger(),
        level,
        category,
        created_at,
        message,
        event.exception.as_ref(),
        event.stack_trace.as_ref()
    );

    // Log warning for unknown levels:
    if !matches!(event.level.as_str(), "Trace" | "Debug" | "Information" | "Warning" | "Error" | "Critical") {
        log::warn!(Source = ".NET Server", Comp = category; "Unknown log level '{}' received.", event.level);
    }

    // Log warning for unreadable timestamps, but only once:
    if created_at.is_none() && !WARNED_ABOUT_UNREADABLE_DOTNET_TIMESTAMP.swap(true, Ordering::Relaxed) {
        log::warn!(Source = ".NET Server", Comp = category; "Could not read the timestamp '{}' of a log event. Such events show the time they arrived instead. This warning appears only once.", event.timestamp);
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
pub struct LogEvent {
    /// When the event happened, in the round-trip format of .NET,
    /// e.g., `2026-10-09T17:53:54.5629130+00:00`.
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
    use std::sync::Mutex;
    use chrono::{TimeDelta, TimeZone};
    use flexi_logger::FormatFunction;
    use log::kv::{Source, ToValue};
    use log::LevelFilter;

    const BUNDLE_IDENTIFIER: &str = "org.mindworkai.AIStudio";

    const MODULE_PATH: &str = "mindwork_ai_studio::log";

    fn write_line(format: FormatFunction, key_values: &dyn Source) -> String {
        let mut line = Vec::new();
        format(
            &mut line,
            &mut DeferredNow::new(),
            &log::Record::builder()
                .args(format_args!("Hello"))
                .level(Level::Info)
                .module_path(Some(MODULE_PATH))
                .key_values(key_values)
                .build(),
        ).unwrap();

        String::from_utf8(line).unwrap()
    }

    fn micros_ago(milliseconds: i64) -> i64 {
        (Utc::now() - TimeDelta::milliseconds(milliseconds)).timestamp_micros()
    }

    fn format_micros(micros: i64) -> String {
        DateTime::<Utc>::from_timestamp_micros(micros)
            .unwrap()
            .format(flexi_logger::TS_DASHES_BLANK_COLONS_DOT_BLANK)
            .to_string()
    }

    #[test]
    fn late_line_starts_with_the_time_its_event_happened() {
        let created_at = micros_ago(2_000);
        let line = write_line(file_logger_format, &[
            ("Source", ".NET Server".to_value()),
            (CREATED_AT_KEY, Some(created_at).to_value()),
        ]);

        let prefix = format!("[{}] INFO [{MODULE_PATH}] [Delay = ", format_micros(created_at));
        assert!(line.starts_with(&prefix), "{line}");
        assert!(line.ends_with(" ms, Source = .NET Server] Hello"), "{line}");

        let delay_ms: i64 = line[prefix.len()..].split(" ms").next().unwrap().parse().unwrap();
        assert!((2_000..60_000).contains(&delay_ms), "{line}");
    }

    #[test]
    fn usual_transport_time_shows_no_delay() {
        let created_at = micros_ago(0);
        let line = write_line(file_logger_format, &[
            ("Source", ".NET Server".to_value()),
            (CREATED_AT_KEY, Some(created_at).to_value()),
        ]);

        assert_eq!(line, format!("[{}] INFO [{MODULE_PATH}] [Source = .NET Server] Hello", format_micros(created_at)));
    }

    #[test]
    fn line_without_creation_time_shows_its_arrival_time() {
        let missing_created_at: Option<i64> = None;
        let without_key: [(&str, Value); 1] = [("Source", ".NET Server".to_value())];
        let without_value: [(&str, Value); 2] = [
            ("Source", ".NET Server".to_value()),
            (CREATED_AT_KEY, missing_created_at.to_value()),
        ];

        for key_values in [&without_key as &dyn Source, &without_value] {
            let before = Utc::now().timestamp_micros();
            let line = write_line(file_logger_format, key_values);
            let after = Utc::now().timestamp_micros();

            let timestamp = &line[1..line.find(']').unwrap()];
            let arrived_at = DateTime::parse_from_str(timestamp, flexi_logger::TS_DASHES_BLANK_COLONS_DOT_BLANK).unwrap().timestamp_micros();
            assert!((before..=after).contains(&arrived_at), "{line}");
            assert!(line.ends_with(&format!("INFO [{MODULE_PATH}] [Source = .NET Server] Hello")), "{line}");
        }
    }

    #[test]
    fn creation_time_is_never_written_as_a_pair() {
        let formats: [FormatFunction; 2] = [file_logger_format, terminal_colored_logger_format];
        for format in formats {
            let late_line = write_line(format, &[(CREATED_AT_KEY, micros_ago(2_000))]);
            assert!(late_line.contains("[Delay = "), "{late_line}");
            assert!(!late_line.contains(CREATED_AT_KEY), "{late_line}");

            let line = write_line(format, &[(CREATED_AT_KEY, micros_ago(0))]);
            assert!(!line.contains(CREATED_AT_KEY), "{line}");
            assert!(!line.contains("[] "), "{line}");
        }
    }

    #[test]
    fn delay_counts_only_from_the_threshold_on() {
        let created_at = Utc::now();
        let arrived_after = |milliseconds| created_at + TimeDelta::milliseconds(milliseconds);

        assert_eq!(get_notable_delay_ms(created_at, arrived_after(DELAY_THRESHOLD_MS - 1)), None);
        assert_eq!(get_notable_delay_ms(created_at, arrived_after(DELAY_THRESHOLD_MS)), Some(DELAY_THRESHOLD_MS));
        assert_eq!(get_notable_delay_ms(created_at, arrived_after(1_583)), Some(1_583));

        // A record that seems to arrive before its event happened shows no delay:
        assert_eq!(get_notable_delay_ms(created_at, arrived_after(-5)), None);
    }

    #[test]
    fn dotnet_timestamps_are_read_in_microseconds() {
        let expected = (Utc.with_ymd_and_hms(2026, 10, 9, 17, 53, 54).unwrap() + TimeDelta::microseconds(562_913)).timestamp_micros();

        assert_eq!(parse_dotnet_timestamp("2026-10-09T17:53:54.5629130+00:00"), Some(expected));
        assert_eq!(parse_dotnet_timestamp("2026-10-09T19:53:54.5629130+02:00"), Some(expected));
    }

    #[test]
    fn unreadable_dotnet_timestamps_are_rejected() {
        // The format .NET sent before, with the time separator of the current culture:
        assert_eq!(parse_dotnet_timestamp("2026-10-09 17:53:54.562"), None);
        assert_eq!(parse_dotnet_timestamp("2026-10-09 17.53.54.562"), None);
        assert_eq!(parse_dotnet_timestamp(""), None);
    }

    /// Remembers the message and the creation time of every record it receives.
    #[derive(Default)]
    struct CapturingLogger(Mutex<Vec<(String, Option<i64>)>>);

    impl log::Log for CapturingLogger {
        fn enabled(&self, _: &log::Metadata) -> bool {
            true
        }

        fn log(&self, record: &log::Record) {
            let created_at = record
                .key_values()
                .get(Key::from_str(CREATED_AT_KEY))
                .and_then(|value| value.to_i64());

            self.0.lock().unwrap().push((record.args().to_string(), created_at));
        }

        fn flush(&self) {}
    }

    #[test]
    fn every_line_of_a_dotnet_event_carries_its_time() {
        // Without a global logger, the log macros would drop every record before it reaches ours:
        log::set_max_level(LevelFilter::Info);

        let logger = CapturingLogger::default();
        let created_at = parse_dotnet_timestamp("2026-10-09T17:53:54.5629130+00:00");
        let exception = String::from("Boom");
        let stack_trace = String::from("at First()\nat Second()");
        assert!(created_at.is_some());

        log_with_level(&logger, Level::Info, "Tests", created_at, "Hello", Some(&exception), Some(&stack_trace));

        assert_eq!(logger.0.into_inner().unwrap(), vec![
            (String::from("Hello"), created_at),
            (String::from("  Exception: Boom"), created_at),
            (String::from("    at First()"), created_at),
            (String::from("    at Second()"), created_at),
        ]);
    }

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