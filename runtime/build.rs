use std::{env, fs};
use std::path::{PathBuf};
use std::process::Command;
use std::io::{Error, ErrorKind};

fn main() {
    match env::var("MINDWORK_START_DEV_ENV") {
        Ok(val) => {
            let is_started_manually = match val.parse::<bool>() {
                Ok(b) => b,
                Err(_) => {
                    println!("cargo: warning= Invalid value for MINDWORK_START_DEV_ENV: expected 'true' or 'false'");
                    return;
                }
            };
            if is_started_manually {
                if let Err(e) = kill_zombie_qdrant_process(){
                    println!("cargo:warning=Error: {e}");
                    return;
                };
                if let Err(e) = delete_old_certificates() {
                    println!("cargo: warning= Failed to delete old certificates: {e}");
                }
            }
        },
        Err(_) => {
            println!("cargo: warning= The environment variable 'MINDWORK_START_DEV_ENV' was not found.");
            if let Err(e) = kill_zombie_qdrant_process(){
                println!("cargo:warning=Error: {e}");
                return;
            };
            if let Err(e) = delete_old_certificates() {
                println!("cargo: warning= Failed to delete old certificates: {e}");
            }
        }
    }
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

#[cfg(unix)]
pub fn ensure_process_killed(pid: u32, expected_name: &str) -> Result<(), Error> {
    //
    // Check if PID exists and name matches
    //
    let ps_output = Command::new("ps")
        .arg("-p")
        .arg(pid.to_string())
        .arg("-o")
        .arg("comm=")
        .output()?;

    let output = String::from_utf8_lossy(&ps_output.stdout).trim().to_string();

    if output.is_empty() {
        // Process doesn't exist
        return Ok(());
    }

    let name = output;
    if name != expected_name {
        return Err(Error::new(ErrorKind::InvalidInput, "Process name does not match"));
    }

    //
    // Kill the process
    //
    let kill_output = Command::new("kill")
        .arg("-9")
        .arg(pid.to_string())
        .output()?;

    if !kill_output.status.success() {
        return Err(Error::new(ErrorKind::Other, "Failed to kill process"));
    }

    //
    // Verify process is killed
    //
    let ps_check = Command::new("ps")
        .arg("-p")
        .arg(pid.to_string())
        .output()?;

    let output = String::from_utf8_lossy(&ps_check.stdout).trim().to_string();
    if output.is_empty() {
        Ok(())
    } else {
        Err(Error::new(ErrorKind::Other, "Process still running after kill attempt"))
    }
}

#[cfg(windows)]
pub fn ensure_process_killed(pid: u32, expected_name: &str) -> Result<(), Error> {
    //
    // Check if PID exists and name matches
    //
    let tasklist_output = Command::new("tasklist")
        .arg("/FI")
        .arg(format!("PID eq {}", pid))
        .arg("/FO")
        .arg("CSV")
        .arg("/NH")
        .output()?;

    let output = String::from_utf8_lossy(&tasklist_output.stdout).trim().to_string();

    if output.is_empty() || !output.starts_with('"') {
        println!("cargo:warning= Pid file was found, but process was not.");
        return Ok(())
    }

    let name = output.split(',').next().unwrap_or("").trim_matches('"');
    if name != expected_name {
        return Err(Error::new(ErrorKind::InvalidInput, format!("Process name does not match. Expected:{}, got:{}",expected_name,name)));
    }

    //
    // Kill the process
    //
    let kill_output = Command::new("taskkill")
        .arg("/PID")
        .arg(pid.to_string())
        .arg("/F")
        .arg("/T")
        .output()?;

    if !kill_output.status.success() {
        return Err(Error::new(ErrorKind::Other, "Failed to kill process"));
    }

    //
    // Verify process is killed
    //
    let tasklist_check = Command::new("tasklist")
        .arg("/FI")
        .arg(format!("PID eq {}", pid))
        .output()?;

    let output = String::from_utf8_lossy(&tasklist_check.stdout).trim().to_string();
    if output.is_empty() || !output.starts_with('"') {
        Ok(())
    }
    else {
        Err(Error::new(ErrorKind::Other, "Process still running after kill attempt"))
    }
}


pub fn kill_zombie_qdrant_process() -> Result<(), Error> {
    let pid_file = dirs::data_local_dir()
        .expect("Local appdata was not found")
        .join("com.github.mindwork-ai.ai-studio")
        .join("data")
        .join("databases")
        .join("qdrant")
        .join("qdrant.pid");

    if !pid_file.exists() {
        return Ok(());
    }

    let pid_str = fs::read_to_string(&pid_file)?;
    let pid: u32 = pid_str.trim().parse().map_err(|_| {Error::new(ErrorKind::InvalidData, "Invalid PID in file")})?;
    if let Err(e) = ensure_process_killed(pid, "qdrant.exe".as_ref()){
        return Err(e);
    }

    fs::remove_file(&pid_file)?;
    println!("cargo:warning= Killed qdrant process and deleted redundant Pid file: {}", pid_file.display());

    Ok(())
}

pub fn delete_old_certificates() -> Result<(), Box<dyn std::error::Error>> {
    let dir_path = dirs::data_local_dir()
        .expect("Local appdata was not found")
        .join("com.github.mindwork-ai.ai-studio")
        .join("data")
        .join("databases")
        .join("qdrant");

    if !dir_path.exists() {
        return Ok(());
    }

    for entry in fs::read_dir(dir_path)? {
        let entry = entry?;
        let path = entry.path();

        if path.is_dir() {
            let file_name = entry.file_name();
            let folder_name = file_name.to_string_lossy();

            if folder_name.starts_with("cert-") {
                fs::remove_dir_all(&path)?;
                println!("cargo: warning= Removed old certificates in: {}", path.display());
            }
        }
    }
    Ok(())
}
