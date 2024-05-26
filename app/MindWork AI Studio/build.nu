#!/usr/bin/env nu

def main [] {}

def are_assets_exist [rid: string]: string -> bool {
    $"bin/release/net8.0/($rid)/publish/wwwroot/_content/MudBlazor/MudBlazor.min.css" | path exists
}

def "main help" []: nothing -> nothing {
    print "Usage: nu build.nu [action]"
    print ""
    print "Optional Actions:"
    print "-----------------"
    print "  fix_web_assets   Prepare the web assets; run this once for each release on one platform; changes will be committed."
    print ""
    print "  metadata         Update the metadata file; run this on every platform right before the release; changes will be"
    print "                   committed once; there should be no differences between the platforms."
    print ""
    print "Actions:"
    print "---------"
    print "  prepare [action] Prepare the project for a release; increases the version & build numbers, updates the build time,"
    print "                   and runs fix_web_assets; run this once for each release on one platform; changes will be committed."
    print "                   The action can be 'major', 'minor', or 'patch'. The version will be updated accordingly."
    print ""
    print "  publish          Publish the project for all supported RIDs; run this on every platform."
    print ""
}

def "main prepare" [action: string]: string -> nothing {
    if (update_app_version $action) {
        main fix_web_assets
        inc_build_number
        update_build_time
        main metadata
    }
}

