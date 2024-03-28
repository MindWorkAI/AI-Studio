use std::path::PathBuf;

fn main() {
    tauri_build::build();
    let out_dir = PathBuf::from(std::env::var("OUT_DIR").unwrap());
    println!("cargo:rustc-env=OUT_DIR={}", out_dir.to_str().unwrap());
}
