use crate::api_token::APIToken;
use axum::Json;
use log::{debug, error, info, warn};
use serde::Serialize;
use std::collections::{HashMap, HashSet};
use std::env;
use std::fs;
use std::path::{Path, PathBuf};
use std::sync::OnceLock;
use sys_locale::get_locale;

const DEFAULT_LANGUAGE: &str = "en-US";

const ENTERPRISE_CONFIG_SLOT_MAX: u32 = 99_999;
const ENTERPRISE_CONFIG_SLOT_WIDTH: usize = 5;

const ENTERPRISE_CONFIG_ID_KEY_PREFIX: &str = "config_id";
const ENTERPRISE_CONFIG_SERVER_URL_KEY_PREFIX: &str = "config_server_url";

#[cfg(target_os = "windows")]
const ENTERPRISE_REGISTRY_KEY_PATH: &str = r"Software\github\MindWork AI Studio\Enterprise IT";

const ENTERPRISE_POLICY_SECRET_FILE_NAME: &str = "config_encryption_secret.yaml";
const EXTERNAL_HTTP_CUSTOM_ROOT_CERTIFICATE_POLICY_FILE_NAME: &str = "external_http_custom_root_certificates.yaml";

pub const DOTNET_ENV_CUSTOM_ROOT_CERTIFICATE_POLICY_CONFIGURED: &str = "AI_STUDIO_EXTERNAL_HTTP_CUSTOM_ROOT_CERTIFICATES_POLICY_CONFIGURED";
pub const DOTNET_ENV_CUSTOM_ROOT_CERTIFICATES_ENABLED: &str = "AI_STUDIO_EXTERNAL_HTTP_CUSTOM_ROOT_CERTIFICATES_ENABLED";
pub const DOTNET_ENV_CUSTOM_ROOT_CERTIFICATE_BUNDLE_PATH: &str = "AI_STUDIO_EXTERNAL_HTTP_CUSTOM_ROOT_CERTIFICATE_BUNDLE_PATH";
pub const DOTNET_ENV_CUSTOM_ROOT_CERTIFICATE_ALLOWED_HOSTS: &str = "AI_STUDIO_EXTERNAL_HTTP_CUSTOM_ROOT_CERTIFICATE_ALLOWED_HOSTS";

#[cfg(any(target_os = "linux", test))]
const FLATPAK_ENTERPRISE_POLICY_DIRECTORY: &str = "/app/etc/MindWorkAI";

const ENTERPRISE_ENV_CONFIG_ID_PREFIX: &str = "MINDWORK_AI_STUDIO_ENTERPRISE_CONFIG_ID";
const ENTERPRISE_ENV_CONFIG_SERVER_URL_PREFIX: &str = "MINDWORK_AI_STUDIO_ENTERPRISE_CONFIG_SERVER_URL";
const ENTERPRISE_ENV_CONFIGS: &str = "MINDWORK_AI_STUDIO_ENTERPRISE_CONFIGS";
const ENTERPRISE_ENV_CONFIG_ENCRYPTION_SECRET: &str = "MINDWORK_AI_STUDIO_ENTERPRISE_CONFIG_ENCRYPTION_SECRET";

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

/// Returns the current user's username.
pub async fn read_user_name(_token: APIToken) -> String {
    whoami::username().unwrap_or_else(|e| {
        error!("Failed to read the current OS username: {e}.");
        String::new()
    })
}

#[derive(Clone, Debug, PartialEq, Eq, Serialize)]
pub struct RuntimeInfo {
    pub working_directory: String,
    pub executable_path: String,
    pub linux_package_type: String,
}

pub async fn get_runtime_info(_token: APIToken) -> Json<RuntimeInfo> {
    Json(RuntimeInfo {
        working_directory: env::current_dir()
            .map(|path| path.to_string_lossy().into_owned())
            .unwrap_or_default(),
        executable_path: env::current_exe()
            .map(|path| path.to_string_lossy().into_owned())
            .unwrap_or_default(),
        linux_package_type: detect_linux_package_type().to_string(),
    })
}

#[cfg(target_os = "linux")]
fn detect_linux_package_type() -> &'static str {
    if is_flatpak() {
        "flatpak"
    } else if is_appimage() {
        "appimage"
    } else {
        "unknown"
    }
}

#[cfg(not(target_os = "linux"))]
fn detect_linux_package_type() -> &'static str {
    "not_applicable"
}

#[cfg(target_os = "linux")]
pub(crate) fn is_flatpak() -> bool {
    env_var_has_value("FLATPAK_ID")
        || Path::new("/.flatpak-info").is_file()
        || env::var("container")
            .is_ok_and(|value| value.trim().eq_ignore_ascii_case("flatpak"))
}

#[cfg(not(target_os = "linux"))]
pub(crate) fn is_flatpak() -> bool {
    false
}

#[cfg(target_os = "linux")]
fn is_appimage() -> bool {
    env_var_has_value("APPIMAGE") || env_var_has_value("APPDIR")
}

