#!/usr/bin/env nu

build

def build [] {
    
    # Ensure, that the dist directory exists:
    mkdir bin/dist

    # Define the list of RIDs to build for, cf. https://learn.microsoft.com/en-us/dotnet/core/rid-catalog:
    let rids = ["win-x64", "win-arm64", "linux-x64", "linux-arm64", "osx-arm64", "osx-x64"]
    
    # Get the current OS:
    let current_os = (sys).host.name | str downcase
    let current_os_dotnet = match $current_os {
        "windows" => "win-",
        "linux" => "linux-",
        "darwin" => "osx-",
        
        _ => {
            print $"Unsupported OS: ($current_os)"
            return
        }
    }
    
    let published_filename_dotnet = match $current_os {
        "windows" => "mindworkAIStudio.exe",
        _ => "mindworkAIStudio"
    }
    
    # Filter the RIDs to build for the current OS:
    let rids = $rids | where $it =~ $current_os_dotnet
    
    # Build for each RID:
    for rid in $rids {
        print "=============================="
        print $"Start building for ($rid)..."
        
        ^dotnet publish -c release -r $rid
        
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
}