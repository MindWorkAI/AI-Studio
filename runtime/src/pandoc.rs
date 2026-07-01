use std::collections::HashSet;
use std::env;
use std::fs;
use std::path::{Path, PathBuf};
use std::sync::OnceLock;
use log::{info, warn};
use tokio::process::Command;
use crate::environment::{DATA_DIRECTORY, is_flatpak};
use crate::metadata::META_DATA;

/// Tracks whether the RID mismatch warning has been logged.
static HAS_LOGGED_RID_MISMATCH: OnceLock<()> = OnceLock::new();
static HAS_LOGGED_PANDOC_PATH: OnceLock<()> = OnceLock::new();
const FLATPAK_PANDOC_PLUGIN_BIN_DIRECTORY: &str = "/app/plugins/pandoc/bin";

/// Microsoft documents CREATE_NO_WINDOW as a process creation flag with value 0x08000000.
/// It starts console applications without opening a console window:
/// https://learn.microsoft.com/en-us/windows/win32/procthread/process-creation-flags
#[cfg(windows)]
const CREATE_NO_WINDOW: u32 = 0x08000000;

pub struct PandocExecutable {
    pub executable: String,
    pub is_local_installation: bool,
}

pub struct PandocPreparedProcess {
    pub command: Command,
    pub is_local_installation: bool,
}

pub struct PandocProcessBuilder {
    provided_input_file: Option<String>,
    provided_output_file: Option<String>,
    provided_input_format: Option<String>,
    provided_output_format: Option<String>,
    additional_arguments: Vec<String>,
}

impl Default for PandocProcessBuilder {
    fn default() -> Self {
        Self::new()
    }
}

impl PandocProcessBuilder {
    pub fn new() -> Self {
        Self {
            provided_input_file: None,
            provided_output_file: None,
            provided_input_format: None,
            provided_output_format: None,
            additional_arguments: Vec::new(),
        }
    }

    pub fn with_input_file<S: Into<String>>(mut self, input_file: S) -> Self {
        self.provided_input_file = Some(input_file.into());
        self
    }

    pub fn with_output_file<S: Into<String>>(mut self, output_file: S) -> Self {
        self.provided_output_file = Some(output_file.into());
        self
    }

    pub fn with_input_format<S: Into<String>>(mut self, input_format: S) -> Self {
        self.provided_input_format = Some(input_format.into());
        self
    }

    pub fn with_output_format<S: Into<String>>(mut self, output_format: S) -> Self {
        self.provided_output_format = Some(output_format.into());
        self
    }

    pub fn add_argument<S: Into<String>>(mut self, argument: S) -> Self {
        self.additional_arguments.push(argument.into());
        self
    }

    pub fn build(self) -> PandocPreparedProcess {
        let mut arguments = Vec::new();
        if let Some(input_file) = &self.provided_input_file {
            arguments.push(input_file.clone());
        }

        if let Some(input_format) = &self.provided_input_format {
            arguments.push("-f".to_string());
            arguments.push(input_format.clone());
        }

        if let Some(output_format) = &self.provided_output_format {
            arguments.push("-t".to_string());
            arguments.push(output_format.clone());
        }

        for additional_argument in &self.additional_arguments {
            arguments.push(additional_argument.clone());
        }

        if let Some(output_file) = &self.provided_output_file {
            arguments.push("-o".to_string());
            arguments.push(output_file.clone());
        }

        let pandoc_executable = Self::pandoc_executable_path();
        let mut command = Command::new(&pandoc_executable.executable);
        
        #[cfg(windows)]
        command.creation_flags(CREATE_NO_WINDOW);
        command.args(&arguments);

        PandocPreparedProcess {
            command,
            is_local_installation: pandoc_executable.is_local_installation,
        }
    }