#[cfg(target_os = "linux")]
fn env_var_has_value(key: &str) -> bool {
    env::var(key).is_ok_and(|value| !value.trim().is_empty())
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
    pub source: String,
    pub source_detail: String,
    pub slot: String,
}

#[derive(Clone, Debug, Default, PartialEq, Eq)]
pub struct ExternalHttpCustomRootCertificatePolicy {
    pub is_configured: bool,
    pub enabled: bool,
    pub bundle_path: String,
    pub allowed_hosts: String,
    pub source_detail: String,
}

#[derive(Clone, Debug, PartialEq, Eq)]
struct EnterpriseSourceValue {
    value: String,
    source_detail: String,
}

impl EnterpriseSourceValue {
    fn new(value: String, source_detail: String) -> Self {
        Self {
            value,
            source_detail,
        }
    }
}

trait EnterpriseSourceValueAccess {
    fn value(&self) -> &str;
    fn source_detail(&self) -> &str;
}

impl EnterpriseSourceValueAccess for EnterpriseSourceValue {
    fn value(&self) -> &str {
        &self.value
    }

    fn source_detail(&self) -> &str {
        &self.source_detail
    }
}

impl EnterpriseSourceValueAccess for String {
    fn value(&self) -> &str {
        self
    }

    fn source_detail(&self) -> &str {
        ""
    }
}

type EnterpriseSourceValues = HashMap<String, EnterpriseSourceValue>;

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

pub fn resolve_external_http_custom_root_certificate_policy() -> ExternalHttpCustomRootCertificatePolicy {
    load_external_http_custom_root_certificate_policy_from_directories(&enterprise_policy_directories())
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

    let mut values = EnterpriseSourceValues::new();
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

    match key.values() {
        Ok(registry_values) => {
            for (key_name, value) in registry_values {
                let Some(source_key_name) = enterprise_registry_value_key_name(&key_name) else {
                    continue;
                };

                match String::try_from(value) {
                    Ok(value) => {
                        values.insert(source_key_name, EnterpriseSourceValue::new(value, String::new()));
                    },

                    Err(error) => {
                        warn!(r"Could not read enterprise registry value 'HKEY_CURRENT_USER\{}\{}' as string: {}.", ENTERPRISE_REGISTRY_KEY_PATH, key_name, error);
                    },
                }
            }
        },

        Err(error) => {
            warn!(r"Could not enumerate enterprise registry values from 'HKEY_CURRENT_USER\{}': {}.", ENTERPRISE_REGISTRY_KEY_PATH, error);
        },
    }

    parse_enterprise_source_values("Windows registry", &values)
}

#[cfg(target_os = "windows")]
fn enterprise_registry_value_key_name(key_name: &str) -> Option<String> {
    if is_legacy_enterprise_source_key(key_name) {
        return Some(String::from(key_name));
    }

    enterprise_indexed_source_key_name(key_name)
}

fn load_policy_file_enterprise_source() -> EnterpriseSourceData {
    let directories = enterprise_policy_directories();
    info!("Trying to read enterprise configuration metadata from policy files in {} director{}.", directories.len(), if directories.len() == 1 { "y" } else { "ies" });

    let values = load_policy_values_from_directories(&directories);
    parse_enterprise_source_values("policy files", &values)
}

fn load_environment_enterprise_source() -> EnterpriseSourceData {
    info!("Trying to read enterprise configuration metadata from environment variables.");
    let mut values = EnterpriseSourceValues::new();
    for (env_name, value) in env::vars() {
        if let Some(source_key_name) = enterprise_environment_key_name(&env_name) {
            let source_detail = enterprise_environment_source_detail(&source_key_name, &env_name);
            values.insert(source_key_name, EnterpriseSourceValue::new(value, source_detail));
        }
    }

    parse_enterprise_source_values("environment variables", &values)
}

fn enterprise_environment_source_detail(source_key_name: &str, env_name: &str) -> String {
    if source_key_name == "config_id"
        || enterprise_source_key_suffix(source_key_name, ENTERPRISE_CONFIG_ID_KEY_PREFIX).is_some() {
        String::from(env_name)
    } else {
        String::new()
    }
}

fn enterprise_environment_key_name(env_name: &str) -> Option<String> {
    if enterprise_env_key_equals(env_name, ENTERPRISE_ENV_CONFIGS) {
        return Some(String::from("configs"));
    }

    if enterprise_env_key_equals(env_name, ENTERPRISE_ENV_CONFIG_ID_PREFIX) {
        return Some(String::from("config_id"));
    }

    if enterprise_env_key_equals(env_name, ENTERPRISE_ENV_CONFIG_SERVER_URL_PREFIX) {
        return Some(String::from("config_server_url"));
    }

    if enterprise_env_key_equals(env_name, ENTERPRISE_ENV_CONFIG_ENCRYPTION_SECRET) {
        return Some(String::from("config_encryption_secret"));
    }

    if let Some(suffix) = enterprise_env_key_suffix(env_name, ENTERPRISE_ENV_CONFIG_ID_PREFIX) {
        return Some(format!("config_id{suffix}"));
    }

    if let Some(suffix) = enterprise_env_key_suffix(env_name, ENTERPRISE_ENV_CONFIG_SERVER_URL_PREFIX) {
        return Some(format!("config_server_url{suffix}"));
    }

    None
}

