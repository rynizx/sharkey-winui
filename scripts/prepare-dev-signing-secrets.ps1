param(
    [string]$CertName = "SharkeyWinUI Dev",
    [string]$Password = "ChangeMe-DevOnly-123!",
    [string]$OutputDir = ".\\artifacts\\dev-cert"
)

$ErrorActionPreference = "Stop"

# Generates a local code-signing cert for CI testing only. Do not use this for public distribution.
New-Item -ItemType Directory -Force -Path $OutputDir | Out-Null

$subject = "CN=$CertName"
$cert = New-SelfSignedCertificate `
    -Type CodeSigningCert `
    -Subject $subject `
    -CertStoreLocation "Cert:\CurrentUser\My" `
    -HashAlgorithm "SHA256" `
    -KeyExportPolicy Exportable `
    -NotAfter (Get-Date).AddYears(1)

$pfxPath = Join-Path $OutputDir "sharkey-dev-signing.pfx"
$securePassword = ConvertTo-SecureString $Password -AsPlainText -Force
Export-PfxCertificate -Cert $cert -FilePath $pfxPath -Password $securePassword | Out-Null

$pfxBase64 = [Convert]::ToBase64String([IO.File]::ReadAllBytes($pfxPath))
$subjectForManifest = $cert.Subject

Write-Host "Created dev certificate at: $pfxPath"
Write-Host ""
Write-Host "Set these repository secrets in GitHub Actions:" -ForegroundColor Cyan
Write-Host "PACKAGE_CERT_PFX_BASE64 = $pfxBase64"
Write-Host "PACKAGE_CERT_PASSWORD   = $Password"
Write-Host "PACKAGE_CERT_SUBJECT    = $subjectForManifest"
Write-Host ""
Write-Host "Set Package.appxmanifest Identity Publisher to:" -ForegroundColor Yellow
Write-Host $subjectForManifest
Write-Host ""
Write-Host "WARNING: Self-signed certificates are for testing only." -ForegroundColor Red
