use std::sync::OnceLock;
use rocket::get;
use crate::api_token::APIToken;

pub static DATA_DIRECTORY: OnceLock<String> = OnceLock::new();

pub static CONFIG_DIRECTORY: OnceLock<String> = OnceLock::new();

#[get("/system/directories/config")]
pub fn get_config_directory(_token: APIToken) -> String {
    match CONFIG_DIRECTORY.get() {
        Some(config_directory) => config_directory.clone(),
        None => String::from(""),
    }
}

#[get("/system/directories/data")]
pub fn get_data_directory(_token: APIToken) -> String {
    match DATA_DIRECTORY.get() {
        Some(data_directory) => data_directory.clone(),
        None => String::from(""),
    }
}

pub fn is_dev() -> bool {
    cfg!(debug_assertions)
}

pub fn is_prod() -> bool {
    !is_dev()
}