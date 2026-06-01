use std::fmt;

pub enum SidecarType {
    Dotnet,
}

impl fmt::Display for SidecarType {
    fn fmt(&self, f: &mut fmt::Formatter<'_>) -> fmt::Result {
        match self {
            SidecarType::Dotnet => write!(f, ".Net"),
        }
    }
}