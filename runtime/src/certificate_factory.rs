use log::info;
use rcgen::generate_simple_self_signed;
use sha2::{Sha256, Digest};

pub fn generate_certificate() -> (Vec<u8>, Vec<u8>, String) {
    
    let subject_alt_names = vec!["localhost".to_string()];
    let certificate_data = generate_simple_self_signed(subject_alt_names).unwrap();
    let certificate_binary_data = certificate_data.cert.der().to_vec();

    let certificate_fingerprint = Sha256::digest(certificate_binary_data).to_vec();
    let certificate_fingerprint = certificate_fingerprint.iter().fold(String::new(), |mut result, byte| {
        result.push_str(&format!("{:02x}", byte));
        result
    });

    let certificate_fingerprint = certificate_fingerprint.to_uppercase();
    
    info!("Certificate fingerprint: '{certificate_fingerprint}'.");
    
    (certificate_data.cert.pem().as_bytes().to_vec(), certificate_data.signing_key.serialize_pem().as_bytes().to_vec(), certificate_fingerprint.clone())
}