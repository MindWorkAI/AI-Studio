#!/usr/bin/env nu

def main [] {}

def are_assets_exist [rid: string]: string -> bool {
    $"bin/release/net8.0/($rid)/publish/wwwroot/_content/MudBlazor/MudBlazor.min.css" | path exists
}

def "main fix_web_assets" []: nothing -> nothing {

    # Get the matching RIDs for the current OS:
    let rids = get_rids
    
    # We chose the first RID to copy the assets from:
    let rid = $rids.0

    if (are_assets_exist $rid) == false {
        print $"Web assets do not exist for ($rid). Please build the project first."
        return
    }
    
    # Ensure, that the dist directory exists:
    mkdir wwwroot/system

    # Copy the web assets from the first RID to the source project:
    let source_path: glob = $"bin/release/net8.0/($rid)/publish/wwwroot/_content/*"
    cp --recursive --force --update $source_path wwwroot/system/
}

def "main publish" []: nothing -> nothing {
    
    # Ensure, that the dist directory exists:
    mkdir bin/dist
    
    # Get the matching RIDs for the current OS:
    let rids = get_rids
    
    if ($rids | length) == 0 {
        print "No RIDs to build for."
        return
    }
    
    let current_os = get_os
    let published_filename_dotnet = match $current_os {
        "windows" => "mindworkAIStudio.exe",
        _ => "mindworkAIStudio"
    }
    
    # Build for each RID:
    for rid in $rids {
        print "=============================="
        print $"Start building for ($rid)..."
        
        ^dotnet publish --configuration release --runtime $rid
        
        let final_filename = match $rid {
            "win-x64" => "mindworkAIStudio-x86_64-pc-windows-msvc.exe",
            "win-arm64" => "mindworkAIStudio-aarch64-pc-windows-msvc.exe",
            "linux-x64" => "mindworkAIStudio-x86_64-unknown-linux-gnu",
            "linux-arm64" => "mindworkAIStudio-aarch64-unknown-linux-gnu",
            "osx-arm64" => "mindworkAIStudio-aarch64-apple-darwin",
            "osx-x64" => "mindworkAIStudio-x86_64-apple-darwin",
            
            _ => {
                print $"Unsupported RID for final filename: ($rid)"
                return
            }
        }
        
        let published_path = $"bin/release/net8.0/($rid)/publish/($published_filename_dotnet)"
        let final_path = $"bin/dist/($final_filename)"
        
        if ($published_path | path exists) {
            print $"Published file ($published_path) exists."
        } else {
            print $"Published file ($published_path) does not exist. Compiling might failed?"
            return
        }
        
        print $"Moving ($published_path) to ($final_path)..."
        mv --force $published_path $final_path
    }
    
    print "=============================="
    print "Start building runtime..."
    
    cd ../../runtime
    try {
        cargo tauri build
    };
    
    cd "../app/MindWork AI Studio"
    print "=============================="
    print "Building done."
}

def get_rids []: nothing -> list {
    # Define the list of RIDs to build for, cf. https://learn.microsoft.com/en-us/dotnet/core/rid-catalog:
    let rids = ["win-x64", "win-arm64", "linux-x64", "linux-arm64", "osx-arm64", "osx-x64"]
    
    # Get the current OS:
    let current_os = get_os
    let current_os_dotnet = match $current_os {
        "windows" => "win-",
        "linux" => "linux-",
        "darwin" => "osx-",
        
        _ => {
            print $"Unsupported OS: ($current_os)"
            return
        }
    }
    
    # Filter the RIDs to build for the current OS:
    let rids = $rids | where $it =~ $current_os_dotnet
    
    # Return the list of RIDs to build for:
    $rids
}

def get_os []: nothing -> string {
    (sys).host.name | str downcase
}