#[cfg(target_os = "windows")]
fn enterprise_env_key_equals(env_name: &str, expected: &str) -> bool {
    env_name.eq_ignore_ascii_case(expected)
}

#[cfg(not(target_os = "windows"))]
fn enterprise_env_key_equals(env_name: &str, expected: &str) -> bool {
    env_name == expected
}

#[cfg(target_os = "windows")]
fn enterprise_env_key_suffix<'a>(env_name: &'a str, prefix: &str) -> Option<&'a str> {
    if env_name.len() < prefix.len() {
        return None;
    }

    let (raw_prefix, suffix) = env_name.split_at(prefix.len());
    if raw_prefix.eq_ignore_ascii_case(prefix) {
        normalize_enterprise_slot_suffix(suffix)
    } else {
        None
    }
}

#[cfg(not(target_os = "windows"))]
fn enterprise_env_key_suffix<'a>(env_name: &'a str, prefix: &str) -> Option<&'a str> {
    env_name
        .strip_prefix(prefix)
        .and_then(normalize_enterprise_slot_suffix)
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
    linux_policy_directories_from_xdg(xdg_config_dirs.as_deref(), is_flatpak())
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
fn linux_policy_directories_from_xdg(xdg_config_dirs: Option<&str>, include_flatpak_provisioning: bool) -> Vec<PathBuf> {
    let mut directories = Vec::new();
    if include_flatpak_provisioning {
        directories.push(PathBuf::from(FLATPAK_ENTERPRISE_POLICY_DIRECTORY));
    }

    let mut has_linux_policy_directory = false;
    if let Some(raw_directories) = xdg_config_dirs {
        for path in raw_directories.split(':') {
            if let Some(path) = normalize_enterprise_value(path) {
                directories.push(PathBuf::from(path).join("mindwork-ai-studio"));
                has_linux_policy_directory = true;
            }
        }
    }

    if !has_linux_policy_directory {
        directories.push(PathBuf::from("/etc/xdg/mindwork-ai-studio"));
    }

    directories
}

fn load_policy_values_from_directories(directories: &[PathBuf]) -> EnterpriseSourceValues {
    let mut values = EnterpriseSourceValues::new();
    for directory in directories {
        info!("Checking enterprise policy directory '{}'.", directory.display());
        let entries = match fs::read_dir(directory) {
            Ok(entries) => entries,
            Err(error) => {
                info!("Could not enumerate enterprise policy directory '{}': {}.", directory.display(), error);
                continue;
            },
        };

        for entry in entries {
            let entry = match entry {
                Ok(entry) => entry,
                Err(error) => {
                    warn!("Could not read an entry from enterprise policy directory '{}': {}.", directory.display(), error);
                    continue;
                },
            };

            let file_name = entry.file_name();
            let Some(file_name) = file_name.to_str() else {
                continue;
            };

            let Some(suffix) = enterprise_policy_file_slot_suffix(file_name) else {
                continue;
            };

            let path = entry.path();
            if let Some(config_values) = read_policy_yaml_mapping(&path) {
                let source_detail = path
                    .canonicalize()
                    .unwrap_or_else(|_| path.clone())
                    .to_string_lossy()
                    .into_owned();
                if let Some(id) = config_values.get("id") {
                    insert_first_non_empty_value(&mut values, &format!("config_id{suffix}"), id, &source_detail);
                }

                if let Some(server_url) = config_values.get("server_url") {
                    insert_first_non_empty_value(&mut values, &format!("config_server_url{suffix}"), server_url, &source_detail);
                }
            }
        }

        let secret_path = directory.join(ENTERPRISE_POLICY_SECRET_FILE_NAME);
        if let Some(secret_values) = read_policy_yaml_mapping(&secret_path)
            && let Some(secret) = secret_values.get("config_encryption_secret") {
            insert_first_non_empty_value(&mut values, "config_encryption_secret", secret, "");
        }
    }

    values
}

fn load_external_http_custom_root_certificate_policy_from_directories(directories: &[PathBuf]) -> ExternalHttpCustomRootCertificatePolicy {
    for directory in directories {
        let path = directory.join(EXTERNAL_HTTP_CUSTOM_ROOT_CERTIFICATE_POLICY_FILE_NAME);
        let Some(values) = read_policy_yaml_mapping(&path) else {
            continue;
        };

        if let Some(policy) = parse_external_http_custom_root_certificate_policy(&path, &values) {
            info!("Using external HTTP custom root certificate policy from '{}'.", policy.source_detail);
            return policy;
        }
    }

    ExternalHttpCustomRootCertificatePolicy::default()
}

