param(
    [switch]$Install,
    [switch]$RebuildCert
)

$ErrorActionPreference = 'Stop'
$Root        = $PSScriptRoot
$AppProject  = "$Root\AUDIOBL\AUDIOBL.csproj"

# ── Version depuis le manifest ─────────────────────────────────────────────
$ManifestPath = "$Root\AUDIOBL.Package\Package.appxmanifest"
$ManifestXml  = [xml](Get-Content $ManifestPath)
$PkgVersion   = $ManifestXml.Package.Identity.Version   # e.g. "1.2.0.0"
$SemVer       = ($PkgVersion -split '\.')[ 0..2 ] -join '.'  # "1.2.0"
Write-Host ">> Version détectée : v$SemVer" -ForegroundColor Cyan

# ── Patch landing page ─────────────────────────────────────────────────────
$IndexPath = "$Root\docs\index.html"
$html = Get-Content $IndexPath -Raw
$patched = $html -replace 'v\d+\.\d+\.\d+(?=<\/span><\/div>)', "v$SemVer"
if ($patched -ne $html) {
    Set-Content $IndexPath $patched -Encoding UTF8 -NoNewline
    Write-Host "   Landing page mise à jour → v$SemVer" -ForegroundColor Green
} else {
    Write-Host "   Landing page déjà à jour" -ForegroundColor Yellow
}
$PkgDir      = "$Root\AUDIOBL.Package"
$PublishDir  = "$Root\_publish"
$PackageDir  = "$Root\_package"
$MsixPath    = "$Root\AUDIOBL.msix"
$CertPfx     = "$Root\AUDIOBL-dev.pfx"
$CertPass    = 'audiobl-dev'
$Publisher   = 'CN=AUDIOBL Dev'

$MakeAppx = 'C:\Program Files (x86)\Windows Kits\10\bin\10.0.26100.0\x64\makeappx.exe'
$SignTool  = 'C:\Program Files (x86)\Windows Kits\10\bin\10.0.26100.0\x64\signtool.exe'

# ── 1. Certificat auto-signé ───────────────────────────────────────────────
if ($RebuildCert -or -not (Test-Path $CertPfx)) {
    Write-Host ">> Création du certificat auto-signé..." -ForegroundColor Cyan
    $cert = New-SelfSignedCertificate `
        -Subject $Publisher `
        -CertStoreLocation 'Cert:\CurrentUser\My' `
        -KeyUsage DigitalSignature `
        -Type CodeSigningCert `
        -HashAlgorithm SHA256

    $certPwd = ConvertTo-SecureString $CertPass -AsPlainText -Force
    Export-PfxCertificate -Cert $cert -FilePath $CertPfx -Password $certPwd | Out-Null

    # Installer dans LocalMachine\TrustedPeople (requiert admin → on élève via UAC)
    $elevatedScript = @"
`$cert = New-Object System.Security.Cryptography.X509Certificates.X509Certificate2('$CertPfx', '$CertPass', 'DefaultKeySet')
`$s1 = New-Object System.Security.Cryptography.X509Certificates.X509Store('TrustedPeople', 'LocalMachine')
`$s1.Open('ReadWrite'); `$s1.Add(`$cert); `$s1.Close()
`$s2 = New-Object System.Security.Cryptography.X509Certificates.X509Store('Root', 'LocalMachine')
`$s2.Open('ReadWrite'); `$s2.Add(`$cert); `$s2.Close()
Write-Host 'Certificat installe'
"@
    $tmpScript = "$env:TEMP\audiobl-cert.ps1"
    $elevatedScript | Set-Content $tmpScript -Encoding UTF8
    Start-Process powershell -Verb RunAs -ArgumentList "-ExecutionPolicy Bypass -File `"$tmpScript`"" -Wait
    Remove-Item $tmpScript -Force -ErrorAction SilentlyContinue
    Write-Host "   Certificat installe dans LocalMachine\TrustedPeople + Root" -ForegroundColor Green
}

# ── 2. Publish app ─────────────────────────────────────────────────────────
Write-Host ">> Publish de l'app..." -ForegroundColor Cyan
if (Test-Path $PublishDir) { Remove-Item $PublishDir -Recurse -Force }

& 'C:\Program Files\dotnet\dotnet.exe' publish $AppProject `
    -c Release `
    -r win-x64 `
    --self-contained true `
    -p:PublishSingleFile=false `
    -o $PublishDir `
    --nologo 2>&1 | Where-Object { $_ -match 'error|warning|publish' }

if ($LASTEXITCODE -ne 0) { throw "Publish échoué" }
Write-Host "   Publish OK → $PublishDir" -ForegroundColor Green

# ── 3. Assembler le package ────────────────────────────────────────────────
Write-Host ">> Assemblage du package..." -ForegroundColor Cyan
if (Test-Path $PackageDir) { Remove-Item $PackageDir -Recurse -Force }
New-Item -ItemType Directory $PackageDir | Out-Null

# Copier les binaires
Copy-Item "$PublishDir\*" $PackageDir -Recurse

# Copier le manifest (makeappx attend AppxManifest.xml)
Copy-Item "$PkgDir\Package.appxmanifest" "$PackageDir\AppxManifest.xml"
Copy-Item "$PkgDir\Assets" $PackageDir -Recurse

# Copier le .ico dans le package (référencé par l'app)
if (Test-Path "$Root\AUDIOBL\Resources\tray.ico") {
    New-Item -ItemType Directory -Force "$PackageDir\Resources" | Out-Null
    Copy-Item "$Root\AUDIOBL\Resources\tray.ico" "$PackageDir\Resources\"
}

Write-Host "   Package assemblé dans $PackageDir" -ForegroundColor Green

# ── 4. Créer le .msix ──────────────────────────────────────────────────────
Write-Host ">> Création du .msix avec makeappx..." -ForegroundColor Cyan
if (Test-Path $MsixPath) { Remove-Item $MsixPath -Force }

& $MakeAppx pack /d $PackageDir /p $MsixPath /nv /o

if ($LASTEXITCODE -ne 0) { throw "makeappx échoué" }
Write-Host "   .msix créé : $MsixPath" -ForegroundColor Green

# ── 5. Signer le .msix ────────────────────────────────────────────────────
Write-Host ">> Signature avec signtool..." -ForegroundColor Cyan
& $SignTool sign /fd SHA256 /p7 . /p7co 1.2.840.113549.1.7.1 /p7ce DetachedSignedData `
    /f $CertPfx /p $CertPass $MsixPath

# signtool peut retourner 0 ou 1 selon les options — on vérifie autrement
& $SignTool sign /fd SHA256 /f $CertPfx /p $CertPass $MsixPath
Write-Host "   Signé OK" -ForegroundColor Green

# ── 6. Installer ──────────────────────────────────────────────────────────
if ($Install) {
    Write-Host ">> Installation du package MSIX..." -ForegroundColor Cyan
    # Désinstaller l'ancienne version si présente
    Get-AppxPackage -Name "AUDIOBL" -ErrorAction SilentlyContinue | Remove-AppxPackage -ErrorAction SilentlyContinue
    Start-Sleep 1
    Add-AppxPackage -Path $MsixPath
    Write-Host "   Installé ! Lance AUDIOBL depuis le menu Démarrer." -ForegroundColor Green
}

Write-Host ""
Write-Host "[OK] Build MSIX termine : $MsixPath" -ForegroundColor Green
if (-not $Install) {
    Write-Host "  Pour installer : .\build-msix.ps1 -Install" -ForegroundColor Yellow
}
