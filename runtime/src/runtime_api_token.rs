use once_cell::sync::Lazy;
use axum::extract::FromRequestParts;
use axum::http::request::Parts;
use axum::http::StatusCode;
use crate::api_token::{generate_api_token, APIToken};

pub static API_TOKEN: Lazy<APIToken> = Lazy::new(generate_api_token);

impl<S> FromRequestParts<S> for APIToken
where
    S: Send + Sync,
{
    type Rejection = StatusCode;

    async fn from_request_parts(parts: &mut Parts, _state: &S) -> Result<Self, Self::Rejection> {
        match parts.headers.get("token").and_then(|value| value.to_str().ok()) {
            Some(token) => {
                let received_token = APIToken::from_hex_text(token);
                if API_TOKEN.validate(&received_token) {
                    Ok(received_token)
                } else {
                    Err(StatusCode::UNAUTHORIZED)
                }
            }

            None => Err(StatusCode::UNAUTHORIZED),
        }
    }
}

/// The API token error types.
#[derive(Debug)]
pub enum APITokenError {
    Missing,
    Invalid,
}