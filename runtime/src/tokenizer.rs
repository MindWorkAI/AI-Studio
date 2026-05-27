use std::fs;
use std::path::PathBuf;
use std::sync::{Mutex, OnceLock, RwLock};

use axum::Json;
use log::{error, warn};
use once_cell::sync::Lazy;
use serde::{Deserialize, Serialize};
use tauri::path::BaseDirectory;
use tauri::Manager;
use tokenizers::tokenizer::Tokenizer;

use crate::api_token::APIToken;
use crate::environment::DATA_DIRECTORY;

const DEFAULT_TOKENIZER_RESOURCE_PATH: &str = "resources/tokenizers/tokenizer.json";
const NO_TOKENIZER_LOADED_MESSAGE: &str = "Tokenizer must be set before counting tokens.";

static TOKENIZER: OnceLock<RwLock<Option<Tokenizer>>> = OnceLock::new();
static DEFAULT_TOKENIZER_PATH: OnceLock<PathBuf> = OnceLock::new();
static TOKENIZER_STATUS: Lazy<Mutex<TokenizerStatusInfo>> = Lazy::new(|| Mutex::new(TokenizerStatusInfo::default()));
static TOKENIZER_OPERATION_LOCK: Lazy<Mutex<()>> = Lazy::new(|| Mutex::new(()));

#[derive(Clone, Copy, Default, Serialize, PartialEq, Eq)]
pub enum TokenizerStatus {
    #[default]
    Unavailable,
    Running,
    Available,
}

#[derive(Default)]
struct TokenizerStatusInfo {
    status: TokenizerStatus,
    unavailable_reason: Option<String>,
}

#[derive(Deserialize)]
pub struct SetTokenText {
    text: String,
}

#[derive(Clone, Deserialize)]
pub struct TokenizerStorage {
    model_id: String,
    file_path: String,
}

#[derive(Clone, Deserialize)]
pub struct TokenizerDelete {
    model_id: String,
}

#[derive(Clone, Deserialize)]
pub struct TokenizerPath {
    file_path: String,
}

#[derive(Serialize)]
pub struct TokenizerResponse {
    success: bool,
    token_count: usize,
    message: String,
    status: TokenizerStatus,
    stored_path: String,
}

impl TokenizerResponse {
    fn available(token_count: usize) -> Self {
        TokenizerResponse {
            success: true,
            token_count,
            message: String::new(),
            status: TokenizerStatus::Available,
            stored_path: String::new(),
        }
    }

    fn stored(stored_path: String) -> Self {
        TokenizerResponse {
            success: true,
            token_count: 0,
            message: String::new(),
            status: TokenizerStatus::Available,
            stored_path,
        }
    }

    fn unavailable(reason: String) -> Self {
        TokenizerResponse {
            success: false,
            token_count: 0,
            message: reason,
            status: TokenizerStatus::Unavailable,
            stored_path: String::new(),
        }
    }
}

pub fn set_default_tokenizer_path(app_handle: tauri::AppHandle) {
    let tokenizer_path = match app_handle
        .path()
        .resolve(DEFAULT_TOKENIZER_RESOURCE_PATH, BaseDirectory::Resource)
    {
        Ok(path) => path,
        Err(e) => {
            let reason = format!("The default tokenizer file '{DEFAULT_TOKENIZER_RESOURCE_PATH}' could not be resolved: {e}");
            error!(Source = "Tokenizer"; "{reason}");
            set_tokenizer_unavailable(reason);
            return;
        }
    };

    if !tokenizer_path.is_file() {
        let reason = format!("The default tokenizer file was not found: {}", tokenizer_path.display());
        error!(Source = "Tokenizer"; "{reason}");
        set_tokenizer_unavailable(reason);
        return;
    }

    match DEFAULT_TOKENIZER_PATH.set(tokenizer_path) {
        Ok(_) => (),
        Err(e) => warn!(Source = "Tokenizer"; "Could not set the default tokenizer path: {:?}", e),
    }
}

pub async fn tokenizer_info(_token: APIToken) -> Json<TokenizerResponse> {
    let status = TOKENIZER_STATUS.lock().unwrap();
    match status.status {
        TokenizerStatus::Available => Json(TokenizerResponse::available(0)),
        TokenizerStatus::Running => Json(TokenizerResponse {
            success: false,
            token_count: 0,
            message: String::new(),
            status: TokenizerStatus::Running,
            stored_path: String::new(),
        }),
        TokenizerStatus::Unavailable => Json(TokenizerResponse::unavailable(status.unavailable_reason.clone().unwrap_or_default())),
    }
}

