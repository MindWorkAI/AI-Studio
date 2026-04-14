use std::fs;
use std::path::PathBuf;
use std::sync::{OnceLock, RwLock};
use log::warn;
use rocket::post;
use rocket::serde::json::Json;
use rocket::serde::Serialize;
use serde::Deserialize;
use tauri::PathResolver;
use tokenizers::Error;
use tokenizers::tokenizer::{Tokenizer, Error as TokenizerError};
use crate::api_token::APIToken;
use crate::environment::DATA_DIRECTORY;

static TOKENIZER: OnceLock<RwLock<Option<Tokenizer>>> = OnceLock::new();

static TOKENIZER_PATH_RESOLVER: OnceLock<PathResolver> = OnceLock::new();

#[derive(Deserialize)]
pub struct SetTokenText {
    pub text: String,
}

#[derive(Clone, Deserialize)]
pub struct TokenizerStorage {
    model_id: String,
    previous_model_id: String,
    file_path: String,
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
}

impl From<Result<usize, TokenizerError>> for TokenizerResponse {
    fn from(result: Result<usize, TokenizerError>) -> Self {
        match result {
            Ok(count) => TokenizerResponse {
                success: true,
                token_count: count,
                message: "Success".to_string(),
            },
            Err(e) => TokenizerResponse {
                success: false,
                token_count: 0,
                message: e.to_string(),
            },
        }
    }
}

pub fn set_path_resolver(path_resolver: PathResolver) {
    match TOKENIZER_PATH_RESOLVER.set(path_resolver) {
        Ok(_) => (),
        Err(e) => warn!(Source = "Tokenizer"; "Could not set the path resolver: {:?}", e),
    }
}

fn tokenizer_state() -> &'static RwLock<Option<Tokenizer>> {
    TOKENIZER.get_or_init(|| RwLock::new(None))
}

pub fn init_tokenizer(path: &str) -> Result<(), Error> {
    let tokenizer_path = if path.trim().is_empty() {
        let relative_source_path = String::from("resources/tokenizers/tokenizer.json");
        let path_resolver = TOKENIZER_PATH_RESOLVER
            .get()
            .ok_or_else(|| Error::from("Tokenizer path resolver is not initialized"))?;
        path_resolver
            .resolve_resource(relative_source_path)
            .ok_or_else(|| Error::from("Failed to resolve default tokenizer resource path"))?
    } else {
        PathBuf::from(path)
    };

    let tokenizer = Tokenizer::from_file(tokenizer_path)?;
    let mut tokenizer_guard = tokenizer_state()
        .write()
        .map_err(|_| Error::from("Tokenizer state lock is poisoned"))?;
    *tokenizer_guard = Some(tokenizer);

    Ok(())
}

fn validate_tokenizer_at_path(path: &PathBuf) -> Result<usize, TokenizerError> {
    if !path.is_file() {
        return Err(TokenizerError::from(format!(
            "Tokenizer file was not found: {}",
            path.display()
        )));
    }

    let tokenizer = Tokenizer::from_file(path).map_err(|e| {
        TokenizerError::from(format!(
            "Failed to load tokenizer from '{}': {}",
            path.display(),
            e
        ))
    })?;

    let test_string = "Hello, world! This is a test string for tokenizer validation.";

    let encoding = tokenizer.encode(test_string, true).map_err(|e| {
        TokenizerError::from(format!(
            "Tokenizer failed to encode validation string: {}",
            e
        ))
    })?;
    let token_count = encoding.len();

    if token_count == 0 {
        return Err(TokenizerError::from(
            "Tokenizer produced 0 tokens for test string. The tokenizer is likely invalid or misconfigured.",
        ));
    }

    if encoding.get_tokens().iter().any(|t| t.is_empty()) {
        return Err(TokenizerError::from(
            "Tokenizer produced empty tokens. The tokenizer is invalid.",
        ));
    }

    Ok(token_count)
}

