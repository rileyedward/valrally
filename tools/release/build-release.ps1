# Builds the ValRally release: dist\ValRally-<version>.zip and dist\ValRally-Launcher.exe, each
# with a .sha256, and copies all four into your Downloads folder, ready to drag onto a GitHub release.
#
#   powershell -ExecutionPolicy Bypass -File tools\release\build-release.ps1
#
# Zip layout (Thunderstore package; r2modman installs it as is):
#   manifest.json                  name, version, dependencies (BepInExPack_Valheim, Jotunn)
#   README.md, CHANGELOG.md, icon.png
#   plugins\ValRally.dll       the Valheim plugin
#   plugins\ValRally.Core.dll  the rally core it depends on
#
# Never in the zip: Valheim, BepInEx or Jotunn files, or anything from Art of Rally.
#
# The launcher keeps the same name every release, so the README can link to
# releases/latest/download/ValRally-Launcher.exe.
#
# Needs: .NET SDK and a Valheim install (the plugin compiles against the game's own DLLs,
# which is why releases are built locally). Set the path in Environment.props if it isn't
# the default Steam folder.
$ErrorActionPreference = 'Stop'

$root = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$project = Join-Path $root 'ValRally'
$manifest = Get-Content "$project\Package\manifest.json" -Raw | ConvertFrom-Json
$version = $manifest.version_number
$pluginVersion = [regex]::Match((Get-Content "$project\ValRallyPlugin.cs" -Raw), 'PluginVersion = "([^"]+)"').Groups[1].Value
if ($pluginVersion -ne $version) { throw "manifest.json is $version but PluginVersion is $pluginVersion" }
$launcherVersion = ([xml](Get-Content "$root\ValRally.Launcher\ValRally.Launcher.csproj" -Raw)).Project.PropertyGroup.Version | Where-Object { $_ } | Select-Object -First 1
if ($launcherVersion -ne $version) { throw "manifest.json is $version but the launcher's <Version> is $launcherVersion" }

$stage = Join-Path $root 'dist\ValRally'
$zip = Join-Path $root "dist\ValRally-$version.zip"
$launcher = Join-Path $root 'dist\ValRally-Launcher.exe'

Write-Host "ValRally $version -> $zip"
if (Test-Path $stage) { Remove-Item -Recurse -Force $stage }
New-Item -ItemType Directory -Force "$stage\plugins" | Out-Null

Write-Host '== build'
dotnet build "$root\ValRally.sln" -c Release --nologo -v q
if ($LASTEXITCODE) { throw 'build failed' }

Write-Host '== sim'
& "$root\ValRally.Sim\bin\Release\net48\ValRally.Sim.exe"
if ($LASTEXITCODE) { throw 'sim checks failed' }

$out = "$project\bin\Release\net48"
Copy-Item "$out\ValRally.dll", "$out\ValRally.Core.dll" "$stage\plugins\"
Copy-Item "$project\Package\manifest.json", "$project\Package\icon.png", "$project\README.md", "$project\CHANGELOG.md" "$stage\"

Write-Host '== zip'
if (Test-Path $zip) { Remove-Item $zip }
# Written by hand so entries use forward slashes; Windows PowerShell's Compress-Archive
# writes backslashes, which Thunderstore and mod managers on Linux don't accept.
Add-Type -AssemblyName System.IO.Compression, System.IO.Compression.FileSystem
$archive = [System.IO.Compression.ZipFile]::Open($zip, 'Create')
try {
    Get-ChildItem $stage -Recurse -File | ForEach-Object {
        $entry = $_.FullName.Substring($stage.Length + 1).Replace('\', '/')
        [System.IO.Compression.ZipFileExtensions]::CreateEntryFromFile($archive, $_.FullName, $entry, 'Optimal') | Out-Null
    }
} finally { $archive.Dispose() }
$kb = [math]::Round((Get-Item $zip).Length / 1KB)
function Write-Sha256($file) {
    $sha = (Get-FileHash $file -Algorithm SHA256).Hash.ToLower()
    Set-Content "$file.sha256" "$sha  $(Split-Path $file -Leaf)" -Encoding ascii
    return $sha
}
$sha = Write-Sha256 $zip
Write-Host "done: $zip ($kb KB)  sha256 $sha"

Write-Host '== launcher'
Copy-Item "$root\ValRally.Launcher\bin\Release\net48\ValRally-Launcher.exe" $launcher -Force
$sha = Write-Sha256 $launcher
Write-Host "done: $launcher ($([math]::Round((Get-Item $launcher).Length / 1KB)) KB)  sha256 $sha"

$downloads = (New-Object -ComObject Shell.Application).Namespace('shell:Downloads').Self.Path
if (-not $downloads) { $downloads = Join-Path $env:USERPROFILE 'Downloads' }
Copy-Item $zip, "$zip.sha256", $launcher, "$launcher.sha256" $downloads -Force
Write-Host "copied to $downloads"