fn parse_external_http_custom_root_certificate_policy(path: &Path, values: &HashMap<String, String>) -> Option<ExternalHttpCustomRootCertificatePolicy> {
    let Some(raw_enabled) = values.get("enabled") else {
        warn!("Ignoring external HTTP custom root certificate policy '{}': missing 'enabled'.", path.display());
        return None;
    };

    let Some(enabled) = parse_policy_boolean_value(raw_enabled) else {
        warn!("Ignoring external HTTP custom root certificate policy '{}': invalid 'enabled' value.", path.display());
        return None;
    };

    let source_detail = path
        .canonicalize()
        .unwrap_or_else(|_| path.to_path_buf())
        .to_string_lossy()
        .into_owned();

    Some(ExternalHttpCustomRootCertificatePolicy {
        is_configured: true,
        enabled,
        bundle_path: values
            .get("bundle_path")
            .and_then(|value| normalize_enterprise_value(value))
            .unwrap_or_default(),
        allowed_hosts: values
            .get("allowed_hosts")
            .and_then(|value| normalize_enterprise_value(value))
            .unwrap_or_default(),
        source_detail,
    })
}

fn enterprise_policy_file_slot_suffix(file_name: &str) -> Option<&str> {
    let suffix = file_name
        .strip_prefix("config")?
        .strip_suffix(".yaml")?;

    normalize_enterprise_slot_suffix(suffix)
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

fn parse_policy_boolean_value(raw_value: &str) -> Option<bool> {
    let normalized = raw_value.trim();
    if normalized.eq_ignore_ascii_case("true")
        || normalized == "1"
        || normalized.eq_ignore_ascii_case("yes")
        || normalized.eq_ignore_ascii_case("on") {
        return Some(true);
    }

    if normalized.eq_ignore_ascii_case("false")
        || normalized == "0"
        || normalized.eq_ignore_ascii_case("no")
        || normalized.eq_ignore_ascii_case("off") {
        return Some(false);
    }

    None
}

fn insert_first_non_empty_value(values: &mut EnterpriseSourceValues, key: &str, raw_value: &str, source_detail: &str) {
    if let Some(value) = normalize_enterprise_value(raw_value) {
        values
            .entry(String::from(key))
            .or_insert_with(|| EnterpriseSourceValue::new(value, String::from(source_detail)));
    }
}

#[cfg(target_os = "windows")]
fn is_legacy_enterprise_source_key(key_name: &str) -> bool {
    matches!(
        key_name,
        "configs" | "config_id" | "config_server_url" | "config_encryption_secret"
    )
}

#[cfg(target_os = "windows")]
fn enterprise_indexed_source_key_name(key_name: &str) -> Option<String> {
    if let Some(suffix) = enterprise_source_key_suffix(key_name, ENTERPRISE_CONFIG_ID_KEY_PREFIX) {
        return Some(format!("config_id{suffix}"));
    }

    if let Some(suffix) = enterprise_source_key_suffix(key_name, ENTERPRISE_CONFIG_SERVER_URL_KEY_PREFIX) {
        return Some(format!("config_server_url{suffix}"));
    }

    None
}

fn enterprise_source_key_suffix<'a>(key_name: &'a str, prefix: &str) -> Option<&'a str> {
    key_name
        .strip_prefix(prefix)
        .and_then(normalize_enterprise_slot_suffix)
}

fn normalize_enterprise_slot_suffix(raw_suffix: &str) -> Option<&str> {
    let suffix = raw_suffix.strip_prefix('_').unwrap_or(raw_suffix);
    if is_enterprise_slot_suffix(suffix) {
        Some(suffix)
    } else {
        None
    }
}

fn is_enterprise_slot_suffix(suffix: &str) -> bool {
    !suffix.is_empty()
        && suffix.len() <= ENTERPRISE_CONFIG_SLOT_WIDTH
        && suffix.chars().all(|c| c.is_ascii_digit())
        && suffix.parse::<u32>().is_ok_and(|index| index <= ENTERPRISE_CONFIG_SLOT_MAX)
}

fn collect_enterprise_config_slots<T: EnterpriseSourceValueAccess>(values: &HashMap<String, T>) -> Vec<String> {
    let mut slots = HashSet::new();
    for key_name in values.keys() {
        if let Some(suffix) = enterprise_source_key_suffix(key_name, ENTERPRISE_CONFIG_ID_KEY_PREFIX)
            && is_enterprise_slot_suffix(suffix) {
            slots.insert(String::from(suffix));
            continue;
        }

        if let Some(suffix) = enterprise_source_key_suffix(key_name, ENTERPRISE_CONFIG_SERVER_URL_KEY_PREFIX)
            && is_enterprise_slot_suffix(suffix) {
            slots.insert(String::from(suffix));
        }
    }

    let mut slots: Vec<String> = slots.into_iter().collect();
    slots.sort_by(|left, right| {
        let left_index = left.parse::<u32>().unwrap_or(ENTERPRISE_CONFIG_SLOT_MAX);
        let right_index = right.parse::<u32>().unwrap_or(ENTERPRISE_CONFIG_SLOT_MAX);

        left_index
            .cmp(&right_index)
            .then_with(|| enterprise_slot_width_rank(left).cmp(&enterprise_slot_width_rank(right)))
            .then_with(|| left.len().cmp(&right.len()))
            .then_with(|| left.cmp(right))
    });
    slots
}

