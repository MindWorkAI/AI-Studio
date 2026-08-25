//! The HTTP endpoint for filtering text that does not arrive through a file stream.
//!
//! Files are filtered inside `extract_data`, where the runtime already sees every chunk.
//! Web pages and retrieval contexts never pass through there — the app fetches and converts
//! them itself — so they are handed over here instead. They are small enough that one
//! request per text is cheaper than streaming.

use crate::api_token::APIToken;
use axum::Json;
use serde::{Deserialize, Serialize};

use super::{sanitize_text, Finding};

#[derive(Deserialize)]
pub struct SanitizeRequest {
    pub text: String,
}

#[derive(Serialize)]
pub struct SanitizeResponse {
    /// The text with the suspicious passages filtered out. Usable as it stands: filtering
    /// removes the passages, it does not reject the text.
    pub sanitized_text: String,

    pub findings: Vec<Finding>,

    /// How many passages were filtered. Can exceed the number of findings, which is capped.
    pub redacted_count: usize,
}

pub async fn sanitize(_token: APIToken, Json(request): Json<SanitizeRequest>) -> Json<SanitizeResponse> {
    let (sanitized_text, report) = sanitize_text(&request.text);

    Json(SanitizeResponse {
        sanitized_text,
        findings: report.findings,
        redacted_count: report.redacted_count,
    })
}