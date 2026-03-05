use std::fmt;

pub enum SidecarType {
    Dotnet,
    Qdrant,
}

impl fmt::Display for SidecarType {
    fn fmt(&self, f: &mut fmt::Formatter<'_>) -> fmt::Result {
        match self {
            SidecarType::Dotnet => write!(f, ".Net"),
            SidecarType::Qdrant => write!(f, "Qdrant"),
        }
    }
}