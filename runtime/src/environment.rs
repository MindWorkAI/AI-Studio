use std::env;
use std::sync::OnceLock;
use log::{debug, info, warn};
use rocket::get;
use rocket::serde::json::Json;
use serde::Serialize;
use sys_locale::get_locale;
use crate::api_token::APIToken;

const DEFAULT_LANGUAGE: &str = "en-US";

/// The data directory where the application stores its data.
pub static DATA_DIRECTORY: OnceLock<String> = OnceLock::new();

/// The config directory where the application stores its configuration.
pub static CONFIG_DIRECTORY: OnceLock<String> = OnceLock::new();

/// The user language cached once per runtime process.
static USER_LANGUAGE: OnceLock<String> = OnceLock::new();

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

fn normalize_locale_tag(locale: &str) -> Option<String> {
    let trimmed = locale.trim();
    if trimmed.is_empty() {
        return None;
    }

    let without_encoding = trimmed
        .split('.')
        .next()
        .unwrap_or(trimmed)
        .split('@')
        .next()
        .unwrap_or(trimmed)
        .trim();

    if without_encoding.is_empty() {
        return None;
    }

    let normalized_delimiters = without_encoding.replace('_', "-");
    let mut segments = normalized_delimiters
        .split('-')
        .filter(|segment| !segment.is_empty());

    let language = segments.next()?;
    if language.eq_ignore_ascii_case("c") || language.eq_ignore_ascii_case("posix") {
        return None;
    }

    let language = language.to_ascii_lowercase();
    if language.len() < 2 || !language.chars().all(|c| c.is_ascii_alphabetic()) {
        return None;
    }

    if let Some(region) = segments.next() {
        if region.len() == 2 && region.chars().all(|c| c.is_ascii_alphabetic()) {
            return Some(format!("{}-{}", language, region.to_ascii_uppercase()));
        }
    }

    Some(language)
}

#[cfg(target_os = "linux")]
fn read_locale_from_environment() -> Option<(String, &'static str)> {
    if let Ok(language) = env::var("LANGUAGE") {
        for candidate in language.split(':') {
            if let Some(locale) = normalize_locale_tag(candidate) {
                return Some((locale, "LANGUAGE"));
            }
        }
    }

    for key in ["LC_ALL", "LC_MESSAGES", "LANG"] {
        if let Ok(value) = env::var(key) {
            if let Some(locale) = normalize_locale_tag(&value) {
                return Some((locale, key));
            }
        }
    }

    None
}

#[cfg(not(target_os = "linux"))]
fn read_locale_from_environment() -> Option<(String, &'static str)> {
    None
}

enum LanguageDetectionSource {
    SysLocale,
    LinuxEnvironmentVariable(&'static str),
    DefaultLanguage,
}

fn detect_user_language() -> (String, LanguageDetectionSource) {
    if let Some(locale) = get_locale() {
        if let Some(normalized_locale) = normalize_locale_tag(&locale) {
            return (normalized_locale, LanguageDetectionSource::SysLocale);
        }

        warn!("sys-locale returned an unusable locale value: '{}'.", locale);
    }

    if let Some((locale, key)) = read_locale_from_environment() {
        return (locale, LanguageDetectionSource::LinuxEnvironmentVariable(key));
    }

    (
        String::from(DEFAULT_LANGUAGE),
        LanguageDetectionSource::DefaultLanguage,
    )
}

#[cfg(test)]
mod tests {
    use super::normalize_locale_tag;

    #[test]
    fn normalize_locale_tag_supports_common_linux_formats() {
        assert_eq!(normalize_locale_tag("de_DE.UTF-8"), Some(String::from("de-DE")));
        assert_eq!(normalize_locale_tag("de_DE@euro"), Some(String::from("de-DE")));
        assert_eq!(normalize_locale_tag("de"), Some(String::from("de")));
        assert_eq!(normalize_locale_tag("en-US"), Some(String::from("en-US")));
    }

