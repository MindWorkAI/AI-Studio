use once_cell::sync::Lazy;
use rocket::http::Status;
use rocket::Request;
use rocket::request::FromRequest;
use crate::api_token::{generate_api_token, APIToken};

pub static API_TOKEN: Lazy<APIToken> = Lazy::new(|| generate_api_token());

/// The request outcome type used to handle API token requests.
type RequestOutcome<R, T> = rocket::request::Outcome<R, T>;

/// The request outcome implementation for the API token.
#[rocket::async_trait]
impl<'r> FromRequest<'r> for APIToken {
    type Error = APITokenError;

    /// Handles the API token requests.
    async fn from_request(request: &'r Request<'_>) -> RequestOutcome<Self, Self::Error> {
        let token = request.headers().get_one("token");
        match token {
            Some(token) => {
                let received_token = APIToken::from_hex_text(token);
                if API_TOKEN.validate(&received_token) {
                    RequestOutcome::Success(received_token)
                } else {
                    RequestOutcome::Error((Status::Unauthorized, APITokenError::Invalid))
                }
            }

            None => RequestOutcome::Error((Status::Unauthorized, APITokenError::Missing)),
        }
    }
}

/// The API token error types.
#[derive(Debug)]
pub enum APITokenError {
    Missing,
    Invalid,
}