fn handle_tokenizer_store(payload: &TokenizerStorage) -> Result<String, std::io::Error> {
    let data_dir = DATA_DIRECTORY
        .get()
        .ok_or_else(|| std::io::Error::new(std::io::ErrorKind::Other, "DATA_DIRECTORY not initialized"))?;

    let base_path = PathBuf::from(data_dir).join("tokenizers");

    if payload.file_path.trim().is_empty() {
        if payload.previous_model_id.trim().is_empty() {
            return Ok(String::from(""));
        }

        let previous_path = base_path.join(&payload.previous_model_id);
        fs::remove_dir_all(previous_path)?;
        return Ok(String::from(""));
    }

    let source_path = PathBuf::from(&payload.file_path);
    let source_name = source_path
        .file_name()
        .and_then(|n| n.to_str())
        .ok_or_else(|| std::io::Error::new(std::io::ErrorKind::InvalidInput, "Invalid tokenizer file path"))?;
    let model_path = &base_path.join(&payload.model_id);
    let destination_path = &model_path.join(source_name);
    println!(
        "source_path: {}, destination_path: {}",
        source_path.display(),
        destination_path.display()
    );
    println!("equals {}", source_path.eq(destination_path));

    if !source_path.eq(destination_path) && model_path.exists() {
        fs::remove_dir_all(model_path)?;
    }
    fs::create_dir_all(model_path)?;
    println!(
        "Moving tokenizer file from {} to {}",
        source_path.display(),
        destination_path.display()
    );
    let previous_path = base_path.join(&payload.previous_model_id);

    if !payload.previous_model_id.trim().is_empty() && source_path.starts_with(&previous_path) {
        fs::rename(&source_path, &destination_path)?;
        if previous_path.exists() && !previous_path.eq(model_path) {
            fs::remove_dir_all(previous_path)?;
        }
    } else {
        fs::copy(&source_path, &destination_path)?;
    }
    Ok(destination_path.to_str().unwrap().to_string())
}

pub fn get_token_count(text: &str) -> Result<usize, TokenizerError> {
    if text.trim().is_empty() {
        return Err(TokenizerError::from("Input text is empty"));
    }

    let tokenizer = tokenizer_state()
        .read()
        .map_err(|_| TokenizerError::from("Tokenizer state lock is poisoned"))?
        .clone()
        .ok_or_else(|| TokenizerError::from("Tokenizer not initialized"))?;
    let enc = tokenizer.encode(text, true)?;
    Ok(enc.len())
}

#[post("/tokenizer/count", data = "<req>")]
pub fn token_count(_token: APIToken, req: Json<SetTokenText>) -> Json<TokenizerResponse> {
    Json(get_token_count(&req.text).into())
}

#[post("/tokenizer/validate", data = "<payload>")]
pub fn validate_tokenizer(_token: APIToken, payload: Json<TokenizerPath>) -> Json<TokenizerResponse> {
    println!("Received tokenizer validation request: {}", payload.file_path);
    Json(validate_tokenizer_at_path(&PathBuf::from(payload.file_path.clone())).into())
}

#[post("/tokenizer/store", data = "<payload>")]
pub fn store_tokenizer(_token: APIToken, payload: Json<TokenizerStorage>) -> Json<TokenizerResponse> {
    println!(
        "Received tokenizer store request: {}, {}, {}",
        payload.model_id, payload.previous_model_id, payload.file_path
    );
    match handle_tokenizer_store(&payload) {
        Ok(dest_path) => Json(TokenizerResponse {
            success: true,
            token_count: 0,
            message: dest_path,
        }),
        Err(e) => Json(TokenizerResponse {
            success: false,
            token_count: 0,
            message: e.to_string(),
        }),
    }
}

#[post("/tokenizer/set", data = "<payload>")]
pub fn set_tokenizer(_token: APIToken, payload: Json<TokenizerPath>) -> Json<TokenizerResponse> {
    match init_tokenizer(&payload.file_path) {
        Ok(_) => Json(TokenizerResponse {
            success: true,
            token_count: 0,
            message: "Success".to_string(),
        }),
        Err(e) => Json(TokenizerResponse {
            success: false,
            token_count: 0,
            message: e.to_string(),
        }),
    }
}
