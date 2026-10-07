param (
    [Parameter(Mandatory=$true)]
    [string]$CompilerPath,
    [Parameter(Mandatory=$false)]
    [string]$OutputPath = (Join-Path (Split-Path -Parent $PSScriptRoot) 'output')
)

$ErrorActionPreference = 'Stop'

# Resolve and validate mandatory compiler path
$CompilerPath = Resolve-Path -LiteralPath $CompilerPath

# Determine repository root and create output directory
$repoPath = Split-Path -Parent $PSScriptRoot
if (-not (Test-Path -LiteralPath $OutputPath)) {
    New-Item -ItemType Directory -Force -Path $OutputPath | Out-Null
}

# Paths for pure source files (in repository root)
$pureSources = @(
    'WheelSelection.cs', 'WheelAimInput.cs', 'WheelMotion.cs', 'LastGraffitiStore.cs', 'WirkleMotion.cs'
) | ForEach-Object { Join-Path $repoPath $_ }

# Paths for test files (in tests folder)
$testSources = @(
    'WheelSelectionTests.cs', 'WheelAimInputTests.cs', 'WheelMotionTests.cs', 'WirkleMotionTests.cs'
) | ForEach-Object { Join-Path $PSScriptRoot $_ }

$testOutput = Join-Path $OutputPath 'WheelSelectionTests.exe'

# Compile the test executable
$compileArgs = @(
    '/nologo',
    '/target:exe',
    ('/out:' + $testOutput)
) + $pureSources + $testSources

& $CompilerPath @compileArgs
if ($LASTEXITCODE -ne 0) { throw 'Navigation test compilation failed.' }

# Run the test executable
& $testOutput
if ($LASTEXITCODE -ne 0) { throw 'Navigation tests failed.' }

$timingOutput = Join-Path $OutputPath 'PresentationTimingTests.exe'
& $CompilerPath /nologo /target:exe (('/out:') + $timingOutput) (Join-Path $repoPath 'PresentationTiming.cs') (Join-Path $repoPath 'CameraMotionState.cs') (Join-Path $PSScriptRoot 'PresentationTimingTests.cs')
if ($LASTEXITCODE -ne 0) { throw 'Presentation timing test compilation failed.' }
& $timingOutput
if ($LASTEXITCODE -ne 0) { throw 'Presentation timing tests failed.' }
