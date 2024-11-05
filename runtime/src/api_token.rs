use once_cell::sync::Lazy;
use rand::{RngCore, SeedableRng};
use rocket::http::Status;
use rocket::Request;
use rocket::request::FromRequest;

pub static API_TOKEN: Lazy<APIToken> = Lazy::new(|| {
    let mut token = [0u8; 32];
    let mut rng = rand_chacha::ChaChaRng::from_entropy();
    rng.fill_bytes(&mut token);
    APIToken::from_bytes(token.to_vec())
});

pub struct APIToken{
    hex_text: String,
}

impl APIToken {
    fn from_bytes(bytes: Vec<u8>) -> Self {
        APIToken {
            hex_text: bytes.iter().fold(String::new(), |mut result, byte| {
                result.push_str(&format!("{:02x}", byte));
                result
            }),
        }
    }

    fn from_hex_text(hex_text: &str) -> Self {
        APIToken {
            hex_text: hex_text.to_string(),
        }
    }

    pub(crate) fn to_hex_text(&self) -> &str {
        self.hex_text.as_str()
    }

    fn validate(&self, received_token: &Self) -> bool {
        received_token.to_hex_text() == self.to_hex_text()
    }
}

type RequestOutcome<R, T> = rocket::request::Outcome<R, T>;

#[rocket::async_trait]
impl<'r> FromRequest<'r> for APIToken {
    type Error = APITokenError;

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

#[derive(Debug)]
enum APITokenError {
    Missing,
    Invalid,
}