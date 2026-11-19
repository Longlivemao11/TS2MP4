param([switch]$Force)
$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent $PSScriptRoot
$assembly = [IO.File]::ReadAllText((Join-Path $repo 'source\AssemblyInfo.cs'))
$versionMatch = [regex]::Match($assembly, 'AssemblyFileVersion\("(\d+\.\d+\.\d+)\.\d+"\)')
if (!$versionMatch.Success) { throw 'Cannot read the application version.' }
$version = $versionMatch.Groups[1].Value
$dist = Join-Path $repo 'dist'
New-Item -ItemType Directory -Path $dist -Force | Out-Null
$archive = Join-Path $dist "TS2MP4-Windows-v$version.zip"
$sourceArchive = Join-Path $dist "TS2MP4-Source-v$version.zip"
$checksums = Join-Path $dist 'SHA256SUMS.txt'
foreach ($path in @($archive, $sourceArchive, $checksums)) {
    if ((Test-Path -LiteralPath $path) -and !$Force) { throw 'Output already exists. Use -Force to replace it.' }
}
& (Join-Path $repo 'Build-Windows.cmd') /nopause
if ($LASTEXITCODE -ne 0) { throw 'Build failed.' }
$stage = Join-Path ([IO.Path]::GetTempPath()) ('TS2MP4-package-' + [Guid]::NewGuid().ToString('N'))
$app = Join-Path $stage 'TS2MP4'
New-Item -ItemType Directory -Path $app -Force | Out-Null
try {
    foreach ($item in @('LICENSE','README.md','CHANGELOG.md','THIRD_PARTY_NOTICES.md','Start-TS2MP4.cmd','Build-Windows.cmd','TS2MP4.exe.config','source','docs','releases','scripts','tests','CONTRIBUTING.md','.gitattributes','.gitignore','.github')) {
        $path = Join-Path $repo $item
        if (Test-Path -LiteralPath $path) { Copy-Item -LiteralPath $path -Destination $app -Recurse }
    }
    Compress-Archive -LiteralPath $app -DestinationPath $sourceArchive -Force
    Copy-Item -LiteralPath (Join-Path $repo 'bin\TS2MP4.exe') -Destination $app
    Compress-Archive -LiteralPath $app -DestinationPath $archive -Force
    $lines = foreach ($path in @($archive, $sourceArchive)) {
        (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash.ToLowerInvariant() + '  ' + [IO.Path]::GetFileName($path)
    }
    [IO.File]::WriteAllText($checksums, (($lines -join "`n") + "`n"), [Text.Encoding]::ASCII)
    Write-Host "Created $archive"
    Write-Host "Created $sourceArchive"
} finally {
    Remove-Item -LiteralPath $stage -Recurse -Force
}
