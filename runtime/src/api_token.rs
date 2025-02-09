use log::info;
use once_cell::sync::Lazy;
use rand::{RngCore, SeedableRng};
use rocket::http::Status;
use rocket::Request;
use rocket::request::FromRequest;

/// The API token used to authenticate requests.
pub static API_TOKEN: Lazy<APIToken> = Lazy::new(|| {
    let mut token = [0u8; 32];
    let mut rng = rand_chacha::ChaChaRng::from_os_rng();
    rng.fill_bytes(&mut token);
    
    let token = APIToken::from_bytes(token.to_vec());
    info!("API token was generated successfully.");
    
    token
});

/// The API token data structure used to authenticate requests.
pub struct APIToken {
    hex_text: String,
}

impl APIToken {
    /// Creates a new API token from a byte vector.
    fn from_bytes(bytes: Vec<u8>) -> Self {
        APIToken {
            hex_text: bytes.iter().fold(String::new(), |mut result, byte| {
                result.push_str(&format!("{:02x}", byte));
                result
            }),
        }
    }

    /// Creates a new API token from a hexadecimal text.
    fn from_hex_text(hex_text: &str) -> Self {
        APIToken {
            hex_text: hex_text.to_string(),
        }
    }

    pub(crate) fn to_hex_text(&self) -> &str {
        self.hex_text.as_str()
    }

    /// Validates the received token against the valid token.
    fn validate(&self, received_token: &Self) -> bool {
        received_token.to_hex_text() == self.to_hex_text()
    }
}

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