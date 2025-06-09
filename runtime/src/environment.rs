use std::env;
use std::sync::OnceLock;
use log::{debug, warn};
use rocket::{delete, get};
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
        warn!("Could not determine the system language. Use default 'en-US'.");
        String::from("en-US")
    })
}

#[get("/system/enterprise/config/id")]
pub fn read_enterprise_env_config_id(_token: APIToken) -> String {
    //
    // When we are on a Windows machine, we try to read the enterprise config from
    // the Windows registry. In case we can't find the registry key, or we are on a
    // macOS or Linux machine, we try to read the enterprise config from the
    // environment variables.
    //
    // The registry key is:
    // HKEY_CURRENT_USER\Software\github\MindWork AI Studio\Enterprise IT
    //
    // In this registry key, we expect the following values:
    // - config_id
    //
    // The environment variable is:
    // MINDWORK_AI_STUDIO_ENTERPRISE_CONFIG_ID
    //
    debug!("Trying to read the enterprise environment for some config ID.");
    get_enterprise_configuration(
        "config_id",
        "MINDWORK_AI_STUDIO_ENTERPRISE_CONFIG_ID",
    )
}

#[delete("/system/enterprise/config/id")]
pub fn delete_enterprise_env_config_id(_token: APIToken) -> String {
    //
    // When we are on a Windows machine, we try to read the enterprise config from
    // the Windows registry. In case we can't find the registry key, or we are on a
    // macOS or Linux machine, we try to read the enterprise config from the
    // environment variables.
    //
    // The registry key is:
    // HKEY_CURRENT_USER\Software\github\MindWork AI Studio\Enterprise IT
    //
    // In this registry key, we expect the following values:
    // - delete_config_id
    //
    // The environment variable is:
    // MINDWORK_AI_STUDIO_ENTERPRISE_DELETE_CONFIG_ID
    //
    debug!("Trying to read the enterprise environment for some config ID, which should be deleted.");
    get_enterprise_configuration(
        "delete_config_id",
        "MINDWORK_AI_STUDIO_ENTERPRISE_DELETE_CONFIG_ID",
    )
}

#[get("/system/enterprise/config/server")]
pub fn read_enterprise_env_config_server_url(_token: APIToken) -> String {
    //
    // When we are on a Windows machine, we try to read the enterprise config from
    // the Windows registry. In case we can't find the registry key, or we are on a
    // macOS or Linux machine, we try to read the enterprise config from the
    // environment variables.
    //
    // The registry key is:
    // HKEY_CURRENT_USER\Software\github\MindWork AI Studio\Enterprise IT
    //
    // In this registry key, we expect the following values:
    // - config_server_url
    //
    // The environment variable is:
    // MINDWORK_AI_STUDIO_ENTERPRISE_CONFIG_SERVER_URL
    //
    debug!("Trying to read the enterprise environment for the config server URL.");
    get_enterprise_configuration(
        "config_server_url",
        "MINDWORK_AI_STUDIO_ENTERPRISE_CONFIG_SERVER_URL",
    )
}

fn get_enterprise_configuration(_reg_value: &str, env_name: &str) -> String {
    cfg_if::cfg_if! {
        if #[cfg(target_os = "windows")] {
            debug!(r"Detected a Windows machine, trying to read the registry key 'HKEY_CURRENT_USER\Software\github\MindWork AI Studio\Enterprise IT' or environment variables.");
            use windows_registry::*;
            let key_path = r"Software\github\MindWork AI Studio\Enterprise IT";
            let key = match CURRENT_USER.open(key_path) {
                Ok(key) => key,
                Err(_) => {
                    debug!(r"Could not read the registry key HKEY_CURRENT_USER\Software\github\MindWork AI Studio\Enterprise IT. Falling back to environment variables.");
                    return match env::var(env_name) {
                        Ok(val) => {
                            debug!("Falling back to the environment variable '{}' was successful.", env_name);
                            val
                        },
                        Err(_) => {
                            debug!("Falling back to the environment variable '{}' was not successful.", env_name);
                            "".to_string()
                        },
                    }
                },
            };

            match key.get_string(_reg_value) {
                Ok(val) => val,
                Err(_) => {
                    debug!(r"We could read the registry key 'HKEY_CURRENT_USER\Software\github\MindWork AI Studio\Enterprise IT', but the value '{}' could not be read. Falling back to environment variables.", _reg_value);
                    match env::var(env_name) {
                        Ok(val) => {
                            debug!("Falling back to the environment variable '{}' was successful.", env_name);
                            val
                        },
                        Err(_) => {
                            debug!("Falling back to the environment variable '{}' was not successful.", env_name);
                            "".to_string()
                        }
                    }
                },
            }
        } else {
            // In the case of macOS or Linux, we just read the environment variable:
            debug!(r"Detected a Unix machine, trying to read the environment variable '{}'.", env_name);
            match env::var(env_name) {
                Ok(val) => val,
                Err(_) => {
                    debug!("The environment variable '{}' was not found.", env_name);
                    "".to_string()
                }
            }
        }
    }
}