    /// Returns the path to the pandoc executable.
    ///
    /// Any local installation of pandoc will be preferred over the system-wide installation.
    /// When a local installation is found, its absolute path will be returned. In case no local
    /// installation is found, the name of the pandoc executable will be returned.
    fn pandoc_executable_path() -> PandocExecutable {
        // First, we try to find the pandoc executable in the data directory.
        // Any local installation should be preferred over the system-wide installation.
        let data_folder = PathBuf::from(DATA_DIRECTORY.get().unwrap());
        let local_installation_root_directory = data_folder.join("pandoc");
        let executable_name = Self::pandoc_executable_name();

        if local_installation_root_directory.exists()
            && let Ok(pandoc_path) = Self::find_executable_in_dir(&local_installation_root_directory, &executable_name) {
            HAS_LOGGED_PANDOC_PATH.get_or_init(|| {
                info!(Source = "PandocProcessBuilder"; "Found local Pandoc installation at: '{}'.", pandoc_path.to_string_lossy()
                );
            });

            return PandocExecutable {
                executable: pandoc_path.to_string_lossy().to_string(),
                is_local_installation: true,
            };
        }

        for candidate in Self::system_pandoc_executable_candidates(&executable_name) {
            if candidate.exists() && candidate.is_file() {
                HAS_LOGGED_PANDOC_PATH.get_or_init(|| {
                    info!(Source = "PandocProcessBuilder"; "Found system Pandoc installation at: '{}'.", candidate.to_string_lossy()
                    );
                });

                return PandocExecutable {
                    executable: candidate.to_string_lossy().to_string(),
                    is_local_installation: false,
                };
            }
        }

        // When no local installation was found, we assume that the pandoc executable is in the system PATH:
        HAS_LOGGED_PANDOC_PATH.get_or_init(|| {
            warn!(Source = "PandocProcessBuilder"; "Falling back to system PATH for the Pandoc executable: '{}'.", executable_name);
        });

        PandocExecutable {
            executable: executable_name,
            is_local_installation: false,
        }
    }

    fn find_executable_in_dir(dir: &Path, executable_name: &str) -> Result<PathBuf, Box<dyn std::error::Error>> {
        let pandoc_path = dir.join(executable_name);
        if pandoc_path.exists() && pandoc_path.is_file() {
            return Ok(pandoc_path);
        }

        // Recursively search in subdirectories
        if let Ok(entries) = fs::read_dir(dir) {
            for entry in entries.flatten() {
                let path = entry.path();
                if path.is_dir() && let Ok(found_path) = Self::find_executable_in_dir(&path, executable_name) {
                    return Ok(found_path);
                }
            }
        }

        Err("Executable not found".into())
    }

    fn system_pandoc_executable_candidates(executable_name: &str) -> Vec<PathBuf> {
        Self::system_pandoc_executable_candidates_for(env::consts::OS, executable_name, is_flatpak())
    }

    fn system_pandoc_executable_candidates_for(os: &str, executable_name: &str, include_flatpak_extension: bool) -> Vec<PathBuf> {
        let mut candidates: Vec<PathBuf> = Vec::new();
        match os {
            "windows" => {
                Self::push_env_candidate(&mut candidates, "LOCALAPPDATA", &["Pandoc", executable_name]);
                Self::push_env_candidate(&mut candidates, "ProgramFiles", &["Pandoc", executable_name]);
                Self::push_env_candidate(&mut candidates, "ProgramFiles(x86)", &["Pandoc", executable_name]);
            },
            "macos" => {
                candidates.push(PathBuf::from("/opt/homebrew/bin").join(executable_name));
                candidates.push(PathBuf::from("/usr/local/bin").join(executable_name));
                candidates.push(PathBuf::from("/usr/bin").join(executable_name));
            },
            "linux" => {
                if include_flatpak_extension {
                    candidates.push(PathBuf::from(FLATPAK_PANDOC_PLUGIN_BIN_DIRECTORY).join(executable_name));
                }
                candidates.push(PathBuf::from("/usr/local/bin").join(executable_name));
                candidates.push(PathBuf::from("/usr/bin").join(executable_name));
                candidates.push(PathBuf::from("/snap/bin").join(executable_name));

                if let Some(home_dir) = env::var_os("HOME") {
                    candidates.push(PathBuf::from(home_dir).join(".local").join("bin").join(executable_name));
                }
            },
            _ => {},
        }

        if let Some(path_value) = env::var_os("PATH") {
            for path_dir in env::split_paths(&path_value) {
                candidates.push(path_dir.join(executable_name));
            }
        }

        let mut seen = HashSet::new();
        candidates
            .into_iter()
            .filter(|path| seen.insert(path.clone()))
            .collect()
    }

