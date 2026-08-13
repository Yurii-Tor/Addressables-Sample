[CmdletBinding()]
param(
    [string]$ProjectName = "addressables-sample",
    [string]$ServerDataPath = (Join-Path $PSScriptRoot "..\ServerData"),
    [string]$PublicBaseUrl = "https://addressables-sample.pages.dev",
    [switch]$SkipHttpVerification
)

$ErrorActionPreference = "Stop"

if (-not (Test-Path -LiteralPath $ServerDataPath -PathType Container)) {
    throw "ServerData directory was not found: $ServerDataPath"
}

$serverData = (Resolve-Path -LiteralPath $ServerDataPath).Path
$headersPath = Join-Path $serverData "_headers"

$headersContent = @'
/*
  Access-Control-Allow-Origin: *
  X-Robots-Tag: noindex

/*.bundle
  Cache-Control: public, max-age=31556952, immutable

/*.hash
  Cache-Control: no-store

/*.json
  Cache-Control: no-cache
'@

# UTF-8 without BOM, compatible with Windows PowerShell 5.1 and PowerShell 7.
[System.IO.File]::WriteAllText(
    $headersPath,
    $headersContent,
    [System.Text.UTF8Encoding]::new($false)
)

$files = @(Get-ChildItem -LiteralPath $serverData -Recurse -File)

if ($files.Count -eq 0) {
    throw "ServerData is empty. Build Addressables content first."
}

if ($files.Count -gt 20000) {
    throw "Cloudflare Pages limit exceeded: $($files.Count) files."
}

$tooLarge = @($files | Where-Object { $_.Length -gt 25MB })

if ($tooLarge.Count -gt 0) {
    $paths = $tooLarge.FullName -join [Environment]::NewLine
    throw "The following files exceed the 25 MiB limit:`n$paths"
}

$jsonCatalogs = @(Get-ChildItem -LiteralPath $serverData -Recurse -File -Filter "catalog*.json")
$hashCatalogs = @(Get-ChildItem -LiteralPath $serverData -Recurse -File -Filter "catalog*.hash")
$bundles = @(Get-ChildItem -LiteralPath $serverData -Recurse -File -Filter "*.bundle")

if ($jsonCatalogs.Count -eq 0) {
    throw "No JSON Addressables catalog was found."
}

if ($hashCatalogs.Count -eq 0) {
    throw "No Addressables catalog hash was found."
}

if ($bundles.Count -eq 0) {
    throw "No Addressables bundles were found."
}

if (-not (Get-Command npx.cmd -ErrorAction SilentlyContinue)) {
    throw "npx was not found. Install the current Node.js LTS release first."
}

Write-Host "Deploying $($files.Count) files from:"
Write-Host $serverData

& npx.cmd wrangler pages deploy $serverData `
    --project-name $ProjectName `
    --branch main

if ($LASTEXITCODE -ne 0) {
    throw "Wrangler deployment failed with exit code $LASTEXITCODE."
}

if (-not $SkipHttpVerification) {
    $baseUri = $PublicBaseUrl.TrimEnd('/')
    if (-not [Uri]::IsWellFormedUriString($baseUri, [UriKind]::Absolute) -or
        -not $baseUri.StartsWith("https://", [StringComparison]::OrdinalIgnoreCase)) {
        throw "PublicBaseUrl must be an absolute HTTPS URL."
    }

    $publishedFiles = @($jsonCatalogs) + @($hashCatalogs) + @($bundles)
    foreach ($file in $publishedFiles) {
        $relativePath = $file.FullName.Substring($serverData.Length).TrimStart([char[]]"\/")
        $publicUrl = $baseUri + '/' + $relativePath.Replace('\', '/')
        $verified = $false

        for ($attempt = 1; $attempt -le 5; $attempt++) {
            try {
                $response = Invoke-WebRequest -Uri $publicUrl -Method Head -UseBasicParsing
                if ($response.StatusCode -eq 200) {
                    $verified = $true
                    break
                }
            }
            catch {
                if ($attempt -eq 5) {
                    throw "Published file did not return HTTP 200: $publicUrl`n$($_.Exception.Message)"
                }
            }

            Start-Sleep -Seconds 2
        }

        if (-not $verified) {
            throw "Published file did not return HTTP 200: $publicUrl"
        }
    }

    Write-Host "Verified $($publishedFiles.Count) catalog/hash/bundle URLs over HTTPS."
}

Write-Host "Deployment completed successfully."
