use std::sync::OnceLock;
use log::info;
use rcgen::generate_simple_self_signed;
use sha2::{Sha256, Digest};

/// The certificate used for the runtime API server.
pub static CERTIFICATE: OnceLock<Vec<u8>> = OnceLock::new();

/// The private key used for the certificate of the runtime API server.
pub static CERTIFICATE_PRIVATE_KEY: OnceLock<Vec<u8>> = OnceLock::new();

/// The fingerprint of the certificate used for the runtime API server.
pub static CERTIFICATE_FINGERPRINT: OnceLock<String> = OnceLock::new();

/// Generates a TLS certificate for the runtime API server.
pub fn generate_certificate() {
    
    info!("Try to generate a TLS certificate for the runtime API server...");
    
    let subject_alt_names = vec!["localhost".to_string()];
    let certificate_data = generate_simple_self_signed(subject_alt_names).unwrap();
    let certificate_binary_data = certificate_data.cert.der().to_vec();
    
    let certificate_fingerprint = Sha256::digest(certificate_binary_data).to_vec();
    let certificate_fingerprint = certificate_fingerprint.iter().fold(String::new(), |mut result, byte| {
        result.push_str(&format!("{:02x}", byte));
        result
    });
    
    let certificate_fingerprint = certificate_fingerprint.to_uppercase();
    
    CERTIFICATE_FINGERPRINT.set(certificate_fingerprint.clone()).expect("Could not set the certificate fingerprint.");
    CERTIFICATE.set(certificate_data.cert.pem().as_bytes().to_vec()).expect("Could not set the certificate.");
    CERTIFICATE_PRIVATE_KEY.set(certificate_data.signing_key.serialize_pem().as_bytes().to_vec()).expect("Could not set the private key.");
    
    info!("Certificate fingerprint: '{certificate_fingerprint}'.");
    info!("Done generating certificate for the runtime API server.");
}