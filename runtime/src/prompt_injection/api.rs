//! The HTTP endpoints for filtering text that does not arrive through a file stream.
//!
//! Files are filtered inside `extract_data`, where the runtime already sees every chunk.
//! Web pages and retrieval contexts never pass through there — the app fetches and converts
//! them itself — so they are handed over here instead. They are small enough that one
//! request per text is cheaper than streaming.
//!
//! A single tool call, however, can produce many texts at once: a web search returns several
//! pages, each with its own content, title, description, and authors. Those go through the
//! batch endpoint, which filters them in one request instead of one round trip per field.

use crate::api_token::APIToken;
use axum::http::StatusCode;
use axum::Json;
use serde::{Deserialize, Serialize};

use super::{sanitize_text, Finding};

#[derive(Deserialize)]
pub struct SanitizeRequest {
    pub text: String,
}

#[derive(Deserialize)]
pub struct SanitizeBatchRequest {
    pub texts: Vec<String>,
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

#[derive(Serialize)]
pub struct SanitizeBatchResponse {
    /// One result per requested text, in request order. The caller matches results to its own
    /// texts by index, so this list always has the same length as the request's.
    pub results: Vec<SanitizeResponse>,
}

pub async fn sanitize(_token: APIToken, Json(request): Json<SanitizeRequest>) -> Json<SanitizeResponse> {
    let (sanitized_text, report) = sanitize_text(&request.text);

    Json(SanitizeResponse {
        sanitized_text,
        findings: report.findings,
        redacted_count: report.redacted_count,
    })
}

pub async fn sanitize_batch(
    _token: APIToken,
    Json(request): Json<SanitizeBatchRequest>,
) -> Result<Json<SanitizeBatchResponse>, (StatusCode, String)> {
    //
    // Scanning is CPU-bound, and a batch carries far more text than a single request: an entire
    // web search instead of one page. Running that on a runtime worker would stall every other
    // call the app makes meanwhile, so it goes to the blocking pool.
    //
    tokio::task::spawn_blocking(move || {
        let results = request
            .texts
            .iter()
            .map(|text| {
                let (sanitized_text, report) = sanitize_text(text);
                SanitizeResponse {
                    sanitized_text,
                    findings: report.findings,
                    redacted_count: report.redacted_count,
                }
            })
            .collect();

        Json(SanitizeBatchResponse { results })
    })
    .await
    .map_err(|error| {
        (
            StatusCode::INTERNAL_SERVER_ERROR,
            format!("The prompt injection filter failed: {error}"),
        )
    })
}