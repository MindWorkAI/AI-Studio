use std::fs;
use std::fs::File;
use std::io::{Error, ErrorKind, Write};
use std::path::PathBuf;
use std::process::Command;
use log::{info, warn};

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
        info!("Pid file {} was found, but process was not.", pid);
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
        info!("Pid file {} was found, but process was not.", pid);
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


pub fn kill_zombie_process(pid_file_path: PathBuf, process_name: &str) -> Result<(), Error> {
    if !pid_file_path.exists() {
        return Ok(());
    }

    let pid_str = fs::read_to_string(&pid_file_path)?;
    let pid: u32 = pid_str.trim().parse().map_err(|_| {Error::new(ErrorKind::InvalidData, "Invalid PID in file")})?;
    if let Err(e) = ensure_process_killed(pid, process_name){
        return Err(e);
    }

    fs::remove_file(&pid_file_path)?;
    info!("Killed qdrant process and deleted redundant Pid file: {}", pid_file_path.display());

    Ok(())
}

pub fn log_potential_zombie_process(pid_file_path: PathBuf, content: &str) {
    match File::create(&pid_file_path) {
        Ok(mut file) => {
            if let Err(e) = file.write_all(content.as_bytes()) {
                warn!("Failed to write to {}: {}", pid_file_path.display(), e);
            }
        }
        Err(e) => {
            warn!("Failed to create {}: {}", pid_file_path.display(), e);
        }
    }
}