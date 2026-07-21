use axum::Json;
use keyring_core::{Entry, Error as KeyringError};
use log::{error, info, warn};
use serde::{Deserialize, Serialize};
use crate::api_token::APIToken;
use crate::encryption::{EncryptedText, ENCRYPTION};

/// A structured issue reported by the native credential store.
#[derive(Clone, Copy, Debug, PartialEq, Serialize)]
pub enum SecretStoreIssueCode {
    None,
    SecretNotFound,
    NoDefaultCollection,
    CollectionLocked,
    PromptDismissed,
    ServiceUnavailable,
    Unknown,
}

fn issue_code(error: &KeyringError) -> SecretStoreIssueCode {
    if matches!(error, KeyringError::NoEntry) {
        return SecretStoreIssueCode::SecretNotFound;
    }

    #[cfg(target_os = "linux")]
    if let KeyringError::PlatformFailure(error) | KeyringError::NoStorageAccess(error) = error {
        if let Some(error) = error.downcast_ref::<dbus_secret_service::Error>() {
            return secret_service_issue_code(error);
        }
    }

    SecretStoreIssueCode::Unknown
}

#[cfg(target_os = "linux")]
fn secret_service_issue_code(error: &dbus_secret_service::Error) -> SecretStoreIssueCode {
    use dbus_secret_service::Error;

    match error {
        Error::NoResult => SecretStoreIssueCode::NoDefaultCollection,
        Error::Locked => SecretStoreIssueCode::CollectionLocked,
        Error::Prompt => SecretStoreIssueCode::PromptDismissed,
        Error::Unavailable => SecretStoreIssueCode::ServiceUnavailable,
        _ => SecretStoreIssueCode::Unknown,
    }
}

/// Initializes the native credential store used by keyring-core.
pub fn init_secret_store() {
    cfg_if::cfg_if! {
        if #[cfg(target_os = "macos")] {
            match apple_native_keyring_store::keychain::Store::new() {
                Ok(store) => {
                    keyring_core::set_default_store(store);
                    info!(Source = "Secret Store"; "Initialized the macOS Keychain credential store.");
                },
                Err(e) => error!(Source = "Secret Store"; "Failed to initialize the macOS Keychain credential store: {e}."),
            }
        } else if #[cfg(target_os = "windows")] {
            match windows_native_keyring_store::Store::new() {
                Ok(store) => {
                    keyring_core::set_default_store(store);
                    info!(Source = "Secret Store"; "Initialized the Windows Credential Manager store.");
                },
                Err(e) => error!(Source = "Secret Store"; "Failed to initialize the Windows Credential Manager store: {e}."),
            }
        } else if #[cfg(target_os = "linux")] {
            match dbus_secret_service_keyring_store::Store::new() {
                Ok(store) => {
                    keyring_core::set_default_store(store);
                    info!(Source = "Secret Store"; "Initialized the DBus Secret Service credential store.");
                },
                Err(e) => error!(Source = "Secret Store"; "Failed to initialize the DBus Secret Service credential store: {e}."),
            }
        } else {
            warn!(Source = "Secret Store"; "No native credential store is configured for this platform.");
        }
    }
}

/// Stores a secret in the secret store using the operating system's keyring.
pub async fn store_secret(_token: APIToken, request: Json<StoreSecret>) -> Json<StoreSecretResponse> {
    let user_name = request.user_name.as_str();
    let decrypted_text = match ENCRYPTION.decrypt(&request.secret) {
        Ok(text) => text,
        Err(e) => {
            error!(Source = "Secret Store"; "Failed to decrypt the text: {e}.");
            return Json(StoreSecretResponse {
                success: false,
                issue: format!("Failed to decrypt the text: {e}"),
                issue_code: SecretStoreIssueCode::Unknown,
            })
        },
    };

    let service = format!("mindwork-ai-studio::{}", request.destination);
    let entry = match Entry::new(service.as_str(), user_name) {
        Ok(entry) => entry,
        Err(e) => {
            error!(Source = "Secret Store"; "Failed to create secret entry for {service} and user {user_name}: {e}.");
            return Json(StoreSecretResponse {
                success: false,
                issue: e.to_string(),
                issue_code: issue_code(&e),
            });
        },
    };
    let result = entry.set_password(decrypted_text.as_str());
    match result {
        Ok(_) => {
            info!(Source = "Secret Store"; "Secret for {service} and user {user_name} was stored successfully.");
            Json(StoreSecretResponse {
                success: true,
                issue: String::from(""),
                issue_code: SecretStoreIssueCode::None,
            })
        },

        Err(e) => {
            error!(Source = "Secret Store"; "Failed to store secret for {service} and user {user_name}: {e}.");
            Json(StoreSecretResponse {
                success: false,
                issue: e.to_string(),
                issue_code: issue_code(&e),
            })
        },
    }
}

/// The structure of the request to store a secret.
#[derive(Deserialize)]
pub struct StoreSecret {
    destination: String,
    user_name: String,
    secret: EncryptedText,
}

/// The structure of the response to storing a secret.
#[derive(Serialize)]
pub struct StoreSecretResponse {
    success: bool,
    issue: String,
    issue_code: SecretStoreIssueCode,
}

