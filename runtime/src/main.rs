// Prevents additional console window on Windows in release, DO NOT REMOVE!!
#![cfg_attr(not(debug_assertions), windows_subsystem = "windows")]

use keyring::Entry;

fn main() {
    tauri::Builder::default()
        .invoke_handler(tauri::generate_handler![store_secret, get_secret])
        .run(tauri::generate_context!())
        .expect("error while running tauri application");
}

#[tauri::command]
fn store_secret(destination: String, user_name: String, secret: String) {
    let entry = Entry::new(&format!("mindwork-ai-studio::{}", destination), user_name.as_str()).unwrap();
    entry.set_password(secret.as_str()).unwrap();
}

#[tauri::command]
fn get_secret(destination: String, user_name: String) -> String {
    let entry = Entry::new(&format!("mindwork-ai-studio::{}", destination), user_name.as_str()).unwrap();
    entry.get_password().unwrap()
}