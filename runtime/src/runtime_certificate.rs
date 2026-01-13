use std::sync::OnceLock;
use log::info;
use crate::certificate_factory::generate_certificate;

/// The certificate used for the runtime API server.
pub static CERTIFICATE: OnceLock<Vec<u8>> = OnceLock::new();

/// The private key used for the certificate of the runtime API server.
pub static CERTIFICATE_PRIVATE_KEY: OnceLock<Vec<u8>> = OnceLock::new();

/// The fingerprint of the certificate used for the runtime API server.
pub static CERTIFICATE_FINGERPRINT: OnceLock<String> = OnceLock::new();

/// Generates a TLS certificate for the runtime API server.
pub fn generate_runtime_certificate() {
    
    info!("Try to generate a TLS certificate for the runtime API server...");

    let (certificate, cer_private_key, cer_fingerprint) = generate_certificate();
    
    CERTIFICATE_FINGERPRINT.set(cer_fingerprint).expect("Could not set the certificate fingerprint.");
    CERTIFICATE.set(certificate).expect("Could not set the certificate.");
    CERTIFICATE_PRIVATE_KEY.set(cer_private_key).expect("Could not set the private key.");
    
    info!("Done generating certificate for the runtime API server.");
}