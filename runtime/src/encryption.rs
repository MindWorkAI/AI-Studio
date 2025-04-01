use std::fmt;
use std::time::Instant;
use base64::Engine;
use base64::prelude::BASE64_STANDARD;
use aes::cipher::{block_padding::Pkcs7, BlockDecryptMut, BlockEncryptMut, KeyIvInit};
use hmac::Hmac;
use log::info;
use once_cell::sync::Lazy;
use pbkdf2::pbkdf2;
use rand::{RngCore, SeedableRng};
use rocket::{data, Data, Request};
use rocket::data::ToByteUnit;
use rocket::http::Status;
use rocket::serde::{Deserialize, Serialize};
use sha2::Sha512;
use tokio::io::AsyncReadExt;

type Aes256CbcEnc = cbc::Encryptor<aes::Aes256>;

type Aes256CbcDec = cbc::Decryptor<aes::Aes256>;

type DataOutcome<'r, T> = data::Outcome<'r, T>;

/// The encryption instance used for the IPC channel.
pub static ENCRYPTION: Lazy<Encryption> = Lazy::new(|| {
    //
    // Generate a secret key & salt for the AES encryption for the IPC channel:
    //
    let mut secret_key = [0u8; 512]; // 512 bytes = 4096 bits
    let mut secret_key_salt = [0u8; 16]; // 16 bytes = 128 bits

    // We use a cryptographically secure pseudo-random number generator
    // to generate the secret password & salt. ChaCha20Rng is the algorithm
    // of our choice:
    let mut rng = rand_chacha::ChaChaRng::from_os_rng();

    // Fill the secret key & salt with random bytes:
    rng.fill_bytes(&mut secret_key);
    rng.fill_bytes(&mut secret_key_salt);

    info!("Secret password for the IPC channel was generated successfully.");
    Encryption::new(&secret_key, &secret_key_salt).unwrap()
});

/// The encryption struct used for the IPC channel.
pub struct Encryption {
    key: [u8; 32],
    iv: [u8; 16],

    pub secret_password: [u8; 512],
    pub secret_key_salt: [u8; 16],
}

impl Encryption {
    // The number of iterations to derive the key and IV from the password. For a password
    // manager where the user has to enter their primary password, 100 iterations would be
    // too few and insecure. Here, the use case is different: We generate a 512-byte long
    // and cryptographically secure password at every start. This password already contains
    // enough entropy. In our case, we need key and IV primarily because AES, with the
    // algorithms we chose, requires a fixed key length, and our password is too long.
    const ITERATIONS: u32 = 100;

    /// Initializes the encryption with the given secret password and salt.
    pub fn new(secret_password: &[u8], secret_key_salt: &[u8]) -> Result<Self, String> {
        if secret_password.len() != 512 {
            return Err("The secret password must be 512 bytes long.".to_string());
        }

        if secret_key_salt.len() != 16 {
            return Err("The salt must be 16 bytes long.".to_string());
        }

        info!(Source = "Encryption"; "Initializing encryption...");
        let mut encryption = Encryption {
            key: [0u8; 32],
            iv: [0u8; 16],

            secret_password: [0u8; 512],
            secret_key_salt: [0u8; 16],
        };

        encryption.secret_password.copy_from_slice(secret_password);
        encryption.secret_key_salt.copy_from_slice(secret_key_salt);

        let start = Instant::now();
        let mut key_iv = [0u8; 48];
        pbkdf2::<Hmac<Sha512>>(secret_password, secret_key_salt, Self::ITERATIONS, &mut key_iv).map_err(|e| format!("Error while generating key and IV: {e}"))?;
        encryption.key.copy_from_slice(&key_iv[0..32]);
        encryption.iv.copy_from_slice(&key_iv[32..48]);

        let duration = start.elapsed();
        let duration = duration.as_millis();
        info!(Source = "Encryption"; "Encryption initialized in {duration} milliseconds.", );

        Ok(encryption)
    }

    /// Encrypts the given data.
    pub fn encrypt(&self, data: &str) -> Result<EncryptedText, String> {
        let cipher = Aes256CbcEnc::new(&self.key.into(), &self.iv.into());
        let encrypted = cipher.encrypt_padded_vec_mut::<Pkcs7>(data.as_bytes());
        let mut result = BASE64_STANDARD.encode(self.secret_key_salt);
        result.push_str(&BASE64_STANDARD.encode(&encrypted));
        Ok(EncryptedText::new(result))
    }

    /// Decrypts the given data.
    pub fn decrypt(&self, encrypted_data: &EncryptedText) -> Result<String, String> {
        let decoded = BASE64_STANDARD.decode(encrypted_data.get_encrypted()).map_err(|e| format!("Error decoding base64: {e}"))?;

        if decoded.len() < 16 {
            return Err("Encrypted data is too short.".to_string());
        }

        let (salt, encrypted) = decoded.split_at(16);
        if salt != self.secret_key_salt {
            return Err("The salt bytes do not match. The data is corrupted or tampered.".to_string());
        }

        let cipher = Aes256CbcDec::new(&self.key.into(), &self.iv.into());
        let decrypted = cipher.decrypt_padded_vec_mut::<Pkcs7>(encrypted).map_err(|e| format!("Error decrypting data: {e}"))?;

        String::from_utf8(decrypted).map_err(|e| format!("Error converting decrypted data to string: {}", e))
    }
}

/// Represents encrypted text.
#[derive(Clone, Serialize, Deserialize)]
pub struct EncryptedText(String);

impl EncryptedText {
    
    /// Creates a new encrypted text instance.
    pub fn new(encrypted_data: String) -> Self {
        EncryptedText(encrypted_data)
    }

    /// Returns the encrypted data.
    pub fn get_encrypted(&self) -> &str {
        &self.0
    }
}

impl fmt::Debug for EncryptedText {
    fn fmt(&self, f: &mut fmt::Formatter<'_>) -> fmt::Result {
        write!(f, "EncryptedText(**********)")
    }
}

impl fmt::Display for EncryptedText {
    fn fmt(&self, f: &mut fmt::Formatter<'_>) -> fmt::Result {
        write!(f, "**********")
    }
}

/// Use Case: When we receive encrypted text from the client as body (e.g., in a POST request).
/// We must interpret the body as EncryptedText.
#[rocket::async_trait]
impl<'r> data::FromData<'r> for EncryptedText {
    type Error = String;
    
    /// Parses the data as EncryptedText.
    async fn from_data(req: &'r Request<'_>, data: Data<'r>) -> DataOutcome<'r, Self> {
        let content_type = req.content_type();
        if content_type.map_or(true, |ct| !ct.is_text()) {
            return DataOutcome::Forward((data, Status::Ok));
        }

        let mut stream = data.open(2.mebibytes());
        let mut body = String::new();
        if let Err(e) = stream.read_to_string(&mut body).await {
            return DataOutcome::Error((Status::InternalServerError, format!("Failed to read data: {}", e)));
        }

        DataOutcome::Success(EncryptedText(body))
    }
}