pub async fn token_count(_token: APIToken, req: Json<SetTokenText>) -> Json<TokenizerResponse> {
    match get_token_count(&req.text) {
        Ok(count) => Json(TokenizerResponse::available(count)),
        Err(e) => Json(TokenizerResponse::unavailable(e)),
    }
}

pub async fn validate_tokenizer(_token: APIToken, payload: Json<TokenizerPath>) -> Json<TokenizerResponse> {
    match handle_tokenizer_validate(&PathBuf::from(payload.file_path.clone())) {
        Ok(count) => Json(TokenizerResponse::available(count)),
        Err(e) => Json(TokenizerResponse::unavailable(e)),
    }
}

pub async fn store_tokenizer(_token: APIToken, payload: Json<TokenizerStorage>) -> Json<TokenizerResponse> {
    match handle_tokenizer_store(&payload) {
        Ok(dest_path) => Json(TokenizerResponse::stored(dest_path)),
        Err(e) => Json(TokenizerResponse::unavailable(e.to_string())),
    }
}

pub async fn delete_tokenizer(_token: APIToken, payload: Json<TokenizerDelete>) -> Json<TokenizerResponse> {
    match handle_tokenizer_delete(&payload) {
        Ok(_) => Json(TokenizerResponse::stored(String::new())),
        Err(e) => Json(TokenizerResponse::unavailable(e.to_string())),
    }
}

pub async fn set_tokenizer(_token: APIToken, payload: Json<TokenizerPath>) -> Json<TokenizerResponse> {
    match handle_tokenizer_set(&payload.file_path) {
        Ok(_) => Json(TokenizerResponse::available(0)),
        Err(e) => Json(TokenizerResponse::unavailable(e)),
    }
}

pub fn handle_tokenizer_set(path: &str) -> Result<(), String> {
    let _operation_guard = begin_tokenizer_operation()?;
    set_tokenizer_running();

    let tokenizer_path = resolve_tokenizer_path(path).map_err(|e| {
        error!(Source = "Tokenizer"; "{e} Starting the app without a tokenizer.");
        unavailable_with_status_update(&e)
    })?;

    let tokenizer = load_tokenizer_from_file(&tokenizer_path).map_err(|e| {
        error!(Source = "Tokenizer"; "{e}");
        unavailable_with_status_update(&e)
    })?;

    match tokenizer_state().write() {
        Ok(mut tokenizer_guard) => *tokenizer_guard = Some(tokenizer),
        Err(_) => return Err(unavailable_with_status_update("Tokenizer state lock is poisoned.")),
    }

    set_tokenizer_available();
    Ok(())
}

fn handle_tokenizer_validate(path: &PathBuf) -> Result<usize, String> {
    let _operation_guard = begin_tokenizer_operation()?;
    set_tokenizer_running();

    let result = validate_tokenizer_file(path);
    match tokenizer_state().read() {
        Ok(tokenizer_guard) if tokenizer_guard.is_some() => set_tokenizer_available(),
        Ok(_) => set_tokenizer_unavailable(NO_TOKENIZER_LOADED_MESSAGE.to_string()),
        Err(_) => set_tokenizer_unavailable("Tokenizer state lock is poisoned.".to_string()),
    }

    result
}

pub fn get_token_count(text: &str) -> Result<usize, String> {
    if text.trim().is_empty() {
        return Ok(0);
    }

    let _operation_guard = begin_tokenizer_operation()?;
    {
        let status = TOKENIZER_STATUS.lock().unwrap();
        if status.status != TokenizerStatus::Available {
            return Err(status.unavailable_reason.clone().unwrap_or_else(|| NO_TOKENIZER_LOADED_MESSAGE.to_string()));
        }
    }

    let tokenizer_guard = tokenizer_state()
        .read()
        .map_err(|_| unavailable_with_status_update("Tokenizer state lock is poisoned."))?;
    let tokenizer = match tokenizer_guard.as_ref() {
        Some(tokenizer) => tokenizer,
        None => {
            drop(tokenizer_guard);
            return Err(unavailable_with_status_update("Tokenizer not initialized."));
        }
    };
    let token_count = match tokenizer.encode(text, true) {
        Ok(enc) => enc.len(),
        Err(e) => {
            let reason = format!("Failed to tokenize text: {e}");
            drop(tokenizer_guard);
            return Err(unavailable_with_status_update(&reason));
        }
    };

    Ok(token_count)
}