    #[test]
    fn normalize_locale_tag_rejects_non_language_locales() {
        assert_eq!(normalize_locale_tag("C"), None);
        assert_eq!(normalize_locale_tag("C.UTF-8"), None);
        assert_eq!(normalize_locale_tag("POSIX"), None);
        assert_eq!(normalize_locale_tag(""), None);
    }
}

#[get("/system/language")]
pub fn read_user_language(_token: APIToken) -> String {
    USER_LANGUAGE
        .get_or_init(|| {
            let (user_language, source) = detect_user_language();
            match source {
                LanguageDetectionSource::SysLocale => {
                    info!("Detected user language from sys-locale: '{}'.", user_language);
                },

                LanguageDetectionSource::LinuxEnvironmentVariable(key) => {
                    info!(
                        "Detected user language from Linux environment variable '{}': '{}'.",
                        key, user_language
                    );
                },

                LanguageDetectionSource::DefaultLanguage => {
                    warn!(
                        "Could not determine the system language. Use default '{}'.",
                        DEFAULT_LANGUAGE
                    );
                },
            }

            user_language
        })
        .clone()
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

#[get("/system/enterprise/config/encryption_secret")]
pub fn read_enterprise_env_config_encryption_secret(_token: APIToken) -> String {
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
    // - config_encryption_secret
    //
    // The environment variable is:
    // MINDWORK_AI_STUDIO_ENTERPRISE_CONFIG_ENCRYPTION_SECRET
    //
    debug!("Trying to read the enterprise environment for the config encryption secret.");
    get_enterprise_configuration(
        "config_encryption_secret",
        "MINDWORK_AI_STUDIO_ENTERPRISE_CONFIG_ENCRYPTION_SECRET",
    )
}

/// Represents a single enterprise configuration entry with an ID and server URL.
#[derive(Serialize)]
pub struct EnterpriseConfig {
    pub id: String,
    pub server_url: String,
}

/// Returns all enterprise configurations. Collects configurations from both the
/// new multi-config format (`id1@url1;id2@url2`) and the legacy single-config
/// environment variables, merging them into one list. Duplicates (by ID) are
/// skipped — the first occurrence wins.
#[get("/system/enterprise/configs")]
pub fn read_enterprise_configs(_token: APIToken) -> Json<Vec<EnterpriseConfig>> {
    info!("Trying to read the enterprise environment for all configurations.");

    let mut configs: Vec<EnterpriseConfig> = Vec::new();
    let mut seen_ids: std::collections::HashSet<String> = std::collections::HashSet::new();

    // Read the new combined format:
    let combined = get_enterprise_configuration(
        "configs",
        "MINDWORK_AI_STUDIO_ENTERPRISE_CONFIGS",
    );

    if !combined.is_empty() {
        // Parse the new format: id1@url1;id2@url2;...
        for entry in combined.split(';') {
            let entry = entry.trim();
            if entry.is_empty() {
                continue;
            }

            // Split at the first '@' (GUIDs never contain '@'):
            if let Some((id, url)) = entry.split_once('@') {
                let id = id.trim().to_lowercase();
                let url = url.trim().to_string();
                if !id.is_empty() && !url.is_empty() && seen_ids.insert(id.clone()) {
                    configs.push(EnterpriseConfig { id, server_url: url });
                }
            }
        }
    }

    // Also read the legacy single-config variables:
    let config_id = get_enterprise_configuration(
        "config_id",
        "MINDWORK_AI_STUDIO_ENTERPRISE_CONFIG_ID",
    );

    let config_server_url = get_enterprise_configuration(
        "config_server_url",
        "MINDWORK_AI_STUDIO_ENTERPRISE_CONFIG_SERVER_URL",
    );

    if !config_id.is_empty() && !config_server_url.is_empty() {
        let id = config_id.trim().to_lowercase();
        if seen_ids.insert(id.clone()) {
            configs.push(EnterpriseConfig { id, server_url: config_server_url });
        }
    }

    Json(configs)
}

fn get_enterprise_configuration(_reg_value: &str, env_name: &str) -> String {
    cfg_if::cfg_if! {
        if #[cfg(target_os = "windows")] {
            info!(r"Detected a Windows machine, trying to read the registry key 'HKEY_CURRENT_USER\Software\github\MindWork AI Studio\Enterprise IT\{}' or the environment variable '{}'.", _reg_value, env_name);
            use windows_registry::*;
            let key_path = r"Software\github\MindWork AI Studio\Enterprise IT";
            let key = match CURRENT_USER.open(key_path) {
                Ok(key) => key,
                Err(_) => {
                    info!(r"Could not read the registry key 'HKEY_CURRENT_USER\Software\github\MindWork AI Studio\Enterprise IT\{}'. Falling back to the environment variable '{}'.", _reg_value, env_name);
                    return match env::var(env_name) {
                        Ok(val) => {
                            info!("Falling back to the environment variable '{}' was successful.", env_name);
                            val
                        },
                        Err(_) => {
                            info!("Falling back to the environment variable '{}' was not successful. It seems that there is no enterprise environment available.", env_name);
                            "".to_string()
                        },
                    }
                },
            };

            match key.get_string(_reg_value) {
                Ok(val) => val,
                Err(_) => {
                    info!(r"We could read the registry key 'HKEY_CURRENT_USER\Software\github\MindWork AI Studio\Enterprise IT', but the value '{}' could not be read. Falling back to the environment variable '{}'.", _reg_value, env_name);
                    match env::var(env_name) {
                        Ok(val) => {
                            info!("Falling back to the environment variable '{}' was successful.", env_name);
                            val
                        },
                        Err(_) => {
                            info!("Falling back to the environment variable '{}' was not successful. It seems that there is no enterprise environment available.", env_name);
                            "".to_string()
                        }
                    }
                },
            }
        } else {
            // In the case of macOS or Linux, we just read the environment variable:
            info!(r"Detected a Unix machine, trying to read the environment variable '{}'.", env_name);
            match env::var(env_name) {
                Ok(val) => val,
                Err(_) => {
                    info!("The environment variable '{}' was not found. It seems that there is no enterprise environment available.", env_name);
                    "".to_string()
                }
            }
        }
    }
}