fn enterprise_slot_width_rank(suffix: &str) -> u8 {
    if suffix.len() == ENTERPRISE_CONFIG_SLOT_WIDTH {
        0
    } else {
        1
    }
}

fn indexed_enterprise_source_value<'a, T: EnterpriseSourceValueAccess>(
    values: &'a HashMap<String, T>,
    prefix: &str,
    suffix: &str,
) -> Option<&'a T> {
    let separated_key = format!("{prefix}_{suffix}");
    values
        .get(&separated_key)
        .or_else(|| values.get(&format!("{prefix}{suffix}")))
}

fn parse_enterprise_source_values<T: EnterpriseSourceValueAccess>(
    source_name: &str,
    values: &HashMap<String, T>,
) -> EnterpriseSourceData {
    let mut configs = Vec::new();
    let mut seen_ids = HashSet::new();

    for suffix in collect_enterprise_config_slots(values) {
        add_enterprise_config_pair(
            source_name,
            &format!("indexed slot {suffix}"),
            indexed_enterprise_source_value(values, ENTERPRISE_CONFIG_ID_KEY_PREFIX, &suffix),
            indexed_enterprise_source_value(values, ENTERPRISE_CONFIG_SERVER_URL_KEY_PREFIX, &suffix),
            &mut configs,
            &mut seen_ids,
        );
    }

    if let Some(combined) = values
        .get("configs")
        .and_then(|value| normalize_enterprise_value(value.value()))
    {
        add_combined_enterprise_configs(source_name, &combined, &mut configs, &mut seen_ids);
    }

    add_enterprise_config_pair(
        source_name,
        "legacy single configuration",
        values.get("config_id"),
        values.get("config_server_url"),
        &mut configs,
        &mut seen_ids,
    );

    let encryption_secret = values
        .get("config_encryption_secret")
        .and_then(|value| normalize_enterprise_value(value.value()))
        .unwrap_or_default();

    EnterpriseSourceData {
        source_name: String::from(source_name),
        configs,
        encryption_secret,
    }
}