fn validate_tokenizer_file(path: &PathBuf) -> Result<usize, String> {
    let tokenizer = load_tokenizer_from_file(path)?;
    let test_string = "Hello, world! This is a test string for tokenizer validation.";
    let encoding = tokenizer
        .encode(test_string, true)
        .map_err(|e| format!("Tokenizer failed to encode validation string: {e}"))?;
    let token_count = encoding.len();

    if token_count == 0 {
        return Err("Tokenizer produced 0 tokens for test string. The tokenizer is likely invalid or misconfigured.".to_string());
    }

    if encoding.get_tokens().iter().any(|t| t.is_empty()) {
        return Err("Tokenizer produced empty tokens. The tokenizer is invalid.".to_string());
    }

    Ok(token_count)
}

fn handle_tokenizer_store(payload: &TokenizerStorage) -> Result<String, std::io::Error> {
    let data_dir = DATA_DIRECTORY
        .get()
        .ok_or_else(|| std::io::Error::new(std::io::ErrorKind::Other, "DATA_DIRECTORY not initialized"))?;

    let base_path = PathBuf::from(data_dir).join("tokenizers");

    let source_path = PathBuf::from(&payload.file_path);
    let source_name = source_path
        .file_name()
        .and_then(|n| n.to_str())
        .ok_or_else(|| std::io::Error::new(std::io::ErrorKind::InvalidInput, "Invalid tokenizer file path"))?;
    let model_path = base_path.join(&payload.model_id);
    let destination_path = model_path.join(source_name);

    if source_path.eq(&destination_path) {
        return Ok(destination_path.to_string_lossy().to_string());
    }

    if model_path.try_exists()? {
        fs::remove_dir_all(&model_path)?;
    }

    if payload.file_path.trim().is_empty() {
        return Ok(String::new());
    }

    fs::create_dir_all(&model_path)?;
    fs::copy(&source_path, &destination_path)?;

    Ok(destination_path.to_string_lossy().to_string())
}

fn handle_tokenizer_delete(payload: &TokenizerDelete) -> Result<(), std::io::Error> {
    if payload.model_id.trim().is_empty() {
        return Ok(());
    }

    let data_dir = DATA_DIRECTORY
        .get()
        .ok_or_else(|| std::io::Error::new(std::io::ErrorKind::Other, "DATA_DIRECTORY not initialized"))?;

    let tokenizer_path = PathBuf::from(data_dir)
        .join("tokenizers")
        .join(&payload.model_id);

    if tokenizer_path.exists() {
        fs::remove_dir_all(tokenizer_path)?;
    }

    Ok(())
}

fn tokenizer_state() -> &'static RwLock<Option<Tokenizer>> {
    TOKENIZER.get_or_init(|| RwLock::new(None))
}

fn begin_tokenizer_operation() -> Result<std::sync::MutexGuard<'static, ()>, String> {
    TOKENIZER_OPERATION_LOCK
        .lock()
        .map_err(|_| unavailable_with_status_update("Tokenizer operation lock is poisoned."))
}

fn set_tokenizer_available() {
    let mut status = TOKENIZER_STATUS.lock().unwrap();
    status.status = TokenizerStatus::Available;
    status.unavailable_reason = None;
}

fn set_tokenizer_running() {
    let mut status = TOKENIZER_STATUS.lock().unwrap();
    status.status = TokenizerStatus::Running;
    status.unavailable_reason = None;
}

fn set_tokenizer_unavailable(reason: String) {
    let mut status = TOKENIZER_STATUS.lock().unwrap();
    status.status = TokenizerStatus::Unavailable;
    status.unavailable_reason = Some(reason);
}

fn unavailable_with_status_update(reason: &str) -> String {
    let reason = reason.to_string();
    match tokenizer_state().write() {
        Ok(mut tokenizer_guard) => *tokenizer_guard = None,
        Err(_) => set_tokenizer_unavailable("Tokenizer state lock is poisoned.".to_string()),
    }

    set_tokenizer_unavailable(reason.clone());
    reason
}

fn resolve_tokenizer_path(path: &str) -> Result<PathBuf, String> {
    if !path.trim().is_empty() {
        return Ok(PathBuf::from(path));
    }

    DEFAULT_TOKENIZER_PATH
        .get()
        .cloned()
        .ok_or_else(|| "Default tokenizer path is not initialized.".to_string())
}

fn load_tokenizer_from_file(path: &PathBuf) -> Result<Tokenizer, String> {
    if !path.is_file() {
        return Err(format!("Tokenizer file was not found: {}", path.display()));
    }

    Tokenizer::from_file(path)
        .map_err(|e| format!("Failed to load tokenizer from '{}': {e}", path.display()))
}
