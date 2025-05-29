use std::path::PathBuf;

fn main() {
    tauri_build::build();

    let out_dir = PathBuf::from(std::env::var("OUT_DIR").unwrap());
    println!("cargo:rustc-env=OUT_DIR={}", out_dir.to_str().unwrap());

    //
    // When we are in debug mode, we want to set the current RID
    // to the current architecture. This is necessary, so that
    // the developers get the right behavior.
    //
    // We read the current OS and architecture and overwrite the
    // current RID in the metadata file (line #10).
    //
    // We have to take care of different naming conventions. The
    // following RIDs are supported: win-x64, win-arm64, linux-x64,
    // linux-arm64, osx-x64, osx-arm64.
    //
    let current_os = std::env::consts::OS;
    let current_arch = std::env::consts::ARCH;
    let rid = match (current_os, current_arch) {
        ("windows", "x86_64") => "win-x64",
        ("windows", "aarch64") => "win-arm64",

        ("linux", "x86_64") => "linux-x64",
        ("linux", "aarch64") => "linux-arm64",

        ("macos", "x86_64") => "osx-x64",
        ("macos", "aarch64") => "osx-arm64",

        _ => panic!("Unsupported OS or architecture: {current_os} {current_arch}"),
    };

    let metadata = include_str!("../metadata.txt");
    let mut metadata_lines = metadata.lines().collect::<Vec<_>>();
    metadata_lines[9] = rid;
    let new_metadata = metadata_lines.join("\n");
    std::fs::write("../metadata.txt", new_metadata).unwrap();

    //
    // Read the current version and update the
    // Rust and Tauri configuration files:
    //
    let version = metadata_lines[0];
    update_cargo_toml("Cargo.toml", version);
    update_tauri_conf("tauri.conf.json", version);
}

fn update_cargo_toml(cargo_path: &str, version: &str) {
    let cargo_toml = std::fs::read_to_string(cargo_path).unwrap();
    let cargo_toml_lines = cargo_toml.lines();
    let mut new_cargo_toml = String::new();

    for line in cargo_toml_lines {
        if line.starts_with("version = ") {
            new_cargo_toml.push_str(&format!("version = \"{version}\""));
        } else {
            new_cargo_toml.push_str(line);
        }
        new_cargo_toml.push('\n');
    }

    std::fs::write(cargo_path, new_cargo_toml).unwrap();
}

fn update_tauri_conf(tauri_conf_path: &str, version: &str) {
    let tauri_conf = std::fs::read_to_string(tauri_conf_path).unwrap();
    let tauri_conf_lines = tauri_conf.lines();
    let mut new_tauri_conf = String::new();

    for line in tauri_conf_lines {
        // The version in Tauri's config is formatted like this:
        //  "version": "0.1.0-alpha.0"
        // Please notice, that the version number line might have a leading tab, etc.
        if line.contains("\"version\": ") {
            new_tauri_conf.push_str(&format!("\t\"version\": \"{version}\""));
        } else {
            new_tauri_conf.push_str(line);
        }
        new_tauri_conf.push('\n');
    }

    std::fs::write(tauri_conf_path, new_tauri_conf).unwrap();
}
