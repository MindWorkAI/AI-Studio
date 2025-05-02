use std::sync::Mutex;
use once_cell::sync::Lazy;

pub static META_DATA: Lazy<Mutex<Option<MetaData>>> = Lazy::new(|| Mutex::new(None));

#[derive(Clone)]
pub struct MetaData {
    pub app_version: String,
    pub build_time: String,
    pub build_number: String,
    pub dotnet_sdk_version: String,
    pub dotnet_version: String,
    pub rust_version: String,
    pub mud_blazor_version: String,
    pub tauri_version: String,
    pub app_commit_hash: String,
    pub architecture: String,
}