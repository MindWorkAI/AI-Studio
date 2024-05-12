// Prevents an additional console window on Windows in release, DO NOT REMOVE!!
#![cfg_attr(not(debug_assertions), windows_subsystem = "windows")]

use arboard::Clipboard;
use keyring::Entry;
use serde::Serialize;

fn main() {
    tauri::Builder::default()
        .invoke_handler(tauri::generate_handler![store_secret, get_secret, delete_secret, set_clipboard])
        .run(tauri::generate_context!())
        .expect("error while running tauri application");
}

#[tauri::command]
fn store_secret(destination: String, user_name: String, secret: String) -> StoreSecretResponse {
    let entry = Entry::new(&format!("mindwork-ai-studio::{}", destination), user_name.as_str()).unwrap();
    let result = entry.set_password(secret.as_str());
    match result {
        Ok(_) => StoreSecretResponse {
            success: true,
            issue: String::from(""),
        },
        
        Err(e) => StoreSecretResponse {
            success: false,
            issue: e.to_string(),
        },
    }
}

#[derive(Serialize)]
struct StoreSecretResponse {
    success: bool,
    issue: String,
}

#[tauri::command]
fn get_secret(destination: String, user_name: String) -> RequestedSecret {
    let entry = Entry::new(&format!("mindwork-ai-studio::{}", destination), user_name.as_str()).unwrap();
    let secret = entry.get_password();
    match secret {
        Ok(s) => RequestedSecret {
            success: true,
            secret: s,
            issue: String::from(""),
        },

        Err(e) => RequestedSecret {
            success: false,
            secret: String::from(""),
            issue: e.to_string(),
        },
    }
}

#[derive(Serialize)]
struct RequestedSecret {
    success: bool,
    secret: String,
    issue: String,
}

#[tauri::command]
fn delete_secret(destination: String, user_name: String) -> DeleteSecretResponse {
    let entry = Entry::new(&format!("mindwork-ai-studio::{}", destination), user_name.as_str()).unwrap();
    let result = entry.delete_password();
    match result {
        Ok(_) => DeleteSecretResponse {
            success: true,
            issue: String::from(""),
        },
        
        Err(e) => DeleteSecretResponse {
            success: false,
            issue: e.to_string(),
        },
    }
}

#[derive(Serialize)]
struct DeleteSecretResponse {
    success: bool,
    issue: String,
}

#[tauri::command]
fn set_clipboard(text: String) -> SetClipboardResponse {
    let clipboard_result = Clipboard::new();
    let mut clipboard = match clipboard_result {
        Ok(clipboard) => clipboard,
        Err(e) => return SetClipboardResponse {
            success: false,
            issue: e.to_string(),
        },
    };
    
    let set_text_result = clipboard.set_text(text);
    match set_text_result {
        Ok(_) => SetClipboardResponse {
            success: true,
            issue: String::from(""),
        },
        
        Err(e) => SetClipboardResponse {
            success: false,
            issue: e.to_string(),
        },
    }
}

#[derive(Serialize)]
struct SetClipboardResponse {
    success: bool,
    issue: String,
}