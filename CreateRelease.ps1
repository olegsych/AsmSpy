<#
.SYNOPSIS
    Packages the built Windows executable for ZIP and Chocolatey distribution.
#>
[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

if (-not $IsWindows) { throw 'Executable distribution requires Windows and ILMerge.' }

[string] $Root = $PSScriptRoot
[string] $Output = Join-Path $Root 'out'
[string] $Executable = Join-Path $Root 'bin\Release\net472\AsmSpy.exe'
[string] $Configuration = Join-Path $Root 'bin\Release\net472\AsmSpy.CommandLine.exe.config'
[string] $CoreAssembly = Join-Path $Root 'bin\Release\net472\AsmSpy.Core.dll'

function Main {
    if (-not (Test-Path -LiteralPath $Executable)) { throw 'Build the Release configuration before packaging.' }
    if (-not (Test-Path -LiteralPath $Configuration)) { throw 'The Release executable configuration is missing.' }
    Get-Command choco -ErrorAction Stop | Out-Null

    [System.IO.FileInfo[]] $packages = @(Get-ChildItem (Join-Path $Output 'packages') -Filter 'AsmSpy.Core.*.nupkg')
    if ($packages.Count -ne 1) { throw 'Pack Core first; out\packages must contain exactly one AsmSpy.Core nupkg. Remove stale packages.' }
    [string] $version = ReadPackageVersion $packages[0].FullName
    if ([System.Diagnostics.FileVersionInfo]::GetVersionInfo($Executable).ProductVersion -ne
        [System.Diagnostics.FileVersionInfo]::GetVersionInfo($CoreAssembly).ProductVersion) {
        throw 'The merged executable and Core assembly are from different builds. Rebuild Release.'
    }
    [string] $distribution = Join-Path $Output 'executable'
    New-Item -ItemType Directory -Path $distribution -Force | Out-Null
    Copy-Item -LiteralPath $Executable -Destination (Join-Path $distribution 'AsmSpy.exe')
    Copy-Item -LiteralPath $Configuration -Destination (Join-Path $distribution 'AsmSpy.exe.config')
    Copy-Item -LiteralPath (Join-Path $Root 'licence.txt') -Destination (Join-Path $distribution 'LICENSE.txt')
    Copy-Item -LiteralPath (Join-Path $Root 'AsmSpy.CommandLine\LICENSE.txt') -Destination (Join-Path $distribution 'ThirdPartyNotices.txt')
    Compress-Archive -LiteralPath @(
        (Join-Path $distribution 'AsmSpy.exe'),
        (Join-Path $distribution 'AsmSpy.exe.config'),
        (Join-Path $distribution 'LICENSE.txt'),
        (Join-Path $distribution 'ThirdPartyNotices.txt')
    ) -DestinationPath (Join-Path $Output "asmspy.$version.zip") -Force

    Push-Location $Root
    try {
        & choco pack (Join-Path $Root 'AsmSpy.CommandLine\AsmSpy.nuspec') --version $version `
            --outputdirectory (Join-Path $Output 'chocolatey')
        if ($LASTEXITCODE -ne 0) { throw 'Chocolatey package creation failed.' }
    } finally {
        Pop-Location
    }
}

function ReadPackageVersion([string] $path) {
    [System.IO.Compression.ZipArchive] $archive = [System.IO.Compression.ZipFile]::OpenRead($path)
    try {
        [System.IO.Stream] $assembly = $archive.GetEntry('lib/net472/AsmSpy.Core.dll').Open()
        [System.Security.Cryptography.SHA256] $hash = [System.Security.Cryptography.SHA256]::Create()
        try {
            if ([Convert]::ToHexString($hash.ComputeHash($assembly)) -ne (Get-FileHash -LiteralPath $CoreAssembly).Hash) {
                throw 'The Core package is stale. Pack the current Release build before packaging the executable.'
            }
        } finally {
            $hash.Dispose()
            $assembly.Dispose()
        }
        [System.IO.StreamReader] $reader = [System.IO.StreamReader]::new($archive.GetEntry('AsmSpy.Core.nuspec').Open())
        try {
            [xml] $manifest = $reader.ReadToEnd()
            return [string] $manifest.package.metadata.version
        } finally {
            $reader.Dispose()
        }
    } finally {
        $archive.Dispose()
    }
}

Main
