param([switch]$Force)
$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent $PSScriptRoot
$version = '1.0.2'
$dist = Join-Path $repo 'dist'
New-Item -ItemType Directory -Path $dist -Force | Out-Null
$archive = Join-Path $dist "TS2MP4-Windows-v$version.zip"
if ((Test-Path -LiteralPath $archive) -and !$Force) { throw 'Archive already exists. Use -Force to replace it.' }
& (Join-Path $repo 'Build-Windows.cmd') /nopause
if ($LASTEXITCODE -ne 0) { throw 'Build failed.' }
$stage = Join-Path ([IO.Path]::GetTempPath()) ('TS2MP4-package-' + [Guid]::NewGuid().ToString('N'))
$app = Join-Path $stage 'TS2MP4'
New-Item -ItemType Directory -Path $app -Force | Out-Null
try {
    foreach ($item in @('LICENSE','README.md','CHANGELOG.md','THIRD_PARTY_NOTICES.md','Start-TS2MP4.cmd','Build-Windows.cmd','TS2MP4.exe.config','source','docs','releases','scripts','tests','CONTRIBUTING.md')) {
        Copy-Item -LiteralPath (Join-Path $repo $item) -Destination $app -Recurse
    }
    Copy-Item -LiteralPath (Join-Path $repo 'bin\TS2MP4.exe') -Destination $app
    Compress-Archive -LiteralPath $app -DestinationPath $archive -Force
    $hash = (Get-FileHash -LiteralPath $archive -Algorithm SHA256).Hash.ToLowerInvariant()
    [IO.File]::WriteAllText((Join-Path $dist 'SHA256SUMS.txt'), ($hash + '  ' + [IO.Path]::GetFileName($archive) + "`n"), [Text.Encoding]::ASCII)
    Write-Host "Created $archive"
} finally {
    Remove-Item -LiteralPath $stage -Recurse -Force
}
