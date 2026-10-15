use std::path::{PathBuf};

fn main() {
    tauri_build::build();

    let out_dir = PathBuf::from(std::env::var("OUT_DIR").unwrap());
    println!("cargo:rustc-env=OUT_DIR={}", out_dir.to_str().unwrap());

    //
    // The runtime reports the platform it is built for. Cargo runs this
    // build script on the machine which builds, so std::env::consts would
    // name that machine. Both differ when we cross-compile, e.g., for
    // Intel-based Macs on an ARM-based one. Hence, we read the target
    // from Cargo and pass its RID on to the compiler.
    //
    // We have to take care of different naming conventions. The
    // following RIDs are supported: win-x64, win-arm64, linux-x64,
    // linux-arm64, osx-x64, osx-arm64.
    //
    let target_os = std::env::var("CARGO_CFG_TARGET_OS").unwrap();
    let target_arch = std::env::var("CARGO_CFG_TARGET_ARCH").unwrap();
    let rid = match (target_os.as_str(), target_arch.as_str()) {
        ("windows", "x86_64") => "win-x64",
        ("windows", "aarch64") => "win-arm64",

        ("linux", "x86_64") => "linux-x64",
        ("linux", "aarch64") => "linux-arm64",

        ("macos", "x86_64") => "osx-x64",
        ("macos", "aarch64") => "osx-arm64",

        _ => panic!("Unsupported OS or architecture: {target_os} {target_arch}"),
    };

    println!("cargo:rustc-env=AI_STUDIO_ARCHITECTURE={rid}");

    //
    // Read the current version and update the
    // Rust and Tauri configuration files:
    //
    let metadata = include_str!("../metadata.txt");
    let version = metadata.lines().next().unwrap();
    update_cargo_toml("Cargo.toml", version);
    update_tauri_conf("tauri.conf.json", version);
}

fn update_cargo_toml(cargo_path: &str, version: &str) {
    let cargo_toml = std::fs::read_to_string(cargo_path).unwrap();
    let cargo_toml_lines = cargo_toml.lines();
    let mut new_cargo_toml = String::new();

    // Return early when the version already matches to avoid unnecessary rewrites.
    let current_version = cargo_toml.lines().find_map(|line| {
        let trimmed = line.trim_start();
        let rest = trimmed.strip_prefix("\"version\": ")?;
        let quoted = rest.strip_prefix('"')?;
        let end_idx = quoted.find('"')?;
        Some(&quoted[..end_idx])
    });
    if current_version == Some(version) {
        return;
    }

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

    // Return early when the version already matches to avoid unnecessary rewrites.
    let current_version = tauri_conf.lines().find_map(|line| {
        let trimmed = line.trim_start();
        let rest = trimmed.strip_prefix("\"version\": ")?;
        let quoted = rest.strip_prefix('"')?;
        let end_idx = quoted.find('"')?;
        Some(&quoted[..end_idx])
    });
    if current_version == Some(version) {
        return;
    }

    let tauri_conf_lines = tauri_conf.lines();
    let mut new_tauri_conf = String::new();

    for line in tauri_conf_lines {
        // The version in Tauri's config is formatted like this:
        //  "version": "0.1.0-alpha.0"
        // Please notice, that the version number line might have a leading tab, etc.
        if line.contains("\"version\": ") {
            new_tauri_conf.push_str(&format!("  \"version\": \"{version}\","));
        } else {
            new_tauri_conf.push_str(line);
        }
        new_tauri_conf.push('\n');
    }

    std::fs::write(tauri_conf_path, new_tauri_conf).unwrap();
}