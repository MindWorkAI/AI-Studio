use log::error;
use rand::rngs::SysRng;
use rand::{Rng, SeedableRng};
use rand_chacha::ChaChaRng;
use subtle::ConstantTimeEq;

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
    ///
    /// The comparison takes the same time no matter where the two tokens differ. A comparison
    /// which stops at the first difference would let the time of each refusal tell how much of a
    /// guess was right.
    pub fn validate(&self, received_token: &Self) -> bool {
        received_token.to_hex_text().as_bytes().ct_eq(self.to_hex_text().as_bytes()).into()
    }
}

pub fn generate_api_token() -> APIToken {
    let mut token = [0u8; 32];

    // The API token authenticates privileged runtime requests. If the OS-backed
    // RNG cannot provide a secure seed, we abort instead of using a weaker RNG
    // because a predictable token would silently break the app's security model.
    let mut sys_rng = SysRng;
    let mut rng = ChaChaRng::try_from_rng(&mut sys_rng)
        .unwrap_or_else(|e| {
            error!(Source = "API Token"; "Failed to seed ChaChaRng from SysRng: {e}");
            panic!("Failed to seed ChaChaRng from SysRng: {e}");
        });
    rng.fill_bytes(&mut token);
    APIToken::from_bytes(token.to_vec())
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn validate_accepts_only_the_identical_token() {
        let valid_token = APIToken::from_hex_text("0123456789abcdef");

        assert!(valid_token.validate(&APIToken::from_hex_text("0123456789abcdef")));
        assert!(!valid_token.validate(&APIToken::from_hex_text("0123456789abcdee")));
        assert!(!valid_token.validate(&APIToken::from_hex_text("1123456789abcdef")));
        assert!(!valid_token.validate(&APIToken::from_hex_text("0123456789abcde")));
        assert!(!valid_token.validate(&APIToken::from_hex_text("0123456789abcdef0")));
        assert!(!valid_token.validate(&APIToken::from_hex_text("")));
    }

    #[test]
    fn a_generated_token_validates_against_its_own_text() {
        let token = generate_api_token();

        assert_eq!(token.to_hex_text().len(), 64);
        assert!(token.validate(&APIToken::from_hex_text(token.to_hex_text())));
    }
}