use std::fs;
use std::fs::File;
use std::io::{Error, ErrorKind, Write};
use std::path::{PathBuf};
use log::{info, warn};
use sysinfo::{Pid, ProcessesToUpdate, Signal, System};

fn parse_pid_file(content: &str) -> Result<(u32, String), Error> {
    let mut lines = content
        .lines()
        .map(|line| line.trim())
        .filter(|line| !line.is_empty());
    let pid_str = lines
        .next()
        .ok_or_else(|| Error::new(ErrorKind::InvalidData, "Missing PID in file"))?;
    let pid: u32 = pid_str
        .parse()
        .map_err(|_| Error::new(ErrorKind::InvalidData, "Invalid PID in file"))?;
    let name = lines
        .next()
        .ok_or_else(|| Error::new(ErrorKind::InvalidData, "Missing process name in file"))?
        .to_string();
    Ok((pid, name))
}

pub fn kill_stale_process(pid_file_path: PathBuf) -> Result<(), Error> {
    if !pid_file_path.exists() {
        return Ok(());
    }

    let pid_file_content = fs::read_to_string(&pid_file_path)?;
    let (pid, expected_name) = parse_pid_file(&pid_file_content)?;

    let mut system = System::new_all();

    let pid = Pid::from_u32(pid);
    system.refresh_processes(ProcessesToUpdate::Some(&[pid]), true);
    if let Some(process) = system.process(pid){
        let name = process.name().to_string_lossy();
        if name != expected_name {
            return Err(Error::new(
                ErrorKind::InvalidInput,
                format!(
                    "Process name does not match: expected '{}' but found '{}'",
                    expected_name, name
                ),
            ));
        }

        let killed = process.kill_with(Signal::Kill).unwrap_or_else(|| process.kill());
        if !killed {
            return Err(Error::new(ErrorKind::Other, "Failed to kill process"));
        }

        system.refresh_processes(ProcessesToUpdate::Some(&[pid]), true);
        if !system.process(pid).is_none() {
            return Err(Error::new(ErrorKind::Other, "Process still running after kill attempt"))
        }
        info!("Killed process: {}", pid_file_path.display());
    } else {
        info!("Pid file {} was found, but process was not.", pid);
    };

    fs::remove_file(&pid_file_path)?;
    info!("Deleted redundant Pid file: {}", pid_file_path.display());
    Ok(())
}

pub fn log_potential_stale_process(pid_file_path: PathBuf, pid: u32) {
    let mut system = System::new_all();
    let pid_u32 = pid;
    let pid = Pid::from_u32(pid_u32);
    system.refresh_processes(ProcessesToUpdate::Some(&[pid]), true);
    let Some(process) = system.process(pid) else {
        warn!(
            "Pid file {} was not created because the process was not found.",
            pid_u32
        );
        return;
    };

    match File::create(&pid_file_path) {
        Ok(mut file) => {
            let name = process.name().to_string_lossy();
            let content = format!("{pid_u32}\n{name}\n");
            if let Err(e) = file.write_all(content.as_bytes()) {
                warn!("Failed to write to {}: {}", pid_file_path.display(), e);
            }
        }
        Err(e) => {
            warn!("Failed to create {}: {}", pid_file_path.display(), e);
        }
    }
}
