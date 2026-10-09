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
# The script needs ssign (https://github.com/Le-Syl21/ssign) in the PATH. ssign signs the file in
# place, adds the certificate chain and a timestamp, and keeps its cloud session for a while, so a
# build logs in only once.
#
param(
    [Parameter(Mandatory = $true)]
    [string] $Path
)

$ErrorActionPreference = 'Stop'

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

# Certum accepts every one-time code only once. When both Windows builds log in within the same
# 30 seconds, one of them is rejected and succeeds with the next code:
$maxAttempts = 3
for ($attempt = 1; $attempt -le $maxAttempts; $attempt++) {
    & ssign $Path
    if ($LASTEXITCODE -eq 0) {
        break
    }

    if ($attempt -eq $maxAttempts) {
        Write-Error "ssign could not sign '$Path' in $maxAttempts attempts."
    }

    Write-Output "ssign failed in attempt $attempt of $maxAttempts, retrying in 35 seconds ..."
    Start-Sleep -Seconds 35
}

$signature = Get-AuthenticodeSignature -FilePath $Path
if ($signature.Status -ne 'Valid') {
    Write-Error "The signature of '$Path' is not valid: $($signature.Status), $($signature.StatusMessage)"
}

Write-Output "Signed by '$($signature.SignerCertificate.Subject)': $Path"
