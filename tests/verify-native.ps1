param (
    [Parameter(Mandatory=$true)]
    [string]$CompilerPath,
    [Parameter(Mandatory=$true)]
    [string]$GameManagedPath,
    [Parameter(Mandatory=$true)]
    [string]$BepInExCorePath,
    [Parameter(Mandatory=$false)]
    [string]$OutputPath = (Join-Path (Split-Path -Parent $PSScriptRoot) 'output')
)

$ErrorActionPreference = 'Stop'

# Resolve and validate mandatory paths
$CompilerPath   = Resolve-Path -LiteralPath $CompilerPath
$GameManagedPath = Resolve-Path -LiteralPath $GameManagedPath
$BepInExCorePath = Resolve-Path -LiteralPath $BepInExCorePath

# Determine repository root and create output directory
$repoPath = Split-Path -Parent $PSScriptRoot
if (-not (Test-Path -LiteralPath $OutputPath)) {
    New-Item -ItemType Directory -Force -Path $OutputPath | Out-Null
}

# Paths
$productDLL = Join-Path $repoPath 'Thunderstore\plugins\QuickSpraySelector\QuickSpraySelector.dll'
$testExe    = Join-Path $OutputPath 'NativeFlowTests.exe'
if (-not (Test-Path -LiteralPath $productDLL)) { throw 'Build the plugin before running native-flow tests.' }

# Compile the native flow test executable
$compileArgs = @(
    '/nologo',
    '/target:exe',
    ('/out:' + $testExe),
    ('/reference:' + (Join-Path $GameManagedPath 'Assembly-CSharp.dll')),
    ('/reference:' + (Join-Path $GameManagedPath 'UnityEngine.CoreModule.dll')),
    ('/reference:' + (Join-Path $BepInExCorePath '0Harmony.dll'))
) + (Join-Path $PSScriptRoot 'NativeFlowTests.cs')

& $CompilerPath @compileArgs
if ($LASTEXITCODE -ne 0) { throw 'Native-flow test compilation failed.' }

# Run the native flow test executable
& $testExe $GameManagedPath $BepInExCorePath $productDLL
if ($LASTEXITCODE -ne 0) { throw 'Native-flow tests failed.' }
