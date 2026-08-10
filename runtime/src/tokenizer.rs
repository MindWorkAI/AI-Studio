use std::collections::HashMap;
use std::fs;
use std::path::PathBuf;
use std::sync::{Arc, Mutex, OnceLock, RwLock};

use axum::Json;
use log::{error, warn};
use serde::{Deserialize, Serialize};
use tauri::path::BaseDirectory;
use tauri::Manager;
use tokenizers::tokenizer::Tokenizer;

use crate::api_token::APIToken;
use crate::environment::DATA_DIRECTORY;

const DEFAULT_TOKENIZER_RESOURCE_PATH: &str = "resources/tokenizers/tokenizer.json";

static TOKENIZERS: OnceLock<RwLock<HashMap<PathBuf, Arc<Tokenizer>>>> = OnceLock::new();
static DEFAULT_TOKENIZER_PATH: OnceLock<PathBuf> = OnceLock::new();
static TOKENIZER_STORAGE_LOCK: Mutex<()> = Mutex::new(());

#[derive(Deserialize)]
pub struct SetTokenText {
    text: String,
    tokenizer_path: String,
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
    stored_path: String,
}

impl TokenizerResponse {
    fn available(token_count: usize) -> Self {
        TokenizerResponse {
            success: true,
            token_count,
            message: String::new(),
            stored_path: String::new(),
        }
    }

    fn stored(stored_path: String) -> Self {
        TokenizerResponse {
            success: true,
            token_count: 0,
            message: String::new(),
            stored_path,
        }
    }

    fn unavailable(reason: String) -> Self {
        TokenizerResponse {
            success: false,
            token_count: 0,
            message: reason,
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
            return;
        }
    };

    if !tokenizer_path.is_file() {
        let reason = format!("The default tokenizer file was not found: {}", tokenizer_path.display());
        error!(Source = "Tokenizer"; "{reason}");
        return;
    }

    match DEFAULT_TOKENIZER_PATH.set(tokenizer_path) {
        Ok(_) => (),
        Err(e) => warn!(Source = "Tokenizer"; "Could not set the default tokenizer path: {:?}", e),
    }
}

pub async fn token_count(_token: APIToken, req: Json<SetTokenText>) -> Json<TokenizerResponse> {
    match get_token_count(&req.tokenizer_path, &req.text) {
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

fn handle_tokenizer_validate(path: &PathBuf) -> Result<usize, String> {
    validate_tokenizer_file(path)
}

pub fn get_token_count(path: &str, text: &str) -> Result<usize, String> {
    let tokenizer = get_tokenizer(path)?;
    get_token_count_internal(&tokenizer, text, true)
}

pub fn get_segment_token_count(tokenizer: &Tokenizer, text: &str) -> Result<usize, String> {
    // Special tokens belong to the final encoding and would inflate sums across many segments.
    get_token_count_internal(tokenizer, text, false)
}

fn get_token_count_internal(tokenizer: &Tokenizer, text: &str, add_special_tokens: bool) -> Result<usize, String> {
    if text.trim().is_empty() {
        return Ok(0);
    }

    tokenizer
        .encode(text, add_special_tokens)
        .map(|encoding| encoding.len())
        .map_err(|e| format!("Failed to tokenize text: {e}"))
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

    let _storage_guard = TOKENIZER_STORAGE_LOCK
        .lock()
        .map_err(|_| std::io::Error::other("Tokenizer storage lock is poisoned."))?;
    if model_path.try_exists()? {
        invalidate_tokenizers_under(&model_path);
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

    let _storage_guard = TOKENIZER_STORAGE_LOCK
        .lock()
        .map_err(|_| std::io::Error::other("Tokenizer storage lock is poisoned."))?;
    if tokenizer_path.exists() {
        invalidate_tokenizers_under(&tokenizer_path);
        fs::remove_dir_all(tokenizer_path)?;
    }

    Ok(())
}

fn tokenizer_cache() -> &'static RwLock<HashMap<PathBuf, Arc<Tokenizer>>> {
    TOKENIZERS.get_or_init(|| RwLock::new(HashMap::new()))
}

pub fn get_tokenizer(path: &str) -> Result<Arc<Tokenizer>, String> {
    let resolved_path = resolve_tokenizer_path(path)?;
    let tokenizer_path = fs::canonicalize(&resolved_path)
        .map_err(|e| format!("Could not resolve tokenizer file '{}': {e}", resolved_path.display()))?;

    if let Some(tokenizer) = tokenizer_cache()
        .read()
        .map_err(|_| "Tokenizer cache lock is poisoned.".to_string())?
        .get(&tokenizer_path)
        .cloned()
    {
        return Ok(tokenizer);
    }

    let _storage_guard = TOKENIZER_STORAGE_LOCK
        .lock()
        .map_err(|_| "Tokenizer storage lock is poisoned.".to_string())?;
    if let Some(tokenizer) = tokenizer_cache()
        .read()
        .map_err(|_| "Tokenizer cache lock is poisoned.".to_string())?
        .get(&tokenizer_path)
        .cloned()
    {
        return Ok(tokenizer);
    }

    let loaded_tokenizer = Arc::new(load_tokenizer_from_file(&tokenizer_path)?);
    let mut cache = tokenizer_cache()
        .write()
        .map_err(|_| "Tokenizer cache lock is poisoned.".to_string())?;
    Ok(cache
        .entry(tokenizer_path)
        .or_insert_with(|| loaded_tokenizer)
        .clone())
}

fn invalidate_tokenizers_under(path: &PathBuf) {
    let cache_path = fs::canonicalize(path).unwrap_or_else(|_| path.clone());
    match tokenizer_cache().write() {
        Ok(mut cache) => cache.retain(|tokenizer_path, _| !tokenizer_path.starts_with(&cache_path)),
        Err(_) => warn!(Source = "Tokenizer"; "Could not invalidate tokenizer cache because its lock is poisoned."),
    }
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
