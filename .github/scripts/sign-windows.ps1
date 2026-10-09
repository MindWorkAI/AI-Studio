#
# Signs one Windows binary with our Certum code signing certificate. Tauri calls this script as its
# custom sign command for every file it signs: the main binary, the sidecar, PDFium, the NSIS
# plugins, the uninstaller, and the installer.
#
# Usage:
#   sign-windows.ps1 <path of the file to sign>
#
# Environment:
#   CERTUM_EMAIL   e-mail address of the Certum SimplySign account
#   CERTUM_OTP     TOTP seed of that account, from which ssign computes each one-time code
#
# The script needs ssign (https://github.com/Le-Syl21/ssign) in the PATH. ssign adds the
# certificate chain and a timestamp, and keeps its cloud session for a while, so a build logs in
# only once.
#
# ssign does not sign in place here: it would replace the file by renaming its signed copy over it,
# and Windows refuses that while another process keeps the file open. Tauri does exactly that with
# the main binary while signing it. So ssign writes its copy elsewhere, and this script writes the
# copy back into the very same file, which an open handle with write sharing allows.
#
param(
    [Parameter(Mandatory = $true)]
    [string] $Path
)

$ErrorActionPreference = 'Stop'

# Tauri passes some paths relative to its working directory. .NET would resolve them against the
# working directory of the process instead, so every call below gets the full path:
$Path = (Resolve-Path -LiteralPath $Path).ProviderPath

# Tauri hands over some files without checking whether they are signed already, the NSIS plugins
# among them. ssign never replaces an existing signature, so a valid one stays as it is, and an
# invalid one stops the build with a clear message instead of three failed attempts:
$signature = Get-AuthenticodeSignature -FilePath $Path
if ($signature.Status -eq 'Valid') {
    Write-Output "Already signed, skipping: $Path"
    exit 0
}

if ($signature.Status -ne 'NotSigned') {
    Write-Error "'$Path' carries a signature which is not valid ($($signature.Status)), and ssign cannot replace it."
}

# Tauri shows the output of its sign command only when signing succeeds. Everything ssign says
# therefore goes into a log, which the workflow prints when the build fails:
$logDirectory = if ($env:RUNNER_TEMP) { $env:RUNNER_TEMP } else { [System.IO.Path]::GetTempPath() }
$logFile = Join-Path $logDirectory 'sign-windows.log'

$outputDirectory = Join-Path ([System.IO.Path]::GetTempPath()) "sign-windows-$([guid]::NewGuid())"
New-Item -ItemType Directory -Path $outputDirectory | Out-Null

try {
    # Certum accepts every one-time code only once. When both Windows builds log in within the same
    # 30 seconds, one of them is rejected and succeeds with the next code:
    $maxAttempts = 3
    for ($attempt = 1; $attempt -le $maxAttempts; $attempt++) {
        $stdout = New-TemporaryFile
        $stderr = New-TemporaryFile
        $process = Start-Process -FilePath 'ssign' -ArgumentList @('--verbose', '--output-dir', "`"$outputDirectory`"", "`"$Path`"") -NoNewWindow -Wait -PassThru -RedirectStandardOutput $stdout.FullName -RedirectStandardError $stderr.FullName
        $exitCode = $process.ExitCode

        Add-Content -Path $logFile -Value "--- $(Get-Date -Format o) | attempt $attempt of $maxAttempts | exit code $exitCode | $Path"
        Get-Content -Path $stdout.FullName, $stderr.FullName | Add-Content -Path $logFile
        Get-Content -Path $stdout.FullName, $stderr.FullName
        Remove-Item -Path $stdout.FullName, $stderr.FullName

        if ($exitCode -eq 0) {
            break
        }

        if ($attempt -eq $maxAttempts) {
            Write-Error "ssign could not sign '$Path' in $maxAttempts attempts, see $logFile."
        }

        Write-Output "ssign failed in attempt $attempt of $maxAttempts, retrying in 35 seconds ..."
        Start-Sleep -Seconds 35
    }

    $signedCopy = Join-Path $outputDirectory (Split-Path -Path $Path -Leaf)
    $source = [System.IO.File]::OpenRead($signedCopy)
    try {
        $target = [System.IO.File]::Open($Path, [System.IO.FileMode]::Truncate, [System.IO.FileAccess]::Write, [System.IO.FileShare]::ReadWrite)
        try {
            $source.CopyTo($target)
        }
        finally {
            $target.Dispose()
        }
    }
    finally {
        $source.Dispose()
    }
}
finally {
    Remove-Item -Path $outputDirectory -Recurse -Force -ErrorAction SilentlyContinue
}

$signature = Get-AuthenticodeSignature -FilePath $Path
if ($signature.Status -ne 'Valid') {
    Write-Error "The signature of '$Path' is not valid: $($signature.Status), $($signature.StatusMessage)"
}

Write-Output "Signed by '$($signature.SignerCertificate.Subject)': $Path"
