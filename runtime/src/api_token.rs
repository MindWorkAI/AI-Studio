use rand::{RngCore, SeedableRng};
use rand_chacha::ChaChaRng;

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
    pub fn from_hex_text(hex_text: &str) -> Self {
        APIToken {
            hex_text: hex_text.to_string(),
        }
    }

    pub(crate) fn to_hex_text(&self) -> &str {
        self.hex_text.as_str()
    }

    /// Validates the received token against the valid token.
    pub fn validate(&self, received_token: &Self) -> bool {
        received_token.to_hex_text() == self.to_hex_text()
    }
}

pub fn generate_api_token() -> APIToken {
    let mut token = [0u8; 32];
    let mut rng = ChaChaRng::from_os_rng();
    rng.fill_bytes(&mut token);
    APIToken::from_bytes(token.to_vec())
}