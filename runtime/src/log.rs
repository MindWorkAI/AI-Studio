use std::collections::BTreeMap;
use std::error::Error;
use std::fmt::Debug;
use std::path::PathBuf;
use std::sync::OnceLock;
use flexi_logger::{DeferredNow, Duplicate, FileSpec, Logger, LoggerHandle};
use flexi_logger::writers::FileLogWriter;
use log::kv;
use log::kv::{Key, Value, VisitSource};
use crate::environment::is_dev;

static LOGGER: OnceLock<RuntimeLoggerHandle> = OnceLock::new();

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

    let runtime_logger = Logger::try_with_str(log_config).expect("Cannot create logging")
        .log_to_file(FileSpec::default()
            .basename(log_basename)
            .suppress_timestamp()
            .suffix("log"))
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

/// Switch the logging system to a file-based output.
pub fn switch_to_file_logging(logger_path: PathBuf) -> Result<(), Box<dyn Error>>{
    LOGGER.get().expect("No LOGGER was set").handle.reset_flw(&FileLogWriter::builder(
        FileSpec::default()
            .directory(logger_path)
            .basename("events")
            .suppress_timestamp()
            .suffix("log")))?;

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