    fn push_env_candidate(candidates: &mut Vec<PathBuf>, env_name: &str, parts: &[&str]) {
        if let Some(root) = env::var_os(env_name) {
            let mut path = PathBuf::from(root);

            for part in parts {
                path.push(part);
            }

            candidates.push(path);
        }
    }

    /// Determines the executable name based on the current OS at runtime.
    ///
    /// This uses runtime detection instead of metadata to ensure correct behavior
    /// on dev machines where the metadata may contain stale values.
    fn pandoc_executable_name() -> String {
        // Log a warning (once) if the runtime OS differs from the metadata architecture.
        // This can happen on dev machines where the metadata.txt contains stale values.
        HAS_LOGGED_RID_MISMATCH.get_or_init(|| {
            let runtime_os = std::env::consts::OS;
            let runtime_arch = std::env::consts::ARCH;

            if let Ok(metadata) = META_DATA.lock() && let Some(metadata) = metadata.as_ref() {
                let metadata_arch = &metadata.architecture;

                // Determine expected OS from metadata:
                let metadata_is_windows = metadata_arch.starts_with("win-");
                let metadata_is_macos = metadata_arch.starts_with("osx-");
                let metadata_is_linux = metadata_arch.starts_with("linux-");

                // Compare with runtime OS:
                let runtime_is_windows = runtime_os == "windows";
                let runtime_is_macos = runtime_os == "macos";
                let runtime_is_linux = runtime_os == "linux";

                let os_mismatch = (metadata_is_windows != runtime_is_windows)
                    || (metadata_is_macos != runtime_is_macos)
                    || (metadata_is_linux != runtime_is_linux);

                if os_mismatch {
                    warn!(
                        Source = "Pandoc";
                        "Runtime-detected OS '{}-{}' differs from metadata architecture '{}'. Using runtime-detected OS. This is expected on dev machines where metadata.txt may be outdated.",
                        runtime_os,
                        runtime_arch,
                        metadata_arch
                    );
                }
            }
        });

        // Use std::env::consts::OS for runtime detection instead of metadata
        match std::env::consts::OS {
            "windows" => "pandoc.exe".to_string(),
            _ => "pandoc".to_string(),
        }
    }
}

#[cfg(test)]
mod tests {
    use super::{FLATPAK_PANDOC_PLUGIN_BIN_DIRECTORY, PandocProcessBuilder};
    use std::fs;
    use std::path::PathBuf;
    use tempfile::tempdir;

    #[test]
    fn linux_candidates_include_flatpak_pandoc_extension_first_when_flatpak() {
        let candidates = PandocProcessBuilder::system_pandoc_executable_candidates_for("linux", "pandoc", true);
        let flatpak_candidate = PathBuf::from(FLATPAK_PANDOC_PLUGIN_BIN_DIRECTORY).join("pandoc");
        let usr_local_candidate = PathBuf::from("/usr/local/bin").join("pandoc");

        let flatpak_index = candidates.iter().position(|candidate| candidate == &flatpak_candidate).unwrap();
        let usr_local_index = candidates.iter().position(|candidate| candidate == &usr_local_candidate).unwrap();

        assert!(flatpak_index < usr_local_index);
    }

    #[test]
    fn linux_candidates_skip_flatpak_pandoc_extension_when_not_flatpak() {
        let candidates = PandocProcessBuilder::system_pandoc_executable_candidates_for("linux", "pandoc", false);
        let flatpak_candidate = PathBuf::from(FLATPAK_PANDOC_PLUGIN_BIN_DIRECTORY).join("pandoc");

        assert!(!candidates.contains(&flatpak_candidate));
    }

    #[test]
    fn local_pandoc_search_finds_data_directory_installation() {
        let directory = tempdir().unwrap();
        let pandoc_directory = directory.path().join("pandoc").join("bin");
        fs::create_dir_all(&pandoc_directory).unwrap();
        let pandoc_path = pandoc_directory.join("pandoc");
        fs::File::create(&pandoc_path).unwrap();

        assert_eq!(
            PandocProcessBuilder::find_executable_in_dir(directory.path(), "pandoc").unwrap(),
            pandoc_path
        );
    }
}