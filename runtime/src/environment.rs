use crate::api_token::APIToken;
use axum::Json;
use log::{debug, info, warn};
use serde::Serialize;
use std::collections::{HashMap, HashSet};
use std::env;
use std::fs;
use std::path::{Path, PathBuf};
use std::sync::OnceLock;
use sys_locale::get_locale;

const DEFAULT_LANGUAGE: &str = "en-US";

const ENTERPRISE_CONFIG_SLOT_COUNT: usize = 10;

#[cfg(target_os = "windows")]
const ENTERPRISE_REGISTRY_KEY_PATH: &str = r"Software\github\MindWork AI Studio\Enterprise IT";

const ENTERPRISE_POLICY_SECRET_FILE_NAME: &str = "config_encryption_secret.yaml";

/// The data directory where the application stores its data.
pub static DATA_DIRECTORY: OnceLock<String> = OnceLock::new();

/// The config directory where the application stores its configuration.
pub static CONFIG_DIRECTORY: OnceLock<String> = OnceLock::new();

/// The user language cached once per runtime process.
static USER_LANGUAGE: OnceLock<String> = OnceLock::new();

/// Returns the config directory.
pub async fn get_config_directory(_token: APIToken) -> String {
    match CONFIG_DIRECTORY.get() {
        Some(config_directory) => config_directory.clone(),
        None => String::from(""),
    }
}

