<#
.SYNOPSIS
Builds the Flutter Android app as a release APK and copies the output to the public OneDrive folder.

.DESCRIPTION
Runs `flutter build apk --release` from the `ui/` Flutter project directory, then copies the generated
APK (typically at `build/app/outputs/flutter-apk/app-release.apk`) to the public OneDrive folder.

.NOTES
Adjust `$flutterProjectDir` or `$destinationDir` if your project layout changes.
#>

$ErrorActionPreference = 'Stop'

# Change these if your project has a different structure
$flutterProjectDir = Join-Path $PSScriptRoot 'ui'
$destinationDir   = 'C:\Users\Abdulla\OneDrive\Public'

Write-Host "Building Flutter Android release APK in: $flutterProjectDir"
Push-Location $flutterProjectDir
try {
    flutter build apk --release
} finally {
    Pop-Location
}

$apkPath = Join-Path $flutterProjectDir 'build\app\outputs\flutter-apk\app-release.apk'
if (-not (Test-Path $apkPath)) {
    throw "Expected APK not found at: $apkPath"
}

Write-Host "Copying APK to: $destinationDir"
Copy-Item -Path $apkPath -Destination $destinationDir -Force
Write-Host "Done."
