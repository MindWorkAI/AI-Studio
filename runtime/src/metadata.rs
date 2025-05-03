use once_cell::sync::Lazy;
use std::sync::Mutex;

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
    pub pdfium_version: String,
}

impl MetaData {
    /// Creates a new instance of `MetaData` from the given metadata string.
    pub fn init_from_string(metadata: &str) -> Self {
        
        // When the metadata is already initialized, return the existing instance:
        if let Some(metadata) = META_DATA.lock().unwrap().as_ref() {
            return metadata.clone();
        }
        
        let mut metadata_lines = metadata.lines();
        let app_version = metadata_lines.next().unwrap();
        let build_time = metadata_lines.next().unwrap();
        let build_number = metadata_lines.next().unwrap();
        let dotnet_sdk_version = metadata_lines.next().unwrap();
        let dotnet_version = metadata_lines.next().unwrap();
        let rust_version = metadata_lines.next().unwrap();
        let mud_blazor_version = metadata_lines.next().unwrap();
        let tauri_version = metadata_lines.next().unwrap();
        let app_commit_hash = metadata_lines.next().unwrap();
        let architecture = metadata_lines.next().unwrap();
        let pdfium_version = metadata_lines.next().unwrap();

        let metadata = MetaData {
            architecture: architecture.to_string(),
            app_commit_hash: app_commit_hash.to_string(),
            app_version: app_version.to_string(),
            build_number: build_number.to_string(),
            build_time: build_time.to_string(),
            dotnet_sdk_version: dotnet_sdk_version.to_string(),
            dotnet_version: dotnet_version.to_string(),
            mud_blazor_version: mud_blazor_version.to_string(),
            rust_version: rust_version.to_string(),
            tauri_version: tauri_version.to_string(),
            pdfium_version: pdfium_version.to_string(),
        };

        *META_DATA.lock().unwrap() = Some(metadata.clone());
        metadata
    }
}