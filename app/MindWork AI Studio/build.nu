#!/usr/bin/env nu

build

def build [] {
    # Define the list of RIDs to build for, cf. https://learn.microsoft.com/en-us/dotnet/core/rid-catalog:
    let rids = ["win-x64", "win-arm64", "linux-x64", "linux-arm64", "osx-arm64", "osx-x64"]
    
    # Get the current OS:
    let current_os = (sys).host.name | str downcase
    let current_os = match $current_os {
        "windows" => "win-",
        "linux" => "linux-",
        "darwin" => "osx-",
        _ => {
            print $"Unsupported OS: ($current_os)"
            return
        }
    }
    
    # Filter the RIDs to build for the current OS:
    let rids = $rids | where $it =~ $current_os
    
    # Build for each RID:
    for rid in $rids {
        print $"Start building for ($rid)..."
        ^dotnet publish -c release -r $rid
    }
}