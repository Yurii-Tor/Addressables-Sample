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
    [string]$OutputPath,
    [string]$LogFile = 'Logs/WebGLBuild.log'
)

$ErrorActionPreference = 'Stop'

# $PSScriptRoot is not reliably populated while param() defaults are evaluated, so the
# script root is resolved here and the output path defaults from it.
$scriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
if (-not $OutputPath) {
    $OutputPath = Join-Path $scriptRoot '..\..\..\Web\demos-site\public\addressables-selection'
}

if (-not (Test-Path -LiteralPath $UnityPath -PathType Leaf)) {
    throw "Unity editor was not found: $UnityPath"
}

$webGlSupport = Join-Path (Split-Path -Parent $UnityPath) 'Data\PlaybackEngines\WebGLSupport'
if (-not (Test-Path -LiteralPath $webGlSupport -PathType Container)) {
    throw "The WebGL build module is not installed for this editor. Install 'Web Build Support' " +
          "from Unity Hub, then run this script again."
}

$projectPath = (Resolve-Path -LiteralPath (Join-Path $scriptRoot '..')).Path

# Resolve-Path fails on a path that does not exist yet, so create it first.
$null = New-Item -ItemType Directory -Force -Path $OutputPath
$OutputPath = (Resolve-Path -LiteralPath $OutputPath).Path
$null = New-Item -ItemType Directory -Force -Path (Join-Path $projectPath 'Logs')

Write-Host "Project: $projectPath"
Write-Host "Output:  $OutputPath"
Write-Host "Switching to WebGL and building. The first target switch reimports every asset."

# Unity.exe is a GUI-subsystem binary, so PowerShell's call operator does not wait for it
# and never sets $LASTEXITCODE. Start-Process -Wait is what actually blocks until the build
# finishes, and -PassThru is what makes the exit code readable.
# Start-Process joins -ArgumentList with spaces and does not quote anything, so every path
# argument has to carry its own quotes -- the project path contains spaces.
$unityArguments = @(
    '-batchmode', '-nographics', '-quit',
    '-projectPath', "`"$projectPath`"",
    '-buildTarget', 'WebGL',
    '-executeMethod', 'AddressablesSample.Game.Editor.ContinuousIntegration.BuildWebGl',
    '-customBuildPath', "`"$OutputPath`"",
    '-logFile', "`"$LogFile`""
)

$unity = Start-Process -FilePath $UnityPath -ArgumentList $unityArguments `
    -WorkingDirectory $projectPath -Wait -PassThru -NoNewWindow
if ($unity.ExitCode -ne 0) {
    throw "Unity exited with code $($unity.ExitCode). See $LogFile."
}

$indexPath = Join-Path $OutputPath 'index.html'
if (-not (Test-Path -LiteralPath $indexPath -PathType Leaf)) {
    throw "The build reported success but produced no index.html in $OutputPath."
}

$size = (Get-ChildItem -LiteralPath $OutputPath -Recurse -File | Measure-Object -Property Length -Sum).Sum
Write-Host ("WebGL demo built: {0} ({1:N1} MB)" -f $OutputPath, ($size / 1MB))
