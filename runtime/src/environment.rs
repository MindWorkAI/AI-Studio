use std::sync::OnceLock;
use rocket::get;
use sys_locale::get_locale;
use crate::api_token::APIToken;

/// The data directory where the application stores its data.
pub static DATA_DIRECTORY: OnceLock<String> = OnceLock::new();

/// The config directory where the application stores its configuration.
pub static CONFIG_DIRECTORY: OnceLock<String> = OnceLock::new();

/// Returns the config directory.
#[get("/system/directories/config")]
pub fn get_config_directory(_token: APIToken) -> String {
    match CONFIG_DIRECTORY.get() {
        Some(config_directory) => config_directory.clone(),
        None => String::from(""),
    }
}

/// Returns the data directory.
#[get("/system/directories/data")]
pub fn get_data_directory(_token: APIToken) -> String {
    match DATA_DIRECTORY.get() {
        Some(data_directory) => data_directory.clone(),
        None => String::from(""),
    }
}

/// Returns true if the application is running in development mode.
pub fn is_dev() -> bool {
    cfg!(debug_assertions)
}

/// Returns true if the application is running in production mode.
pub fn is_prod() -> bool {
    !is_dev()
}

#[get("/system/language")]
pub fn read_user_language(_token: APIToken) -> String {
    get_locale().unwrap_or_else(|| {
        log::warn!("Could not determine the system language. Use default 'en-US'.");
        String::from("en-US")
    })
}