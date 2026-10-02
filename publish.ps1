# Publie la version portable : dist\UnblockFile-win-x64\ + dist\UnblockFile-win-x64.zip
# puis (re)crée le raccourci « Unblock File.lnk » à la racine du dépôt. Aucun droit administrateur requis.
$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
$out = Join-Path $root 'dist\UnblockFile-win-x64'
$zip = Join-Path $root 'dist\UnblockFile-win-x64.zip'
$exe = Join-Path $out 'UnblockFile.exe'
$lnk = Join-Path $root 'Unblock File.lnk'

Remove-Item $out, $zip -Recurse -Force -ErrorAction SilentlyContinue

dotnet publish (Join-Path $root 'src\Golabox.UnblockFile\Golabox.UnblockFile.csproj') `
    -c Release -r win-x64 --self-contained true `
    -p:PublishSingleFile=true -p:PublishTrimmed=false `
    -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true `
    -p:DebugType=None -p:DebugSymbols=false `
    -o $out
if ($LASTEXITCODE -ne 0 -or -not (Test-Path $exe)) { throw 'dotnet publish a échoué' }

Copy-Item (Join-Path $root 'README.md'), (Join-Path $root 'LICENSE') $out
Compress-Archive -Path (Join-Path $out '*') -DestinationPath $zip -CompressionLevel Optimal

# Raccourci racine : créé s'il manque, recréé si sa cible, son dossier de travail ou son icône ont changé.
$shell = New-Object -ComObject WScript.Shell
$icon = "$exe,0"
$ok = $false
if (Test-Path $lnk) {
    $current = $shell.CreateShortcut($lnk)
    $ok = $current.TargetPath -eq $exe -and $current.WorkingDirectory -eq $out -and $current.IconLocation -eq $icon
    if (-not $ok) { Remove-Item $lnk -Force }
}
if (-not $ok) {
    $shortcut = $shell.CreateShortcut($lnk)
    $shortcut.TargetPath = $exe
    $shortcut.WorkingDirectory = $out
    $shortcut.IconLocation = $icon
    $shortcut.Description = 'Débloquer des fichiers (retirer le Mark of the Web)'
    $shortcut.Save()
    Write-Host "Raccourci (re)créé : $lnk"
} else {
    Write-Host "Raccourci à jour : $lnk"
}

Get-ChildItem $out | Select-Object Name, Length
Write-Host "ZIP : $zip"
