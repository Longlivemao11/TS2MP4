param(
    [Parameter(Mandatory = $true)][string]$FFmpeg,
    [string]$FFprobe = '',
    [switch]$IncludeUI
)
$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent $PSScriptRoot
$FFmpeg = (Resolve-Path -LiteralPath $FFmpeg).Path
if (!$FFprobe) { $FFprobe = Join-Path (Split-Path -Parent $FFmpeg) 'ffprobe.exe' }
$FFprobe = (Resolve-Path -LiteralPath $FFprobe).Path
$application = Join-Path $repo 'bin\TS2MP4.exe'
if (!(Test-Path -LiteralPath $application)) { throw 'Run Build-Windows.cmd first.' }
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (!(Test-Path -LiteralPath $compiler)) { $compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319\csc.exe' }
if (!(Test-Path -LiteralPath $compiler)) { throw '.NET Framework compiler was not found.' }
$temporary = Join-Path ([IO.Path]::GetTempPath()) ('TS2MP4-tests-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $temporary | Out-Null
$settings = Join-Path $env:LOCALAPPDATA 'TS2MP4\settings.xml'
$hadSettings = Test-Path -LiteralPath $settings
$settingsBytes = $null
if ($IncludeUI -and $hadSettings) { $settingsBytes = [IO.File]::ReadAllBytes($settings) }
function Invoke-FFmpeg([string[]]$Arguments) {
    & $FFmpeg @Arguments
    if ($LASTEXITCODE -ne 0) { throw 'Fixture generation failed.' }
}
try {
    $inputVideo = Join-Path $temporary "中文 空格 & 100% ' 引号.ts"
    Invoke-FFmpeg -Arguments @('-hide_banner','-loglevel','error','-nostdin','-f','lavfi','-i','testsrc2=duration=3:size=320x240:rate=25','-f','lavfi','-i','sine=frequency=440:duration=3','-c:v','libx264','-preset','ultrafast','-pix_fmt','yuv420p','-c:a','aac','-shortest',$inputVideo)
    Invoke-FFmpeg -Arguments @('-hide_banner','-loglevel','error','-nostdin','-f','lavfi','-i','testsrc2=duration=3:size=320x240:rate=25','-c:v','libx264','-preset','ultrafast','-an',(Join-Path $temporary 'silent.ts'))
    Invoke-FFmpeg -Arguments @('-hide_banner','-loglevel','error','-nostdin','-i',$inputVideo,'-map','0:v:0','-map','0:a:0','-map','0:a:0','-c','copy',(Join-Path $temporary 'multi.ts'))
    Invoke-FFmpeg -Arguments @('-hide_banner','-loglevel','error','-nostdin','-f','lavfi','-i','testsrc2=duration=60:size=1280x720:rate=30','-c:v','libx264','-preset','ultrafast','-crf','28','-an',(Join-Path $temporary 'long.ts'))
    [IO.File]::WriteAllText((Join-Path $temporary 'broken.ts'), 'not a video')
    Copy-Item -LiteralPath $application -Destination $temporary
    Copy-Item -LiteralPath ($application + '.config') -Destination $temporary
    $coreTest = Join-Path $temporary 'CoreTests.exe'
    & $compiler /nologo /codepage:65001 "/out:$coreTest" "/reference:$application" /reference:System.Core.dll (Join-Path $PSScriptRoot 'CoreTests.cs')
    if ($LASTEXITCODE -ne 0) { throw 'Core test compilation failed.' }
    & $coreTest $temporary $FFmpeg $FFprobe
    if ($LASTEXITCODE -ne 0) { throw 'Core integration tests failed.' }
    if ($IncludeUI) {
        $uiTest = Join-Path $temporary 'UITests.exe'
        & $compiler /nologo /codepage:65001 "/out:$uiTest" "/reference:$application" /reference:System.Core.dll /reference:System.Windows.Forms.dll /reference:System.Drawing.dll (Join-Path $PSScriptRoot 'UITests.cs')
        if ($LASTEXITCODE -ne 0) { throw 'UI test compilation failed.' }
        & $uiTest $inputVideo (Join-Path $temporary 'ui.png') $FFmpeg
        if ($LASTEXITCODE -ne 0) { throw 'UI integration tests failed.' }
    }
    Write-Host 'All requested integration tests passed.'
} finally {
    if ($IncludeUI) {
        if ($hadSettings) { [IO.File]::WriteAllBytes($settings, $settingsBytes) }
        elseif (Test-Path -LiteralPath $settings) { Remove-Item -LiteralPath $settings }
    }
    Remove-Item -LiteralPath $temporary -Recurse -Force
}
