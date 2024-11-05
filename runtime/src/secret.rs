use keyring::Entry;
use log::{error, info, warn};
use rocket::post;
use rocket::serde::json::Json;
use serde::{Deserialize, Serialize};
use keyring::error::Error::NoEntry;
use crate::api_token::APIToken;
use crate::encryption::{EncryptedText, ENCRYPTION};

/// Stores a secret in the secret store using the operating system's keyring.
#[post("/secrets/store", data = "<request>")]
pub fn store_secret(_token: APIToken, request: Json<StoreSecret>) -> Json<StoreSecretResponse> {
    let user_name = request.user_name.as_str();
    let decrypted_text = match ENCRYPTION.decrypt(&request.secret) {
        Ok(text) => text,
        Err(e) => {
            error!(Source = "Secret Store"; "Failed to decrypt the text: {e}.");
            return Json(StoreSecretResponse {
                success: false,
                issue: format!("Failed to decrypt the text: {e}"),
            })
        },
    };

    let service = format!("mindwork-ai-studio::{}", request.destination);
    let entry = Entry::new(service.as_str(), user_name).unwrap();
    let result = entry.set_password(decrypted_text.as_str());
    match result {
        Ok(_) => {
            info!(Source = "Secret Store"; "Secret for {service} and user {user_name} was stored successfully.");
            Json(StoreSecretResponse {
                success: true,
                issue: String::from(""),
            })
        },

        Err(e) => {
            error!(Source = "Secret Store"; "Failed to store secret for {service} and user {user_name}: {e}.");
            Json(StoreSecretResponse {
                success: false,
                issue: e.to_string(),
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
}

/// Retrieves a secret from the secret store using the operating system's keyring.
#[post("/secrets/get", data = "<request>")]
pub fn get_secret(_token: APIToken, request: Json<RequestSecret>) -> Json<RequestedSecret> {
    let user_name = request.user_name.as_str();
    let service = format!("mindwork-ai-studio::{}", request.destination);
    let entry = Entry::new(service.as_str(), user_name).unwrap();
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
                    });
                },
            };

            Json(RequestedSecret {
                success: true,
                secret: encrypted_secret,
                issue: String::from(""),
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
}

/// Deletes a secret from the secret store using the operating system's keyring.
#[post("/secrets/delete", data = "<request>")]
pub fn delete_secret(_token: APIToken, request: Json<RequestSecret>) -> Json<DeleteSecretResponse> {
    let user_name = request.user_name.as_str();
    let service = format!("mindwork-ai-studio::{}", request.destination);
    let entry = Entry::new(service.as_str(), user_name).unwrap();
    let result = entry.delete_credential();

    match result {
        Ok(_) => {
            warn!(Source = "Secret Store"; "Secret for {service} and user {user_name} was deleted successfully.");
            Json(DeleteSecretResponse {
                success: true,
                was_entry_found: true,
                issue: String::from(""),
            })
        },

        Err(NoEntry) => {
            warn!(Source = "Secret Store"; "No secret for {service} and user {user_name} was found.");
            Json(DeleteSecretResponse {
                success: true,
                was_entry_found: false,
                issue: String::from(""),
            })
        }

        Err(e) => {
            error!(Source = "Secret Store"; "Failed to delete secret for {service} and user {user_name}: {e}.");
            Json(DeleteSecretResponse {
                success: false,
                was_entry_found: false,
                issue: e.to_string(),
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
}