def "main metadata" []: nothing -> nothing {
    update_dotnet_version
    update_rust_version
    update_mudblazor_version
    update_tauri_version
    update_project_commit_hash
    update_license_year "../../LICENSE"
    update_license_year "Components/Pages/About.razor.cs"
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
    
    main metadata
    
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
        
        ^dotnet publish --configuration release --runtime $rid --disable-build-servers --force
        
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

def update_build_time []: nothing -> nothing {
    mut meta_lines = open --raw ../../metadata.txt | lines
    mut build_time = $meta_lines.1
    
    let updated_build_time = (date now | date to-timezone UTC | format date "%Y-%m-%d %H:%M:%S")
    print $"Updated build time from ($build_time) to ($updated_build_time) UTC."
    
    $build_time = $"($updated_build_time) UTC"
    $meta_lines.1 = $build_time
    $meta_lines | save --raw --force ../../metadata.txt
}

def inc_build_number []: nothing -> nothing {
    mut meta_lines = open --raw ../../metadata.txt | lines
    mut build_number = $meta_lines.2 | into int
    
    let updated_build_number = ([$build_number, 1] | math sum)
    print $"Incremented build number from ($build_number) to ($updated_build_number)."
    
    $build_number = $updated_build_number
    $meta_lines.2 = ($build_number | into string)
    $meta_lines | save --raw --force ../../metadata.txt
}

def update_dotnet_version []: nothing -> nothing {
    mut meta_lines = open --raw ../../metadata.txt | lines
    mut dotnet_sdk_version = $meta_lines.3
    mut dotnet_version = $meta_lines.4
    
    let dotnet_data = (^dotnet --info) | parse --regex '(?s).NET SDK:\s+Version:\s+(?P<sdkVersion>[0-9.]+).+Commit:\s+(?P<sdkCommit>[a-zA-Z0-9]+).+Host:\s+Version:\s+(?P<hostVersion>[0-9.]+).+Commit:\s+(?P<hostCommit>[a-zA-Z0-9]+)'
    let sdk_version = $dotnet_data.sdkVersion.0
    let host_version = $dotnet_data.hostVersion.0
    let sdkCommit = $dotnet_data.sdkCommit.0
    let hostCommit = $dotnet_data.hostCommit.0
    
    print $"Updated .NET SDK version from ($dotnet_sdk_version) to ($sdk_version) \(commit ($sdkCommit)\)."
    $meta_lines.3 = $"($sdk_version) \(commit ($sdkCommit)\)"
    
    print $"Updated .NET version from ($dotnet_version) to ($host_version) \(commit ($hostCommit)\)."
    $meta_lines.4 = $"($host_version) \(commit ($hostCommit)\)"
    
    $meta_lines | save --raw --force ../../metadata.txt
}

def update_rust_version []: nothing -> nothing {
    mut meta_lines = open --raw ../../metadata.txt | lines
    mut rust_version = $meta_lines.5
    
    let rust_data = (^rustc -Vv) | parse --regex 'rustc (?<version>[0-9.]+) \((?<commit>[a-zA-Z0-9]+)'
    let version = $rust_data.version.0
    let commit = $rust_data.commit.0
    
    print $"Updated Rust version from ($rust_version) to ($version) \(commit ($commit)\)."
    $meta_lines.5 = $"($version) \(commit ($commit)\)"
    
    $meta_lines | save --raw --force ../../metadata.txt
}

def update_mudblazor_version []: nothing -> nothing {
    mut meta_lines = open --raw ../../metadata.txt | lines
    mut mudblazor_version = $meta_lines.6
    
    let mudblazor_data = (^dotnet list package) | parse --regex 'MudBlazor\s+(?<version>[0-9.]+)'
    let version = $mudblazor_data.version.0
    
    print $"Updated MudBlazor version from ($mudblazor_version) to ($version)."
    $meta_lines.6 = $version
    
    $meta_lines | save --raw --force ../../metadata.txt
}

def update_tauri_version []: nothing -> nothing {
    mut meta_lines = open --raw ../../metadata.txt | lines
    mut tauri_version = $meta_lines.7
    
    cd ../../runtime
    let tauri_data = (^cargo tree --depth 1) | parse --regex 'tauri\s+v(?<version>[0-9.]+)'
    let version = $tauri_data.version.0
    cd "../app/MindWork AI Studio"
    
    print $"Updated Tauri version from ($tauri_version) to ($version)."
    $meta_lines.7 = $version
    
    $meta_lines | save --raw --force ../../metadata.txt
}

def update_license_year [licence_file: string] {
    let current_year = (date now | date to-timezone UTC | format date "%Y")
    let license_text = open --raw $licence_file | lines
    print $"Updating the license's year in ($licence_file) to ($current_year)."
    
    # Target line looks like `Copyright 2024 Thorsten Sommer`.
    # Perhaps, there are whitespaces at the beginning. Using
    # a regex to match the year.
    let updated_license_text = $license_text | each { |it|
        if $it =~ '^\s*Copyright\s+[0-9]{4}' {
            $it | str replace --regex '([0-9]{4})' $"($current_year)"
        } else {
            $it
        }
    }
    
    $updated_license_text | save --raw --force $licence_file
}

def update_app_version [action: string]: string -> bool {
    mut meta_lines = open --raw ../../metadata.txt | lines
    mut app_version = $meta_lines.0
    
    let version_data = $app_version | parse --regex '(?P<major>[0-9]+)\.(?P<minor>[0-9]+)\.(?P<patch>[0-9]+)'
    
    if $action == "major" {
    
        mut major = $version_data.major | into int
        $major = ([$major.0, 1] | math sum)
        
        let updated_version = [$major, 0, 0] | str join "."
        print $"Updated app version from ($app_version) to ($updated_version)."
        $meta_lines.0 = $updated_version
        
    } else if $action == "minor" {
    
        let major = $version_data.major | into int
        mut minor = $version_data.minor | into int
        $minor = ([$minor.0, 1] | math sum)
        
        let updated_version = [$major.0, $minor, 0] | str join "."
        print $"Updated app version from ($app_version) to ($updated_version)."
        $meta_lines.0 = $updated_version
        
    } else if $action == "patch" {
    
        let major = $version_data.major | into int
        let minor = $version_data.minor | into int
        mut patch = $version_data.patch | into int
        $patch = ([$patch.0, 1] | math sum)
        
        let updated_version = [$major.0, $minor.0, $patch] | str join "."
        print $"Updated app version from ($app_version) to ($updated_version)."
        $meta_lines.0 = $updated_version
        
    } else {
        print $"Invalid action '($action)'. Please use 'major', 'minor', or 'patch'."
        return false
    }
    
    $meta_lines | save --raw --force ../../metadata.txt
    return true
}

def update_project_commit_hash []: nothing -> nothing {
    mut meta_lines = open --raw ../../metadata.txt | lines
    mut commit_hash = $meta_lines.8
    
    # Check, if the work directory is clean. We allow, that the metadata file is dirty:
    let git_status = (^git status --porcelain) | lines
    let dirty_files = $git_status | length
    let first_is_metadata = ($dirty_files > 0 and $git_status.0 =~ '^\sM\s+metadata.txt$')
    let git_tag_response = ^git describe --tags --exact-match | complete
    let state = {
        num_dirty: $dirty_files,
        first_is_metadata: $first_is_metadata,
        git_tag_present: ($git_tag_response.exit_code == 0),
        git_tag: $git_tag_response.stdout
    }
    
    let commit_postfix = match $state {
        { num_dirty: $num_dirty, first_is_metadata: _, git_tag_present: _, git_tag: _ } if $num_dirty > 1 => ", dev debug",
        { num_dirty: $num_dirty, first_is_metadata: false, git_tag_present: _, git_tag: _ } if $num_dirty == 1 => ", dev debug",
        { num_dirty: $num_dirty, first_is_metadata: true, git_tag_present: false, git_tag: _ } if $num_dirty == 1 => ", dev testing",
        { num_dirty: $num_dirty, first_is_metadata: false, git_tag_present: false, git_tag: _ } if $num_dirty == 0 => ", dev testing",
        { num_dirty: $num_dirty, first_is_metadata: true, git_tag_present: true, git_tag: $tag } if $num_dirty == 1 => $", release $tag",
        { num_dirty: $num_dirty, first_is_metadata: false, git_tag_present: true, git_tag: $tag } if $num_dirty == 0 => $", release $tag",
        
        _ => "-dev unknown"
    }
    
    # Use the first ten characters of the commit hash:
    let updated_commit_hash = (^git rev-parse HEAD) | str substring 0..10 | append $commit_postfix | str join
    print $"Updated commit hash from ($commit_hash) to ($updated_commit_hash)."
    
    $meta_lines.8 = $updated_commit_hash
    $meta_lines | save --raw --force ../../metadata.txt
}