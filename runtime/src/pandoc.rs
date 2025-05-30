use std::path::{Path, PathBuf};
use std::fs;
use tokio::process::Command;
use crate::environment::DATA_DIRECTORY;
use crate::metadata::META_DATA;

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

        if local_installation_root_directory.exists() {
            let executable_name = Self::pandoc_executable_name();

            if let Ok(entries) = fs::read_dir(&local_installation_root_directory) {
                for entry in entries.flatten() {
                    let path = entry.path();
                    if path.is_dir() {
                        if let Ok(pandoc_path) = Self::find_executable_in_dir(&path, &executable_name) {
                            return PandocExecutable {
                                executable: pandoc_path.to_string_lossy().to_string(),
                                is_local_installation: true,
                            };
                        }
                    }
                }
            }
        }

        // When no local installation was found, we assume that the pandoc executable is in the system PATH:
        PandocExecutable {
            executable: Self::pandoc_executable_name(),
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
                if path.is_dir() {
                    if let Ok(found_path) = Self::find_executable_in_dir(&path, executable_name) {
                        return Ok(found_path);
                    }
                }
            }
        }

        Err("Executable not found".into())
    }

    /// Reads the os platform to determine the used executable name.
    fn pandoc_executable_name() -> String {
        let metadata = META_DATA.lock().unwrap();
        let metadata = metadata.as_ref().unwrap();

        match metadata.architecture.as_str() {
            "win-arm64" | "win-x64" => "pandoc.exe".to_string(),
            _ => "pandoc".to_string(),
        }
    }
}