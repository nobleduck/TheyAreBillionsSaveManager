param([switch]$Test, [switch]$UiSmoke, [switch]$Package)
$ErrorActionPreference = 'Stop'
$projectRoot = $PSScriptRoot
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (-not (Test-Path -LiteralPath $compiler)) { $compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319\csc.exe' }
if (-not (Test-Path -LiteralPath $compiler)) { throw '.NET Framework C# compiler was not found.' }
$outRoot = Join-Path $projectRoot 'dist'
$testRoot = Join-Path $projectRoot 'build'
New-Item -ItemType Directory -Path $outRoot,$testRoot -Force | Out-Null
$references = @('/r:System.dll','/r:System.Core.dll','/r:System.Runtime.Serialization.dll','/r:System.Windows.Forms.dll','/r:System.Drawing.dll')
$core = @(Get-ChildItem -LiteralPath (Join-Path $projectRoot 'src') -Filter '*.cs' | Where-Object Name -notin @('Program.cs','MainForm.cs') | ForEach-Object FullName)
if ($Test) {
    & $compiler /nologo /warn:4 /warnaserror /target:exe /platform:anycpu /out:"$testRoot\CoreTests.exe" @references @core (Join-Path $projectRoot 'tests\CoreTests.cs')
    if ($LASTEXITCODE -ne 0) { throw 'Test compilation failed.' }
    & "$testRoot\CoreTests.exe"
    if ($LASTEXITCODE -ne 0) { throw 'Regression tests failed.' }
}
if (Test-Path -LiteralPath (Join-Path $projectRoot 'src\Program.cs')) {
    $sources = @(Get-ChildItem -LiteralPath (Join-Path $projectRoot 'src') -Filter '*.cs' | ForEach-Object FullName)
    & $compiler /nologo /warn:4 /warnaserror /target:winexe /platform:anycpu /optimize+ /win32manifest:"$projectRoot\src\app.manifest" /out:"$outRoot\TheyAreBillionsSaveManager.exe" @references @sources
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
    $packageRoot = Join-Path $testRoot 'package'
    $packageDist = Join-Path $packageRoot 'dist'
    New-Item -ItemType Directory -Path $packageDist -Force | Out-Null
    Copy-Item -LiteralPath (Join-Path $outRoot 'TheyAreBillionsSaveManager.exe') -Destination $packageDist -Force
    Copy-Item -LiteralPath (Join-Path $projectRoot 'README.md'),(Join-Path $projectRoot 'Start.cmd') -Destination $packageRoot -Force
    $zipPath = Join-Path $outRoot 'TheyAreBillionsSaveManager-v0.1.0-windows.zip'
    Compress-Archive -LiteralPath (Join-Path $packageRoot 'README.md'),(Join-Path $packageRoot 'Start.cmd'),$packageDist -DestinationPath $zipPath -Force
    Write-Output "Packaged: $zipPath"
}
