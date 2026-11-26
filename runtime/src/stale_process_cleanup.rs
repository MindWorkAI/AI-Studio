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

    // Refreshing the one process adds it, so the system needs to load nothing else:
    let mut system = System::new().map_err(Error::other)?;

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
            return Err(Error::other("Failed to kill process"));
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
    let mut system = match System::new() {
        Ok(system) => system,
        Err(e) => {
            warn!(Source="Stale Process Cleanup";
                "{}: Pid file with process number '{}' was not created because the processes could not be read: {}",
                sidecar_type, pid, e
            );
            return;
        }
    };

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

// The test starts `sleep`, which Windows lacks:
#[cfg(all(test, unix))]
mod tests {
    use super::*;

    /// Verifies that a process found by its number alone gets a PID file and is killed later on.
    #[test]
    fn stale_process_is_recorded_and_killed() {
        use std::os::unix::process::ExitStatusExt;

        let mut child = std::process::Command::new("sleep").arg("30").spawn().unwrap();
        let pid_file_path = std::env::temp_dir().join(format!("ai-studio-stale-process-{}.pid", rand::random::<u64>()));

        log_potential_stale_process(pid_file_path.clone(), child.id(), SidecarType::Dotnet);
        let content = fs::read_to_string(&pid_file_path).unwrap();
        assert_eq!(content, format!("{}\nsleep\n", child.id()));

        kill_stale_process(pid_file_path.clone(), SidecarType::Dotnet).unwrap();
        assert!(!pid_file_path.exists());
        assert_eq!(child.wait().unwrap().signal(), Some(9));
    }
}