/// Returns the data directory.
pub async fn get_data_directory(_token: APIToken) -> String {
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

    if let Some(region) = segments.next() && region.len() == 2 && region.chars().all(|c| c.is_ascii_alphabetic()) {
        return Some(format!("{}-{}", language, region.to_ascii_uppercase()));
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

pub async fn read_user_language(_token: APIToken) -> String {
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

/// Represents a single enterprise configuration entry with an ID and server URL.
#[derive(Clone, Debug, PartialEq, Eq, Serialize)]
pub struct EnterpriseConfig {
    pub id: String,
    pub server_url: String,
}

#[derive(Clone, Debug, Default, PartialEq, Eq)]
struct EnterpriseSourceData {
    source_name: String,
    configs: Vec<EnterpriseConfig>,
    encryption_secret: String,
}

pub async fn read_enterprise_env_config_id(_token: APIToken) -> String {
    debug!("Trying to read the effective enterprise configuration ID.");
    resolve_effective_enterprise_config_source()
        .configs
        .into_iter()
        .next()
        .map(|config| config.id)
        .unwrap_or_default()
}

pub async fn read_enterprise_env_config_server_url(_token: APIToken) -> String {
    debug!("Trying to read the effective enterprise configuration server URL.");
    resolve_effective_enterprise_config_source()
        .configs
        .into_iter()
        .next()
        .map(|config| config.server_url)
        .unwrap_or_default()
}

pub async fn read_enterprise_env_config_encryption_secret(_token: APIToken) -> String {
    debug!("Trying to read the effective enterprise configuration encryption secret.");
    resolve_effective_enterprise_secret_source().encryption_secret
}

/// Returns all enterprise configurations from the effective source.
pub async fn read_enterprise_configs(_token: APIToken) -> Json<Vec<EnterpriseConfig>> {
    info!("Trying to read the effective enterprise configurations.");
    Json(resolve_effective_enterprise_config_source().configs)
}

fn resolve_effective_enterprise_config_source() -> EnterpriseSourceData {
    select_effective_enterprise_config_source(gather_enterprise_sources())
}

fn resolve_effective_enterprise_secret_source() -> EnterpriseSourceData {
    select_effective_enterprise_secret_source(gather_enterprise_sources())
}

fn select_effective_enterprise_config_source(
    sources: Vec<EnterpriseSourceData>,
) -> EnterpriseSourceData {
    for source in sources {
        if !source.configs.is_empty() {
            info!("Using enterprise configuration source '{}'.", source.source_name);
            return source;
        }

        info!("Enterprise configuration source '{}' did not provide any valid configurations.", source.source_name);
    }

    info!("No enterprise configuration source provided any valid configurations.");
    EnterpriseSourceData::default()
}

fn select_effective_enterprise_secret_source(
    sources: Vec<EnterpriseSourceData>,
) -> EnterpriseSourceData {
    for source in sources {
        if !source.encryption_secret.is_empty() {
            info!("Using enterprise encryption-secret source '{}'.", source.source_name);
            return source;
        }

        info!("Enterprise encryption-secret source '{}' did not provide a usable secret.", source.source_name);
    }

    info!("No enterprise source provided an enterprise encryption secret.");
    EnterpriseSourceData::default()
}

fn gather_enterprise_sources() -> Vec<EnterpriseSourceData> {
    cfg_if::cfg_if! {
        if #[cfg(target_os = "windows")] {
            vec![
                load_registry_enterprise_source(),
                load_policy_file_enterprise_source(),
                load_environment_enterprise_source(),
            ]
        } else if #[cfg(any(target_os = "linux", target_os = "macos"))] {
            vec![
                load_policy_file_enterprise_source(),
                load_environment_enterprise_source(),
            ]
        } else {
            vec![load_environment_enterprise_source()]
        }
    }
}

#[cfg(target_os = "windows")]
fn load_registry_enterprise_source() -> EnterpriseSourceData {
    use windows_registry::*;

    info!(r"Trying to read enterprise configuration metadata from 'HKEY_CURRENT_USER\{}'.", ENTERPRISE_REGISTRY_KEY_PATH);

    let mut values = HashMap::new();
    let key = match CURRENT_USER.open(ENTERPRISE_REGISTRY_KEY_PATH) {
        Ok(key) => key,
        Err(_) => {
            info!(r"Could not read 'HKEY_CURRENT_USER\{}'.", ENTERPRISE_REGISTRY_KEY_PATH);
            return EnterpriseSourceData {
                source_name: String::from("Windows registry"),
                ..EnterpriseSourceData::default()
            };
        }
    };

    for index in 0..ENTERPRISE_CONFIG_SLOT_COUNT {
        insert_registry_value(&mut values, &key, &format!("config_id{index}"));
        insert_registry_value(&mut values, &key, &format!("config_server_url{index}"));
    }

    for key_name in [
        "configs",
        "config_id",
        "config_server_url",
        "config_encryption_secret",
    ] {
        insert_registry_value(&mut values, &key, key_name);
    }

    parse_enterprise_source_values("Windows registry", &values)
}

#[cfg(target_os = "windows")]
fn insert_registry_value(
    values: &mut HashMap<String, String>,
    key: &windows_registry::Key,
    key_name: &str,
) {
    if let Ok(value) = key.get_string(key_name) {
        values.insert(String::from(key_name), value);
    }
}

fn load_policy_file_enterprise_source() -> EnterpriseSourceData {
    let directories = enterprise_policy_directories();
    info!("Trying to read enterprise configuration metadata from policy files in {} director{}.", directories.len(), if directories.len() == 1 { "y" } else { "ies" });

    let values = load_policy_values_from_directories(&directories);
    parse_enterprise_source_values("policy files", &values)
}

fn load_environment_enterprise_source() -> EnterpriseSourceData {
    info!("Trying to read enterprise configuration metadata from environment variables.");
    let mut values = HashMap::new();
    for index in 0..ENTERPRISE_CONFIG_SLOT_COUNT {
        insert_env_value(&mut values, &format!("MINDWORK_AI_STUDIO_ENTERPRISE_CONFIG_ID{index}"), &format!("config_id{index}"));
        insert_env_value(&mut values, &format!("MINDWORK_AI_STUDIO_ENTERPRISE_CONFIG_SERVER_URL{index}"), &format!("config_server_url{index}"));
    }

    insert_env_value(&mut values, "MINDWORK_AI_STUDIO_ENTERPRISE_CONFIGS", "configs");
    insert_env_value(&mut values, "MINDWORK_AI_STUDIO_ENTERPRISE_CONFIG_ID", "config_id");
    insert_env_value(&mut values, "MINDWORK_AI_STUDIO_ENTERPRISE_CONFIG_SERVER_URL", "config_server_url");
    insert_env_value(&mut values, "MINDWORK_AI_STUDIO_ENTERPRISE_CONFIG_ENCRYPTION_SECRET", "config_encryption_secret");

    parse_enterprise_source_values("environment variables", &values)
}

fn insert_env_value(values: &mut HashMap<String, String>, env_name: &str, key_name: &str) {
    if let Ok(value) = env::var(env_name) {
        values.insert(String::from(key_name), value);
    }
}

#[cfg(target_os = "windows")]
fn enterprise_policy_directories() -> Vec<PathBuf> {
    let base = env::var_os("ProgramData")
        .map(PathBuf::from)
        .unwrap_or_else(|| PathBuf::from(r"C:\ProgramData"));
    vec![base.join("MindWorkAI").join("AI-Studio")]
}

#[cfg(target_os = "linux")]
fn enterprise_policy_directories() -> Vec<PathBuf> {
    let xdg_config_dirs = env::var("XDG_CONFIG_DIRS").ok();
    linux_policy_directories_from_xdg(xdg_config_dirs.as_deref())
}

#[cfg(target_os = "macos")]
fn enterprise_policy_directories() -> Vec<PathBuf> {
    vec![PathBuf::from(
        "/Library/Application Support/MindWork/AI Studio",
    )]
}

#[cfg(not(any(target_os = "windows", target_os = "linux", target_os = "macos")))]
fn enterprise_policy_directories() -> Vec<PathBuf> {
    Vec::new()
}

#[cfg(any(target_os = "linux", test))]
fn linux_policy_directories_from_xdg(xdg_config_dirs: Option<&str>) -> Vec<PathBuf> {
    let mut directories = Vec::new();
    if let Some(raw_directories) = xdg_config_dirs {
        for path in raw_directories.split(':') {
            if let Some(path) = normalize_enterprise_value(path) {
                directories.push(PathBuf::from(path).join("mindwork-ai-studio"));
            }
        }
    }

    if directories.is_empty() {
        directories.push(PathBuf::from("/etc/xdg/mindwork-ai-studio"));
    }

    directories
}

fn load_policy_values_from_directories(directories: &[PathBuf]) -> HashMap<String, String> {
    let mut values = HashMap::new();
    for directory in directories {
        info!("Checking enterprise policy directory '{}'.", directory.display());
        for index in 0..ENTERPRISE_CONFIG_SLOT_COUNT {
            let path = directory.join(format!("config{index}.yaml"));
            if let Some(config_values) = read_policy_yaml_mapping(&path) {
                if let Some(id) = config_values.get("id") {
                    insert_first_non_empty_value(&mut values, &format!("config_id{index}"), id);
                }

                if let Some(server_url) = config_values.get("server_url") {
                    insert_first_non_empty_value(&mut values, &format!("config_server_url{index}"), server_url);
                }
            }
        }

        let secret_path = directory.join(ENTERPRISE_POLICY_SECRET_FILE_NAME);
        if let Some(secret_values) = read_policy_yaml_mapping(&secret_path)
            && let Some(secret) = secret_values.get("config_encryption_secret") {
            insert_first_non_empty_value(&mut values, "config_encryption_secret", secret);
        }
    }

    values
}

fn read_policy_yaml_mapping(path: &Path) -> Option<HashMap<String, String>> {
    if !path.exists() {
        return None;
    }

    let content = match fs::read_to_string(path) {
        Ok(content) => content,
        Err(error) => {
            warn!("Could not read enterprise policy file '{}': {}", path.display(), error);
            return None;
        }
    };

    match parse_policy_yaml_mapping(path, &content) {
        Some(values) => Some(values),
        None => {
            warn!("Could not parse enterprise policy file '{}'.", path.display());
            None
        }
    }
}

fn parse_policy_yaml_mapping(path: &Path, content: &str) -> Option<HashMap<String, String>> {
    let mut values = HashMap::new();
    for (line_number, line) in content.lines().enumerate() {
        let trimmed = line.trim();
        if trimmed.is_empty() || trimmed.starts_with('#') {
            continue;
        }

        let (key, raw_value) = match trimmed.split_once(':') {
            Some(parts) => parts,
            None => {
                warn!("Invalid enterprise policy file '{}': line {} does not contain ':'.", path.display(), line_number + 1);
                return None;
            }
        };

        let key = key.trim();
        if key.is_empty() {
            warn!("Invalid enterprise policy file '{}': line {} contains an empty key.", path.display(), line_number + 1);
            return None;
        }

        let value = match parse_policy_yaml_value(raw_value) {
            Some(value) => value,
            None => {
                warn!("Invalid enterprise policy file '{}': line {} contains an unsupported YAML value.", path.display(), line_number + 1);
                return None;
            }
        };

        values.insert(String::from(key), value);
    }

    Some(values)
}

fn parse_policy_yaml_value(raw_value: &str) -> Option<String> {
    let trimmed = raw_value.trim();
    if trimmed.is_empty() {
        return Some(String::new());
    }

    if trimmed.starts_with('"') || trimmed.ends_with('"') {
        if trimmed.len() >= 2 && trimmed.starts_with('"') && trimmed.ends_with('"') {
            return Some(trimmed[1..trimmed.len() - 1].to_string());
        }

        return None;
    }

    if trimmed.starts_with('\'') || trimmed.ends_with('\'') {
        if trimmed.len() >= 2 && trimmed.starts_with('\'') && trimmed.ends_with('\'') {
            return Some(trimmed[1..trimmed.len() - 1].to_string());
        }

        return None;
    }

    Some(String::from(trimmed))
}

fn insert_first_non_empty_value(values: &mut HashMap<String, String>, key: &str, raw_value: &str) {
    if let Some(value) = normalize_enterprise_value(raw_value) {
        values.entry(String::from(key)).or_insert(value);
    }
}

fn parse_enterprise_source_values(
    source_name: &str,
    values: &HashMap<String, String>,
) -> EnterpriseSourceData {
    let mut configs = Vec::new();
    let mut seen_ids = HashSet::new();

    for index in 0..ENTERPRISE_CONFIG_SLOT_COUNT {
        let id_key = format!("config_id{index}");
        let server_url_key = format!("config_server_url{index}");
        add_enterprise_config_pair(
            source_name,
            &format!("indexed slot {index}"),
            values.get(&id_key).map(String::as_str),
            values.get(&server_url_key).map(String::as_str),
            &mut configs,
            &mut seen_ids,
        );
    }

    if let Some(combined) = values
        .get("configs")
        .and_then(|value| normalize_enterprise_value(value))
    {
        add_combined_enterprise_configs(source_name, &combined, &mut configs, &mut seen_ids);
    }

    add_enterprise_config_pair(
        source_name,
        "legacy single configuration",
        values.get("config_id").map(String::as_str),
        values.get("config_server_url").map(String::as_str),
        &mut configs,
        &mut seen_ids,
    );

    let encryption_secret = values
        .get("config_encryption_secret")
        .and_then(|value| normalize_enterprise_value(value))
        .unwrap_or_default();

    EnterpriseSourceData {
        source_name: String::from(source_name),
        configs,
        encryption_secret,
    }
}

fn add_enterprise_config_pair(
    source_name: &str,
    context: &str,
    raw_id: Option<&str>,
    raw_server_url: Option<&str>,
    configs: &mut Vec<EnterpriseConfig>,
    seen_ids: &mut HashSet<String>,
) {
    let id = raw_id.and_then(normalize_enterprise_config_id);
    let server_url = raw_server_url.and_then(normalize_enterprise_value);

    match (id, server_url) {
        (Some(id), Some(server_url)) => {
            if seen_ids.insert(id.clone()) {
                configs.push(EnterpriseConfig { id, server_url });
            } else {
                info!("Ignoring duplicate enterprise configuration '{}' from {} in '{}'.", id, source_name, context);
            }
        }

        (Some(_), None) | (None, Some(_)) => {
            warn!("Ignoring incomplete enterprise configuration from {} in '{}'.", source_name, context);
        }

        (None, None) => {}
    }
}

fn add_combined_enterprise_configs(
    source_name: &str,
    combined: &str,
    configs: &mut Vec<EnterpriseConfig>,
    seen_ids: &mut HashSet<String>,
) {
    for (index, entry) in combined.split(';').enumerate() {
        let trimmed = entry.trim();
        if trimmed.is_empty() {
            continue;
        }

        let Some((raw_id, raw_server_url)) = trimmed.split_once('@') else {
            warn!("Ignoring malformed enterprise configuration entry '{}' from {} in combined legacy format.", trimmed, source_name);
            continue;
        };

        add_enterprise_config_pair(
            source_name,
            &format!("combined legacy entry {}", index + 1),
            Some(raw_id),
            Some(raw_server_url),
            configs,
            seen_ids,
        );
    }
}

fn normalize_enterprise_value(value: &str) -> Option<String> {
    let trimmed = value.trim();
    if trimmed.is_empty() {
        None
    } else {
        Some(String::from(trimmed))
    }
}

fn normalize_enterprise_config_id(value: &str) -> Option<String> {
    normalize_enterprise_value(value).map(|value| value.to_lowercase())
}

#[cfg(test)]
mod tests {
    use super::{
        linux_policy_directories_from_xdg, load_policy_values_from_directories,
        normalize_locale_tag, parse_enterprise_source_values,
        select_effective_enterprise_config_source, select_effective_enterprise_secret_source,
        EnterpriseConfig, EnterpriseSourceData,
    };
    use std::collections::HashMap;
    use std::fs;
    use std::path::PathBuf;
    use tempfile::tempdir;

    const TEST_ID_A: &str = "9072B77D-CA81-40DA-BE6A-861DA525EF7B";
    const TEST_ID_B: &str = "a1b2c3d4-e5f6-7890-abcd-ef1234567890";
    const TEST_ID_C: &str = "11111111-2222-3333-4444-555555555555";

    #[test]
    fn normalize_locale_tag_supports_common_linux_formats() {
        assert_eq!(
            normalize_locale_tag("de_DE.UTF-8"),
            Some(String::from("de-DE"))
        );
        assert_eq!(
            normalize_locale_tag("de_DE@euro"),
            Some(String::from("de-DE"))
        );
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

    #[test]
    fn parse_enterprise_source_values_prefers_indexed_then_combined_then_legacy() {
        let mut values = HashMap::new();
        values.insert(String::from("config_id0"), String::from(TEST_ID_A));
        values.insert(
            String::from("config_server_url0"),
            String::from(" https://indexed.example.org "),
        );
        values.insert(
            String::from("configs"),
            format!(
                "{TEST_ID_A}@https://duplicate.example.org;{TEST_ID_B}@https://combined.example.org"
            ),
        );
        values.insert(String::from("config_id"), String::from(TEST_ID_C));
        values.insert(
            String::from("config_server_url"),
            String::from("https://legacy.example.org"),
        );
        values.insert(
            String::from("config_encryption_secret"),
            String::from(" secret "),
        );

        let source = parse_enterprise_source_values("test", &values);

        assert_eq!(
            source.configs,
            vec![
                EnterpriseConfig {
                    id: String::from("9072b77d-ca81-40da-be6a-861da525ef7b"),
                    server_url: String::from("https://indexed.example.org"),
                },
                EnterpriseConfig {
                    id: String::from(TEST_ID_B),
                    server_url: String::from("https://combined.example.org"),
                },
                EnterpriseConfig {
                    id: String::from(TEST_ID_C),
                    server_url: String::from("https://legacy.example.org"),
                },
            ]
        );
        assert_eq!(source.encryption_secret, "secret");
    }

    #[test]
    fn parse_enterprise_source_values_supports_gaps_between_indexed_slots() {
        let mut values = HashMap::new();
        values.insert(String::from("config_id0"), String::from(TEST_ID_A));
        values.insert(
            String::from("config_server_url0"),
            String::from("https://slot0.example.org"),
        );
        values.insert(String::from("config_id4"), String::from(TEST_ID_B));
        values.insert(
            String::from("config_server_url4"),
            String::from("https://slot4.example.org"),
        );

        let source = parse_enterprise_source_values("test", &values);

        assert_eq!(
            source.configs,
            vec![
                EnterpriseConfig {
                    id: String::from("9072b77d-ca81-40da-be6a-861da525ef7b"),
                    server_url: String::from("https://slot0.example.org"),
                },
                EnterpriseConfig {
                    id: String::from(TEST_ID_B),
                    server_url: String::from("https://slot4.example.org"),
                },
            ]
        );
    }

    #[test]
    fn select_effective_enterprise_config_source_uses_first_source_with_configs_only() {
        let selected = select_effective_enterprise_config_source(vec![
            EnterpriseSourceData {
                source_name: String::from("registry"),
                configs: vec![EnterpriseConfig {
                    id: TEST_ID_A.to_lowercase(),
                    server_url: String::from("https://registry.example.org"),
                }],
                encryption_secret: String::new(),
            },
            EnterpriseSourceData {
                source_name: String::from("environment"),
                configs: vec![EnterpriseConfig {
                    id: String::from(TEST_ID_B),
                    server_url: String::from("https://env.example.org"),
                }],
                encryption_secret: String::from("ENV-SECRET"),
            },
        ]);

        assert_eq!(selected.source_name, "registry");
        assert_eq!(selected.encryption_secret, "");
        assert_eq!(selected.configs.len(), 1);
    }

    #[test]
    fn select_effective_enterprise_secret_source_allows_secret_only_source() {
        let selected = select_effective_enterprise_secret_source(vec![
            EnterpriseSourceData {
                source_name: String::from("policy files"),
                configs: Vec::new(),
                encryption_secret: String::from("POLICY-SECRET"),
            },
            EnterpriseSourceData {
                source_name: String::from("environment"),
                configs: vec![EnterpriseConfig {
                    id: String::from(TEST_ID_B),
                    server_url: String::from("https://env.example.org"),
                }],
                encryption_secret: String::new(),
            },
        ]);

        assert_eq!(selected.source_name, "policy files");
        assert_eq!(selected.encryption_secret, "POLICY-SECRET");
        assert!(selected.configs.is_empty());
    }

    #[test]
    fn select_effective_enterprise_secret_source_falls_back_independently_from_configs() {
        let selected = select_effective_enterprise_secret_source(vec![
            EnterpriseSourceData {
                source_name: String::from("registry"),
                configs: vec![EnterpriseConfig {
                    id: TEST_ID_A.to_lowercase(),
                    server_url: String::from("https://registry.example.org"),
                }],
                encryption_secret: String::new(),
            },
            EnterpriseSourceData {
                source_name: String::from("environment"),
                configs: Vec::new(),
                encryption_secret: String::from("ENV-SECRET"),
            },
        ]);

        assert_eq!(selected.source_name, "environment");
        assert_eq!(selected.encryption_secret, "ENV-SECRET");
        assert!(selected.configs.is_empty());
    }

    #[test]
    fn select_effective_enterprise_secret_source_ignores_empty_secrets() {
        let selected = select_effective_enterprise_secret_source(vec![
            EnterpriseSourceData {
                source_name: String::from("policy files"),
                configs: Vec::new(),
                encryption_secret: String::new(),
            },
            EnterpriseSourceData {
                source_name: String::from("environment"),
                configs: Vec::new(),
                encryption_secret: String::from("VALID-SECRET"),
            },
        ]);

        assert_eq!(selected.source_name, "environment");
        assert_eq!(selected.encryption_secret, "VALID-SECRET");
    }

    #[test]
    fn parse_enterprise_source_values_supports_secret_without_configs() {
        let mut values = HashMap::new();
        values.insert(
            String::from("config_encryption_secret"),
            String::from(" SECRET-ONLY "),
        );

        let source = parse_enterprise_source_values("environment variables", &values);

        assert!(source.configs.is_empty());
        assert_eq!(source.encryption_secret, "SECRET-ONLY");
    }

    #[test]
    fn linux_policy_directories_from_xdg_preserves_order_and_falls_back() {
        assert_eq!(
            linux_policy_directories_from_xdg(Some(" /opt/company:/etc/xdg ")),
            vec![
                PathBuf::from("/opt/company/mindwork-ai-studio"),
                PathBuf::from("/etc/xdg/mindwork-ai-studio"),
            ]
        );

        assert_eq!(
            linux_policy_directories_from_xdg(Some(" : ")),
            vec![PathBuf::from("/etc/xdg/mindwork-ai-studio")]
        );
        assert_eq!(
            linux_policy_directories_from_xdg(None),
            vec![PathBuf::from("/etc/xdg/mindwork-ai-studio")]
        );
    }

    #[test]
    fn load_policy_values_from_directories_uses_first_directory_wins() {
        let directory_a = tempdir().unwrap();
        let directory_b = tempdir().unwrap();

        fs::write(
            directory_a.path().join("config0.yaml"),
            "id: \"9072b77d-ca81-40da-be6a-861da525ef7b\"\nserver_url: \"https://org.example.org\"",
        )
        .unwrap();
        fs::write(
            directory_a.path().join("config_encryption_secret.yaml"),
            "config_encryption_secret: \"SECRET-A\"",
        )
        .unwrap();

        fs::write(
            directory_b.path().join("config0.yaml"),
            "id: \"aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa\"\nserver_url: \"https://ignored.example.org\"",
        )
        .unwrap();
        fs::write(
            directory_b.path().join("config1.yaml"),
            "id: \"bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb\"\nserver_url: \"https://dept.example.org\"",
        )
        .unwrap();
        fs::write(
            directory_b.path().join("config_encryption_secret.yaml"),
            "config_encryption_secret: \"SECRET-B\"",
        )
        .unwrap();

        let values = load_policy_values_from_directories(&[
            directory_a.path().to_path_buf(),
            directory_b.path().to_path_buf(),
        ]);

        assert_eq!(
            values.get("config_id0").map(String::as_str),
            Some("9072b77d-ca81-40da-be6a-861da525ef7b")
        );
        assert_eq!(
            values.get("config_server_url0").map(String::as_str),
            Some("https://org.example.org")
        );
        assert_eq!(
            values.get("config_id1").map(String::as_str),
            Some("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb")
        );
        assert_eq!(
            values.get("config_encryption_secret").map(String::as_str),
            Some("SECRET-A")
        );
    }

    #[test]
    fn load_policy_values_from_directories_supports_gaps_between_policy_slots() {
        let directory = tempdir().unwrap();

        fs::write(
            directory.path().join("config0.yaml"),
            "id: \"9072b77d-ca81-40da-be6a-861da525ef7b\"\nserver_url: \"https://slot0.example.org\"",
        )
        .unwrap();
        fs::write(
            directory.path().join("config4.yaml"),
            "id: \"a1b2c3d4-e5f6-7890-abcd-ef1234567890\"\nserver_url: \"https://slot4.example.org\"",
        )
        .unwrap();

        let values = load_policy_values_from_directories(&[directory.path().to_path_buf()]);
        let source = parse_enterprise_source_values("policy files", &values);

        assert_eq!(
            source.configs,
            vec![
                EnterpriseConfig {
                    id: String::from("9072b77d-ca81-40da-be6a-861da525ef7b"),
                    server_url: String::from("https://slot0.example.org"),
                },
                EnterpriseConfig {
                    id: String::from(TEST_ID_B),
                    server_url: String::from("https://slot4.example.org"),
                },
            ]
        );
    }

    #[test]
    fn load_policy_values_from_directories_supports_secret_only_policy_files() {
        let directory = tempdir().unwrap();

        fs::write(
            directory.path().join("config_encryption_secret.yaml"),
            "config_encryption_secret: \"POLICY-SECRET\"",
        )
        .unwrap();

        let values = load_policy_values_from_directories(&[directory.path().to_path_buf()]);
        let source = parse_enterprise_source_values("policy files", &values);

        assert!(source.configs.is_empty());
        assert_eq!(source.encryption_secret, "POLICY-SECRET");
    }

    #[test]
    fn load_policy_values_from_directories_ignores_invalid_and_incomplete_files() {
        let directory = tempdir().unwrap();

        fs::write(directory.path().join("config0.yaml"), "id [broken").unwrap();
        fs::write(
            directory.path().join("config1.yaml"),
            "id: \"9072b77d-ca81-40da-be6a-861da525ef7b\"",
        )
        .unwrap();

        let values = load_policy_values_from_directories(&[directory.path().to_path_buf()]);
        let source = parse_enterprise_source_values("policy files", &values);

        assert!(source.configs.is_empty());
    }
}