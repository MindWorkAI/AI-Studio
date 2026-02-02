use std::fs;
use std::fs::File;
use std::io::{Error, ErrorKind, Write};
use std::path::{PathBuf};
use log::{info, warn};
use sysinfo::{Pid, ProcessesToUpdate, Signal, System};
use crate::sidecar_types::SidecarType;

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

pub fn kill_stale_process(pid_file_path: PathBuf, sidecar_type: SidecarType) -> Result<(), Error> {
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
        info!(Source="Stale Process Cleanup";"{}: Killed process: \"{}\"", sidecar_type,pid_file_path.display());
    } else {
        info!(Source="Stale Process Cleanup";"{}: Pid file with process number '{}' was found, but process was not.", sidecar_type, pid);
    };

    fs::remove_file(&pid_file_path)?;
    info!(Source="Stale Process Cleanup";"{}: Deleted redundant Pid file: \"{}\"", sidecar_type,pid_file_path.display());
    Ok(())
}

pub fn log_potential_stale_process(pid_file_path: PathBuf, pid: u32, sidecar_type: SidecarType) {
    let mut system = System::new_all();
    let pid = Pid::from_u32(pid);
    system.refresh_processes(ProcessesToUpdate::Some(&[pid]), true);
    let Some(process) = system.process(pid) else {
        warn!(Source="Stale Process Cleanup";
            "{}: Pid file with process number '{}' was not created because the process was not found.",
            sidecar_type, pid
        );
        return;
    };

    match File::create(&pid_file_path) {
        Ok(mut file) => {
            let name = process.name().to_string_lossy();
            let content = format!("{pid}\n{name}\n");
            if let Err(e) = file.write_all(content.as_bytes()) {
                warn!(Source="Stale Process Cleanup";"{}: Failed to write to \"{}\": {}", sidecar_type,pid_file_path.display(), e);
            }
        }
        Err(e) => {
            warn!(Source="Stale Process Cleanup";"{}: Failed to create \"{}\": {}", sidecar_type, pid_file_path.display(), e);
        }
    }
}
