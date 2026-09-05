param([switch]$Test, [switch]$UiSmoke, [switch]$Package)
$ErrorActionPreference = 'Stop'
$projectRoot = $PSScriptRoot
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (-not (Test-Path -LiteralPath $compiler)) { $compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319\csc.exe' }
if (-not (Test-Path -LiteralPath $compiler)) { throw '.NET Framework C# compiler was not found.' }
$outRoot = Join-Path $projectRoot 'dist'
$testRoot = Join-Path $projectRoot 'build'
New-Item -ItemType Directory -Path $outRoot,$testRoot -Force | Out-Null
$releaseVersion = [IO.File]::ReadAllText((Join-Path $projectRoot 'VERSION')).Trim()
if ($releaseVersion -notmatch '^\d{1,4}\.\d{1,4}\.\d{1,4}$') { throw 'VERSION must contain a three-part numeric version.' }
$versionSource = Join-Path $testRoot 'VersionInfo.cs'
$versionCode = 'using System.Reflection;' + [Environment]::NewLine + '[assembly: AssemblyVersion("' + $releaseVersion + '.0")]' + [Environment]::NewLine + '[assembly: AssemblyFileVersion("' + $releaseVersion + '.0")]'
[IO.File]::WriteAllText($versionSource, $versionCode)
$manifestPath = Join-Path $testRoot 'app.manifest'
[IO.File]::WriteAllText($manifestPath, [IO.File]::ReadAllText((Join-Path $projectRoot 'src\app.manifest')).Replace('__VERSION__', $releaseVersion + '.0'))
$references = @('/r:System.dll','/r:System.Core.dll','/r:System.Runtime.Serialization.dll','/r:System.Windows.Forms.dll','/r:System.Drawing.dll')
$core = @(Get-ChildItem -LiteralPath (Join-Path $projectRoot 'src') -Filter '*.cs' | Where-Object Name -notin @('Program.cs','MainForm.cs') | ForEach-Object FullName)
if ($Test) {
    & $compiler /nologo /warn:4 /warnaserror /target:exe /platform:anycpu /out:"$testRoot\CoreTests.exe" @references @core (Join-Path $projectRoot 'tests\CoreTests.cs') (Join-Path $projectRoot 'tests\LocalizationTests.cs')
    if ($LASTEXITCODE -ne 0) { throw 'Test compilation failed.' }
    & "$testRoot\CoreTests.exe"
    if ($LASTEXITCODE -ne 0) { throw 'Regression tests failed.' }
}
if (Test-Path -LiteralPath (Join-Path $projectRoot 'src\Program.cs')) {
    $sources = @(Get-ChildItem -LiteralPath (Join-Path $projectRoot 'src') -Filter '*.cs' | ForEach-Object FullName)
    & $compiler /nologo /warn:4 /warnaserror /target:winexe /platform:anycpu /optimize+ /win32manifest:"$manifestPath" /out:"$outRoot\TheyAreBillionsSaveManager.exe" @references @sources $versionSource
    if ($LASTEXITCODE -ne 0) { throw 'Application compilation failed.' }
    Write-Output "Built: $outRoot\TheyAreBillionsSaveManager.exe"
}
if ($UiSmoke) {
    & $compiler /nologo /warn:4 /warnaserror /target:exe /platform:anycpu /out:"$testRoot\UiSmoke.exe" @references @core (Join-Path $projectRoot 'src\MainForm.cs') (Join-Path $projectRoot 'tests\UiSmoke.cs')
    if ($LASTEXITCODE -ne 0) { throw 'UI smoke test compilation failed.' }
    & "$testRoot\UiSmoke.exe" $testRoot
    if ($LASTEXITCODE -ne 0) { throw 'UI smoke test failed.' }
}
if ($Package) {
    $packageRoot = Join-Path $testRoot ('package-' + [Guid]::NewGuid().ToString('N'))
    $packageDist = Join-Path $packageRoot 'dist'
    New-Item -ItemType Directory -Path $packageDist -Force | Out-Null
    Copy-Item -LiteralPath (Join-Path $outRoot 'TheyAreBillionsSaveManager.exe') -Destination $packageDist -Force
    $packageFiles = @('README.md','README.en.md','CHANGELOG.md','Start.cmd') | ForEach-Object { Join-Path $projectRoot $_ }
    Copy-Item -LiteralPath $packageFiles -Destination $packageRoot -Force
    $zipPath = Join-Path $outRoot ('TheyAreBillionsSaveManager-v' + $releaseVersion + '-windows.zip')
    Compress-Archive -Path (Join-Path $packageRoot '*') -DestinationPath $zipPath -Force
    $checksums = @($zipPath,(Join-Path $outRoot 'TheyAreBillionsSaveManager.exe')) | ForEach-Object { (Get-FileHash -LiteralPath $_ -Algorithm SHA256).Hash.ToLowerInvariant() + '  ' + [IO.Path]::GetFileName($_) }
    [IO.File]::WriteAllLines((Join-Path $outRoot 'SHA256SUMS.txt'), $checksums, [Text.Encoding]::ASCII)
    Write-Output "Packaged: $zipPath"
}