fn add_enterprise_config_pair(
    source_name: &str,
    slot: &str,
    raw_id: Option<&impl EnterpriseSourceValueAccess>,
    raw_server_url: Option<&impl EnterpriseSourceValueAccess>,
    configs: &mut Vec<EnterpriseConfig>,
    seen_ids: &mut HashSet<String>,
) {
    let id = raw_id.and_then(|value| normalize_enterprise_config_id(value.value()));
    let server_url = raw_server_url.and_then(|value| normalize_enterprise_value(value.value()));

    match (id, server_url) {
        (Some(id), Some(server_url)) => {
            if seen_ids.insert(id.clone()) {
                configs.push(EnterpriseConfig {
                    id,
                    server_url,
                    source: String::from(source_name),
                    source_detail: raw_id.map(|value| String::from(value.source_detail())).unwrap_or_default(),
                    slot: String::from(slot),
                });
            } else {
                info!("Ignoring duplicate enterprise configuration '{}' from {} in '{}'.", id, source_name, slot);
            }
        }

        (Some(_), None) | (None, Some(_)) => {
            warn!("Ignoring incomplete enterprise configuration from {} in '{}'.", source_name, slot);
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

        let id = EnterpriseSourceValue::new(String::from(raw_id), String::new());
        let server_url = EnterpriseSourceValue::new(String::from(raw_server_url), String::new());
        add_enterprise_config_pair(
            source_name,
            &format!("combined legacy entry {}", index + 1),
            Some(&id),
            Some(&server_url),
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
        enterprise_environment_key_name, enterprise_policy_file_slot_suffix,
        load_external_http_custom_root_certificate_policy_from_directories,
        linux_policy_directories_from_xdg, load_policy_values_from_directories,
        normalize_locale_tag, parse_enterprise_source_values,
        select_effective_enterprise_config_source, select_effective_enterprise_secret_source,
        EnterpriseConfig, EnterpriseSourceData, EnterpriseSourceValue, EnterpriseSourceValues,
        ExternalHttpCustomRootCertificatePolicy,
    };
    use std::collections::HashMap;
    use std::fs;
    use std::path::PathBuf;
    use tempfile::tempdir;

    const TEST_ID_A: &str = "9072B77D-CA81-40DA-BE6A-861DA525EF7B";
    const TEST_ID_B: &str = "a1b2c3d4-e5f6-7890-abcd-ef1234567890";
    const TEST_ID_C: &str = "11111111-2222-3333-4444-555555555555";

    fn enterprise_config(
        id: &str,
        server_url: &str,
        source: &str,
        source_detail: &str,
        slot: &str,
    ) -> EnterpriseConfig {
        EnterpriseConfig {
            id: String::from(id),
            server_url: String::from(server_url),
            source: String::from(source),
            source_detail: String::from(source_detail),
            slot: String::from(slot),
        }
    }

    fn policy_path(path: PathBuf) -> String {
        path
            .canonicalize()
            .unwrap_or(path)
            .to_string_lossy()
            .into_owned()
    }

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
                enterprise_config("9072b77d-ca81-40da-be6a-861da525ef7b", "https://indexed.example.org", "test", "", "indexed slot 0"),
                enterprise_config(TEST_ID_B, "https://combined.example.org", "test", "", "combined legacy entry 2"),
                enterprise_config(TEST_ID_C, "https://legacy.example.org", "test", "", "legacy single configuration"),
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
                enterprise_config("9072b77d-ca81-40da-be6a-861da525ef7b", "https://slot0.example.org", "test", "", "indexed slot 0"),
                enterprise_config(TEST_ID_B, "https://slot4.example.org", "test", "", "indexed slot 4"),
            ]
        );
    }

    #[test]
    fn parse_enterprise_source_values_supports_padded_and_high_indexed_slots() {
        let mut values = HashMap::new();
        values.insert(String::from("config_id_00000"), String::from(TEST_ID_A));
        values.insert(
            String::from("config_server_url_00000"),
            String::from("https://slot0.example.org"),
        );
        values.insert(String::from("config_id_10503"), String::from(TEST_ID_B));
        values.insert(
            String::from("config_server_url_10503"),
            String::from("https://slot10503.example.org"),
        );

        let source = parse_enterprise_source_values("test", &values);

        assert_eq!(
            source.configs,
            vec![
                enterprise_config("9072b77d-ca81-40da-be6a-861da525ef7b", "https://slot0.example.org", "test", "", "indexed slot 00000"),
                enterprise_config(TEST_ID_B, "https://slot10503.example.org", "test", "", "indexed slot 10503"),
            ]
        );
    }

    #[test]
    fn parse_enterprise_source_values_treats_slot_widths_as_distinct_slots() {
        let mut values = HashMap::new();
        values.insert(String::from("config_id_00001"), String::from(TEST_ID_A));
        values.insert(
            String::from("config_server_url_00001"),
            String::from("https://padded.example.org"),
        );
        values.insert(String::from("config_id1"), String::from(TEST_ID_B));
        values.insert(
            String::from("config_server_url1"),
            String::from("https://legacy-slot.example.org"),
        );

        let source = parse_enterprise_source_values("test", &values);

        assert_eq!(
            source.configs,
            vec![
                enterprise_config("9072b77d-ca81-40da-be6a-861da525ef7b", "https://padded.example.org", "test", "", "indexed slot 00001"),
                enterprise_config(TEST_ID_B, "https://legacy-slot.example.org", "test", "", "indexed slot 1"),
            ]
        );
    }

    #[test]
    fn parse_enterprise_source_values_ignores_invalid_slot_suffixes() {
        let mut values = HashMap::new();
        values.insert(String::from("config_id_99999"), String::from(TEST_ID_A));
        values.insert(
            String::from("config_server_url_99999"),
            String::from("https://valid.example.org"),
        );
        values.insert(String::from("config_id_100000"), String::from(TEST_ID_B));
        values.insert(
            String::from("config_server_url_100000"),
            String::from("https://too-high.example.org"),
        );
        values.insert(String::from("config_id_abc"), String::from(TEST_ID_C));
        values.insert(
            String::from("config_server_url_abc"),
            String::from("https://letters.example.org"),
        );

        let source = parse_enterprise_source_values("test", &values);

        assert_eq!(
            source.configs,
            vec![enterprise_config("9072b77d-ca81-40da-be6a-861da525ef7b", "https://valid.example.org", "test", "", "indexed slot 99999")]
        );
    }

    #[test]
    fn enterprise_environment_key_name_maps_indexed_and_legacy_names() {
        assert_eq!(
            enterprise_environment_key_name("MINDWORK_AI_STUDIO_ENTERPRISE_CONFIG_ID_10503"),
            Some(String::from("config_id10503"))
        );
        assert_eq!(
            enterprise_environment_key_name("MINDWORK_AI_STUDIO_ENTERPRISE_CONFIG_SERVER_URL_00000"),
            Some(String::from("config_server_url00000"))
        );
        assert_eq!(
            enterprise_environment_key_name("MINDWORK_AI_STUDIO_ENTERPRISE_CONFIGS"),
            Some(String::from("configs"))
        );
        assert_eq!(
            enterprise_environment_key_name("MINDWORK_AI_STUDIO_ENTERPRISE_CONFIG_ID_100000"),
            None
        );
    }

    #[test]
    fn parse_enterprise_source_values_keeps_environment_id_variable_as_source_detail() {
        let mut values = EnterpriseSourceValues::new();
        values.insert(
            String::from("config_id00000"),
            EnterpriseSourceValue::new(
                String::from(TEST_ID_A),
                String::from("MINDWORK_AI_STUDIO_ENTERPRISE_CONFIG_ID_00000"),
            ),
        );
        values.insert(
            String::from("config_server_url00000"),
            EnterpriseSourceValue::new(String::from("https://env.example.org"), String::new()),
        );

        let source = parse_enterprise_source_values("environment variables", &values);

        assert_eq!(
            source.configs,
            vec![enterprise_config(
                "9072b77d-ca81-40da-be6a-861da525ef7b",
                "https://env.example.org",
                "environment variables",
                "MINDWORK_AI_STUDIO_ENTERPRISE_CONFIG_ID_00000",
                "indexed slot 00000"
            )]
        );
    }

    #[test]
    fn enterprise_policy_file_slot_suffix_accepts_valid_slot_file_names() {
        assert_eq!(enterprise_policy_file_slot_suffix("config0.yaml"), Some("0"));
        assert_eq!(
            enterprise_policy_file_slot_suffix("config_00000.yaml"),
            Some("00000")
        );
        assert_eq!(
            enterprise_policy_file_slot_suffix("config_10503.yaml"),
            Some("10503")
        );
        assert_eq!(enterprise_policy_file_slot_suffix("config_100000.yaml"), None);
        assert_eq!(enterprise_policy_file_slot_suffix("config_abc.yaml"), None);
    }

    #[test]
    fn select_effective_enterprise_config_source_uses_first_source_with_configs_only() {
        let selected = select_effective_enterprise_config_source(vec![
            EnterpriseSourceData {
                source_name: String::from("registry"),
                configs: vec![enterprise_config(&TEST_ID_A.to_lowercase(), "https://registry.example.org", "registry", "", "indexed slot 0")],
                encryption_secret: String::new(),
            },
            EnterpriseSourceData {
                source_name: String::from("environment"),
                configs: vec![enterprise_config(TEST_ID_B, "https://env.example.org", "environment", "", "indexed slot 0")],
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
                configs: vec![enterprise_config(TEST_ID_B, "https://env.example.org", "environment", "", "indexed slot 0")],
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
                configs: vec![enterprise_config(&TEST_ID_A.to_lowercase(), "https://registry.example.org", "registry", "", "indexed slot 0")],
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
            linux_policy_directories_from_xdg(Some(" /opt/company:/etc/xdg "), false),
            vec![
                PathBuf::from("/opt/company/mindwork-ai-studio"),
                PathBuf::from("/etc/xdg/mindwork-ai-studio"),
            ]
        );

        assert_eq!(
            linux_policy_directories_from_xdg(Some(" : "), false),
            vec![PathBuf::from("/etc/xdg/mindwork-ai-studio")]
        );
        assert_eq!(
            linux_policy_directories_from_xdg(None, false),
            vec![PathBuf::from("/etc/xdg/mindwork-ai-studio")]
        );
    }

    #[test]
    fn linux_policy_directories_from_xdg_checks_flatpak_provisioning_first() {
        assert_eq!(
            linux_policy_directories_from_xdg(Some(" /opt/company:/etc/xdg "), true),
            vec![
                PathBuf::from("/app/etc/MindWorkAI"),
                PathBuf::from("/opt/company/mindwork-ai-studio"),
                PathBuf::from("/etc/xdg/mindwork-ai-studio"),
            ]
        );

        assert_eq!(
            linux_policy_directories_from_xdg(None, true),
            vec![
                PathBuf::from("/app/etc/MindWorkAI"),
                PathBuf::from("/etc/xdg/mindwork-ai-studio"),
            ]
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
            values.get("config_id0").map(|value| value.value.as_str()),
            Some("9072b77d-ca81-40da-be6a-861da525ef7b")
        );
        assert_eq!(
            values.get("config_server_url0").map(|value| value.value.as_str()),
            Some("https://org.example.org")
        );
        assert_eq!(
            values.get("config_id1").map(|value| value.value.as_str()),
            Some("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb")
        );
        assert_eq!(
            values.get("config_encryption_secret").map(|value| value.value.as_str()),
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
                enterprise_config("9072b77d-ca81-40da-be6a-861da525ef7b", "https://slot0.example.org", "policy files", &policy_path(directory.path().join("config0.yaml")), "indexed slot 0"),
                enterprise_config(TEST_ID_B, "https://slot4.example.org", "policy files", &policy_path(directory.path().join("config4.yaml")), "indexed slot 4"),
            ]
        );
    }

    #[test]
    fn load_policy_values_from_directories_supports_padded_and_high_policy_slots() {
        let directory = tempdir().unwrap();

        fs::write(
            directory.path().join("config_00000.yaml"),
            "id: \"9072b77d-ca81-40da-be6a-861da525ef7b\"\nserver_url: \"https://slot0.example.org\"",
        )
        .unwrap();
        fs::write(
            directory.path().join("config_10503.yaml"),
            "id: \"a1b2c3d4-e5f6-7890-abcd-ef1234567890\"\nserver_url: \"https://slot10503.example.org\"",
        )
        .unwrap();
        fs::write(
            directory.path().join("config_100000.yaml"),
            "id: \"11111111-2222-3333-4444-555555555555\"\nserver_url: \"https://ignored.example.org\"",
        )
        .unwrap();

        let values = load_policy_values_from_directories(&[directory.path().to_path_buf()]);
        let source = parse_enterprise_source_values("policy files", &values);

        assert_eq!(
            source.configs,
            vec![
                enterprise_config("9072b77d-ca81-40da-be6a-861da525ef7b", "https://slot0.example.org", "policy files", &policy_path(directory.path().join("config_00000.yaml")), "indexed slot 00000"),
                enterprise_config(TEST_ID_B, "https://slot10503.example.org", "policy files", &policy_path(directory.path().join("config_10503.yaml")), "indexed slot 10503"),
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
    fn load_external_http_custom_root_certificate_policy_uses_first_valid_directory() {
        let directory_a = tempdir().unwrap();
        let directory_b = tempdir().unwrap();

        fs::write(
            directory_a.path().join("external_http_custom_root_certificates.yaml"),
            "enabled: true\nbundle_path: \"/app/etc/MindWorkAI/company-a.pem\"\nallowed_hosts: \"*.a.example.org;eri.a.example.org\"",
        )
        .unwrap();
        fs::write(
            directory_b.path().join("external_http_custom_root_certificates.yaml"),
            "enabled: true\nbundle_path: \"/app/etc/MindWorkAI/company-b.pem\"\nallowed_hosts: \"*.b.example.org\"",
        )
        .unwrap();

        let policy = load_external_http_custom_root_certificate_policy_from_directories(&[
            directory_a.path().to_path_buf(),
            directory_b.path().to_path_buf(),
        ]);

        assert_eq!(
            policy,
            ExternalHttpCustomRootCertificatePolicy {
                is_configured: true,
                enabled: true,
                bundle_path: String::from("/app/etc/MindWorkAI/company-a.pem"),
                allowed_hosts: String::from("*.a.example.org;eri.a.example.org"),
                source_detail: policy_path(directory_a.path().join("external_http_custom_root_certificates.yaml")),
            }
        );
    }

    #[test]
    fn load_external_http_custom_root_certificate_policy_allows_disabled_policy_to_win() {
        let directory_a = tempdir().unwrap();
        let directory_b = tempdir().unwrap();

        fs::write(
            directory_a.path().join("external_http_custom_root_certificates.yaml"),
            "enabled: false",
        )
        .unwrap();
        fs::write(
            directory_b.path().join("external_http_custom_root_certificates.yaml"),
            "enabled: true\nbundle_path: \"/app/etc/MindWorkAI/company-b.pem\"\nallowed_hosts: \"*.b.example.org\"",
        )
        .unwrap();

        let policy = load_external_http_custom_root_certificate_policy_from_directories(&[
            directory_a.path().to_path_buf(),
            directory_b.path().to_path_buf(),
        ]);

        assert_eq!(
            policy,
            ExternalHttpCustomRootCertificatePolicy {
                is_configured: true,
                enabled: false,
                bundle_path: String::new(),
                allowed_hosts: String::new(),
                source_detail: policy_path(directory_a.path().join("external_http_custom_root_certificates.yaml")),
            }
        );
    }

    #[test]
    fn load_external_http_custom_root_certificate_policy_skips_invalid_files() {
        let directory_a = tempdir().unwrap();
        let directory_b = tempdir().unwrap();

        fs::write(
            directory_a.path().join("external_http_custom_root_certificates.yaml"),
            "enabled: maybe\nbundle_path: \"/app/etc/MindWorkAI/ignored.pem\"",
        )
        .unwrap();
        fs::write(
            directory_b.path().join("external_http_custom_root_certificates.yaml"),
            "enabled: yes\nbundle_path: \"/app/etc/MindWorkAI/company-b.pem\"\nallowed_hosts: \"*.b.example.org,eri.b.example.org\"",
        )
        .unwrap();

        let policy = load_external_http_custom_root_certificate_policy_from_directories(&[
            directory_a.path().to_path_buf(),
            directory_b.path().to_path_buf(),
        ]);

        assert_eq!(
            policy,
            ExternalHttpCustomRootCertificatePolicy {
                is_configured: true,
                enabled: true,
                bundle_path: String::from("/app/etc/MindWorkAI/company-b.pem"),
                allowed_hosts: String::from("*.b.example.org,eri.b.example.org"),
                source_detail: policy_path(directory_b.path().join("external_http_custom_root_certificates.yaml")),
            }
        );
    }

    #[test]
    fn load_external_http_custom_root_certificate_policy_requires_enabled_key() {
        let directory = tempdir().unwrap();

        fs::write(
            directory.path().join("external_http_custom_root_certificates.yaml"),
            "bundle_path: \"/app/etc/MindWorkAI/company.pem\"\nallowed_hosts: \"*.example.org\"",
        )
        .unwrap();

        let policy = load_external_http_custom_root_certificate_policy_from_directories(&[directory.path().to_path_buf()]);

        assert_eq!(policy, ExternalHttpCustomRootCertificatePolicy::default());
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