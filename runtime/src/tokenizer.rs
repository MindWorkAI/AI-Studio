use rocket::yansi::Paint;
use std::fs;
use std::path::{PathBuf};
use std::sync::OnceLock;
use rocket::{post};
use rocket::serde::json::Json;
use rocket::serde::Serialize;
use serde::Deserialize;
use tokenizers::Error;
use tokenizers::tokenizer::{Tokenizer, Error as TokenizerError};
use crate::api_token::APIToken;
use crate::environment::{DATA_DIRECTORY};

static TOKENIZER: OnceLock<Tokenizer> = OnceLock::new();

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
pub struct TokenizerValidation {
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

pub fn init_tokenizer() -> Result<(), Error>{
    let mut target_dir = PathBuf::from("target");
    target_dir.push("tokenizers");
    fs::create_dir_all(&target_dir)?;

    let mut local_tokenizer_path = target_dir.clone();
    local_tokenizer_path.push("tokenizer.json");

    TOKENIZER.set(Tokenizer::from_file(local_tokenizer_path)?).expect("Could not set the tokenizer.");
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
        println!("Failed to load tokenizer from {}: {}", Paint::red(&path.display()), e);
        TokenizerError::from(format!(
            "Failed to load tokenizer from '{}': {}",
            path.display(),
            e
        ))
    })?;
    println!("Loaded tokenizer from {}", Paint::green(&path.display()));

    let test_string = "Hello, world! This is a test string for tokenizer validation.";

    let encoding = tokenizer.encode(test_string, true).map_err(|e| {
        println!(
            "Tokenizer failed to encode validation string for {}: {}",
            Paint::red(&path.display()),
            e
        );
        TokenizerError::from(format!(
            "Tokenizer failed to encode validation string: {}",
            e
        ))
    })?;
    let token_count = encoding.len();

    if token_count == 0 {
        return Err(TokenizerError::from(
            "Tokenizer produced 0 tokens for test string. The tokenizer is likely invalid or misconfigured."
        ));
    }

    if encoding.get_tokens().iter().any(|t| t.is_empty()) {
        return Err(TokenizerError::from(
            "Tokenizer produced empty tokens. The tokenizer is invalid."
        ));
    }

    Ok(token_count)
}

fn handle_tokenizer_store(payload: &TokenizerStorage) -> Result<(), std::io::Error> {
    let data_dir = DATA_DIRECTORY
        .get()
        .ok_or_else(|| std::io::Error::new(std::io::ErrorKind::Other, "DATA_DIRECTORY not initialized"))?;

    let base_path = PathBuf::from(data_dir).join("tokenizers");

    // Delete previous model if file_path is empty
    if payload.file_path.trim().is_empty() {
        if payload.previous_model_id.trim().is_empty() {
            return Ok(()); // Nothing to delete
        }
        let previous_path = base_path.join(&payload.previous_model_id);
        fs::remove_dir_all(previous_path)?;
        return Ok(());
    }

    // Copy file
    let source_path = PathBuf::from(&payload.file_path);
    let source_name = source_path.file_name()
        .and_then(|n| n.to_str())
        .ok_or_else(|| std::io::Error::new(std::io::ErrorKind::InvalidInput, "Invalid tokenizer file path"))?;
    fs::create_dir_all(&base_path.join(&payload.model_id))?;
    let destination_path = base_path.join(&payload.model_id).join(source_name);
    println!("Moving tokenizer file from {} to {}", source_path.display(), destination_path.display());

    let previous_path = base_path.join(&payload.previous_model_id);

    // Delete previous tokenizer folder if specified
    if !payload.previous_model_id.trim().is_empty() && source_path.starts_with(&previous_path){
        fs::rename(&source_path, &destination_path)?;
        if previous_path.exists() {
            fs::remove_dir_all(previous_path)?;
        }
    }else{
        fs::copy( & source_path, & destination_path)?;
    }
    Ok(())
}

pub fn get_token_count(text: &str) -> Result<usize, TokenizerError> {
    if text.trim().is_empty() {
        return Err(TokenizerError::from("Input text is empty"));
    }

    let tokenizer = TOKENIZER.get().cloned().ok_or_else(|| TokenizerError::from("Tokenizer not initialized"))?;
    let enc = tokenizer.encode(text, true)?;
    Ok(enc.len())
}

#[post("/tokenizer/count", data = "<req>")]
pub fn token_count(_token: APIToken, req: Json<SetTokenText>) -> Json<TokenizerResponse> {
    Json(get_token_count(&req.text).into())
}

#[post("/tokenizer/validate", data = "<payload>")]
pub fn validate_tokenizer(_token: APIToken, payload: Json<TokenizerValidation>) -> Json<TokenizerResponse>{
    println!("Received tokenizer validation request: {}", payload.file_path);
    Json(validate_tokenizer_at_path(&PathBuf::from(payload.file_path.clone())).into())
}

#[post("/tokenizer/store", data = "<payload>")]
pub fn store_tokenizer(_token: APIToken, payload: Json<TokenizerStorage>) -> Json<TokenizerResponse>{
    println!("Received tokenizer store request: {}, {}, {}", payload.model_id, payload.previous_model_id, payload.file_path);
    match handle_tokenizer_store(&payload) {
        Ok(()) => Json(TokenizerResponse {
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