/// Retrieves a secret from the secret store using the operating system's keyring.
pub async fn get_secret(_token: APIToken, request: Json<RequestSecret>) -> Json<RequestedSecret> {
    let user_name = request.user_name.as_str();
    let service = format!("mindwork-ai-studio::{}", request.destination);
    let entry = match Entry::new(service.as_str(), user_name) {
        Ok(entry) => entry,
        Err(e) => {
            if !request.is_trying {
                error!(Source = "Secret Store"; "Failed to create secret entry for '{service}' and user '{user_name}': {e}.");
            }

            return Json(RequestedSecret {
                success: false,
                secret: EncryptedText::new(String::from("")),
                issue: format!("Failed to create secret entry for '{service}' and user '{user_name}': {e}"),
                issue_code: issue_code(&e),
            });
        },
    };
    let secret = entry.get_password();
    match secret {
        Ok(s) => {
            info!(Source = "Secret Store"; "Secret for '{service}' and user '{user_name}' was retrieved successfully.");

            // Encrypt the secret:
            let encrypted_secret = match ENCRYPTION.encrypt(s.as_str()) {
                Ok(e) => e,
                Err(e) => {
                    error!(Source = "Secret Store"; "Failed to encrypt the secret: {e}.");
                    return Json(RequestedSecret {
                        success: false,
                        secret: EncryptedText::new(String::from("")),
                        issue: format!("Failed to encrypt the secret: {e}"),
                        issue_code: SecretStoreIssueCode::Unknown,
                    });
                },
            };

            Json(RequestedSecret {
                success: true,
                secret: encrypted_secret,
                issue: String::from(""),
                issue_code: SecretStoreIssueCode::None,
            })
        },

        Err(e) => {
            if !request.is_trying {
                error!(Source = "Secret Store"; "Failed to retrieve secret for '{service}' and user '{user_name}': {e}.");
            }

            Json(RequestedSecret {
                success: false,
                secret: EncryptedText::new(String::from("")),
                issue: format!("Failed to retrieve secret for '{service}' and user '{user_name}': {e}"),
                issue_code: issue_code(&e),
            })
        },
    }
}

/// The structure of the request to retrieve a secret.
#[derive(Deserialize)]
pub struct RequestSecret {
    destination: String,
    user_name: String,
    is_trying: bool,
}

/// The structure of the response to retrieving a secret.
#[derive(Serialize)]
pub struct RequestedSecret {
    success: bool,
    secret: EncryptedText,
    issue: String,
    issue_code: SecretStoreIssueCode,
}

/// Deletes a secret from the secret store using the operating system's keyring.
pub async fn delete_secret(_token: APIToken, request: Json<RequestSecret>) -> Json<DeleteSecretResponse> {
    let user_name = request.user_name.as_str();
    let service = format!("mindwork-ai-studio::{}", request.destination);
    let entry = match Entry::new(service.as_str(), user_name) {
        Ok(entry) => entry,
        Err(e) => {
            error!(Source = "Secret Store"; "Failed to create secret entry for {service} and user {user_name}: {e}.");
            return Json(DeleteSecretResponse {
                success: false,
                was_entry_found: false,
                issue: e.to_string(),
                issue_code: issue_code(&e),
            });
        },
    };
    let result = entry.delete_credential();

    match result {
        Ok(_) => {
            warn!(Source = "Secret Store"; "Secret for {service} and user {user_name} was deleted successfully.");
            Json(DeleteSecretResponse {
                success: true,
                was_entry_found: true,
                issue: String::from(""),
                issue_code: SecretStoreIssueCode::None,
            })
        },

        Err(KeyringError::NoEntry) => {
            warn!(Source = "Secret Store"; "No secret for {service} and user {user_name} was found.");
            Json(DeleteSecretResponse {
                success: true,
                was_entry_found: false,
                issue: String::from(""),
                issue_code: SecretStoreIssueCode::SecretNotFound,
            })
        }

        Err(e) => {
            error!(Source = "Secret Store"; "Failed to delete secret for {service} and user {user_name}: {e}.");
            Json(DeleteSecretResponse {
                success: false,
                was_entry_found: false,
                issue: e.to_string(),
                issue_code: issue_code(&e),
            })
        },
    }
}

/// The structure of the response to deleting a secret.
#[derive(Serialize)]
pub struct DeleteSecretResponse {
    success: bool,
    was_entry_found: bool,
    issue: String,
    issue_code: SecretStoreIssueCode,
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn no_entry_is_reported_as_secret_not_found() {
        assert_eq!(issue_code(&KeyringError::NoEntry), SecretStoreIssueCode::SecretNotFound);
    }

    #[test]
    fn unrelated_keyring_error_uses_unknown_fallback() {
        let error = KeyringError::Invalid("service".to_string(), "invalid".to_string());
        assert_eq!(issue_code(&error), SecretStoreIssueCode::Unknown);
    }

    #[test]
    fn issue_code_is_included_in_json() {
        let response = StoreSecretResponse {
            success: false,
            issue: "technical details".to_string(),
            issue_code: SecretStoreIssueCode::NoDefaultCollection,
        };
        let json = serde_json::to_value(response).unwrap();

        assert_eq!(json["issue_code"], "NoDefaultCollection");
        assert_eq!(json["issue"], "technical details");
    }

    #[cfg(target_os = "linux")]
    #[test]
    fn secret_service_errors_are_mapped_to_issue_codes() {
        use dbus_secret_service::Error;

        assert_eq!(secret_service_issue_code(&Error::NoResult), SecretStoreIssueCode::NoDefaultCollection);
        assert_eq!(secret_service_issue_code(&Error::Locked), SecretStoreIssueCode::CollectionLocked);
        assert_eq!(secret_service_issue_code(&Error::Prompt), SecretStoreIssueCode::PromptDismissed);
        assert_eq!(secret_service_issue_code(&Error::Unavailable), SecretStoreIssueCode::ServiceUnavailable);
        assert_eq!(secret_service_issue_code(&Error::Parse), SecretStoreIssueCode::Unknown);
    }
}