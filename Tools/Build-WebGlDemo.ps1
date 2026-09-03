<#
.SYNOPSIS
    Builds the WebGL demo player into a static-site directory.

.DESCRIPTION
    Switches the project to the WebGL build target, regenerates and validates the project,
    builds the Addressables content for WebGL, and produces the player.

    The Addressables content is local, so it is copied into StreamingAssets inside the build
    and no hosting URL is baked in. The resulting directory can be served from any origin
    and any sub-path without rebuilding.

    Unity must not already have this project open.

.EXAMPLE
    .\Tools\Build-WebGlDemo.ps1
    .\Tools\Build-WebGlDemo.ps1 -OutputPath 'D:\sites\demos\public\addressables-selection'
#>
[CmdletBinding()]
param(
    [string]$UnityPath = 'C:\Program Files\Unity\Hub\Editor\6000.3.21f1\Editor\Unity.exe',
    [string]$OutputPath = (Join-Path $PSScriptRoot '..\..\..\Web\demos-site\public\addressables-selection'),
    [string]$LogFile = 'Logs/WebGLBuild.log'
)

$ErrorActionPreference = 'Stop'

if (-not (Test-Path -LiteralPath $UnityPath -PathType Leaf)) {
    throw "Unity editor was not found: $UnityPath"
}

$webGlSupport = Join-Path (Split-Path -Parent $UnityPath) 'Data\PlaybackEngines\WebGLSupport'
if (-not (Test-Path -LiteralPath $webGlSupport -PathType Container)) {
    throw "The WebGL build module is not installed for this editor. Install 'Web Build Support' " +
          "from Unity Hub, then run this script again."
}

$projectPath = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..')).Path

# Resolve-Path fails on a path that does not exist yet, so create it first.
$null = New-Item -ItemType Directory -Force -Path $OutputPath
$OutputPath = (Resolve-Path -LiteralPath $OutputPath).Path
$null = New-Item -ItemType Directory -Force -Path (Join-Path $projectPath 'Logs')

Write-Host "Project: $projectPath"
Write-Host "Output:  $OutputPath"
Write-Host "Switching to WebGL and building. The first target switch reimports every asset."

& $UnityPath -batchmode -nographics -quit `
    -projectPath $projectPath `
    -buildTarget WebGL `
    -executeMethod AddressablesSample.Game.Editor.ContinuousIntegration.BuildWebGl `
    -customBuildPath $OutputPath `
    -logFile $LogFile

if ($LASTEXITCODE -ne 0) {
    throw "Unity exited with code $LASTEXITCODE. See $LogFile."
}

$indexPath = Join-Path $OutputPath 'index.html'
if (-not (Test-Path -LiteralPath $indexPath -PathType Leaf)) {
    throw "The build reported success but produced no index.html in $OutputPath."
}

$size = (Get-ChildItem -LiteralPath $OutputPath -Recurse -File | Measure-Object -Property Length -Sum).Sum
Write-Host ("WebGL demo built: {0} ({1:N1} MB)" -f $OutputPath, ($size / 1MB))
