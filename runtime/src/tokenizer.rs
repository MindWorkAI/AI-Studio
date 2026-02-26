use std::fs;
use std::path::{PathBuf};
use std::sync::OnceLock;
use rocket::{post};
use rocket::serde::json::Json;
use rocket::serde::Serialize;
use serde::Deserialize;
use tokenizers::Error;
use tokenizers::tokenizer::Tokenizer;
use crate::api_token::APIToken;

static TOKENIZER: OnceLock<Tokenizer> = OnceLock::new();

static TEXT: &str = "";

pub fn init_tokenizer() -> Result<(), Error>{
    let mut target_dir = PathBuf::from("target");
    target_dir.push("tokenizers");
    fs::create_dir_all(&target_dir)?;

    let mut local_tokenizer_path = target_dir.clone();
    local_tokenizer_path.push("tokenizer.json");

    TOKENIZER.set(Tokenizer::from_file(local_tokenizer_path)?).expect("Could not set the tokenizer.");
    Ok(())
}

pub fn get_token_count(mut text: &str) -> usize {
    if text.is_empty() {
        text = TEXT;
    }
    match TOKENIZER.get().unwrap().encode(text, true) {
        Ok(encoding) => encoding.len(),
        Err(_) => 0,
    }
}

#[derive(Deserialize)]
pub struct SetTokenText {
    pub text: String,
}

#[derive(Serialize)]
pub struct GetTokenCount{
    token_count: usize,
}


#[post("/system/tokenizer/count", data = "<req>")]
pub fn tokenizer_count(_token: APIToken, req: Json<SetTokenText>) -> Json<GetTokenCount> {
    Json(GetTokenCount {
        token_count: get_token_count(&req.text),
    })
}